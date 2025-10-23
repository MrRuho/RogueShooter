using System;
using UnityEngine;
using Mirror;
using System.Collections;

public class GrenadeProjectile : NetworkBehaviour
{
    [SyncVar] public uint actorUnitNetId;
    public static event EventHandler OnAnyGranadeExploded;

    [SerializeField] private Transform grenadeExplodeVFXPrefab;
    [SerializeField] private float damageRadius = 4f;
    [SerializeField] private int damage = 30;
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private int timer = 2;
    [SerializeField] private LayerMask floorMask = ~0;
    [SerializeField] private float landingJitterRadius = 0.18f;
    [SerializeField] private AnimationCurve arcYAnimationCurve;

    // Pieni hajonta, muutaman sadasosan verran
    [SerializeField] private float explosionJitterMin = 0.02f;
    [SerializeField] private float explosionJitterMax = 0.08f;

    private bool _explosionScheduled; // vartija, ettei ajeta kahta kertaa

    [SyncVar(hook = nameof(OnTargetChanged))] private Vector3 targetPosition;

    private float totalDistance;
    private Vector3 positionXZ;
    private const float MIN_DIST = 0.01f;

    private bool isExploded = false;

    private bool isLanded = false;

    private bool _ready;

    public override void OnStartClient()
    {
        base.OnStartClient();
    }

    public void Setup(Vector3 targetWorld)
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        var groundTarget = SnapToGround(targetWorld);
        // Aseta SyncVar, hook kutsutaan kaikilla (server + clientit)
        targetPosition = groundTarget;
        RecomputeDerived(); // varmistetaan serverillä heti
        _ready = true;
        timer = 2;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        timer -= 1;
        if (timer <= 0 && !_explosionScheduled && !isExploded)
        {
            _explosionScheduled = true;
            StartCoroutine(ExplodeAfterJitter());
        }
    }

    private IEnumerator ExplodeAfterJitter()
    {
        // Deterministinen "satunnaisuus": sama viive serverillä ja clienteillä tälle kranaatille
        uint id = GetComponent<NetworkIdentity>() ? GetComponent<NetworkIdentity>().netId : 0u;
        float t = Mathf.Abs(Mathf.Sin(id * 12.9898f + targetPosition.x * 78.233f + targetPosition.z * 37.719f));
        float delay = Mathf.Lerp(explosionJitterMin, explosionJitterMax, t);

        yield return new WaitForSeconds(delay);

        Exlosion();
    }

    private void OnDestroy()
    {
        TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
    }

    private Vector3 SnapToGround(Vector3 worldXZ)
    {
        return new Vector3(worldXZ.x, 0f, worldXZ.z);
    }

    void OnTargetChanged(Vector3 _old, Vector3 _new)
    {
        // Kun SyncVar saapuu clientille, laske johdetut kentät sielläkin
        RecomputeDerived();
        _ready = true;
    }

    private void RecomputeDerived()
    {
        positionXZ = transform.position;
        positionXZ.y = 0f;

        totalDistance = Vector3.Distance(positionXZ, targetPosition);
        if (totalDistance < MIN_DIST) totalDistance = MIN_DIST; // suoja nollaa vastaan
    }

    private void Update()
    {
        if (!_ready || isExploded) return;
        if (isLanded) return;
        

        Vector3 moveDir = targetPosition - positionXZ;
        if (moveDir.sqrMagnitude < 1e-6f) moveDir = Vector3.forward; // varadir, ettei normalized → NaN
        moveDir.Normalize();

        positionXZ += moveSpeed * Time.deltaTime * moveDir;

        float distance = Vector3.Distance(positionXZ, targetPosition);
        if (totalDistance < 1e-6f) totalDistance = 0.01f;
        float distanceNormalized = 1f - (distance / totalDistance);
        distanceNormalized = Mathf.Clamp01(distanceNormalized);

        float maxHeight = totalDistance / 4f;
        float positionY = arcYAnimationCurve != null
            ? arcYAnimationCurve.Evaluate(distanceNormalized) * maxHeight
            : 0f;

        if (float.IsNaN(positionY)) positionY = 0f;                   // viimeinen pelastus
        transform.position = new Vector3(positionXZ.x, positionY, positionXZ.z);

        float reachedTargetDistance = .2f;


        if (!isLanded && (positionXZ - targetPosition).sqrMagnitude <= reachedTargetDistance * reachedTargetDistance)
        {
            isLanded = true;

            // a) ruudun keskelle (x,z) + oikea kerros-Y LevelGridistä
            var gp = LevelGrid.Instance.GetGridPosition(targetPosition);
            var center = LevelGrid.Instance.GetWorldPosition(gp);         // ruudun keskipiste & floor-Y 
            
            // b) satunnainen siirto ruudun sisällä (XZ)
            Vector2 j2 = UnityEngine.Random.insideUnitCircle * landingJitterRadius;
            Vector3 p = new Vector3(center.x + j2.x, center.y, center.z + j2.y);

            // c) lattia-Y (voit myös käyttää pelkkää center.y)
            float y = p.y;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 10f, floorMask, QueryTriggerInteraction.Ignore))
                y = hit.point.y;

            // d) jos pivot ei ole maassa, kompensoi kolliderin puoli-korkeus
            if (TryGetComponent<Collider>(out var col)) y += col.bounds.extents.y;

            // ASETUS
            transform.position = new Vector3(p.x, y, p.z);
        }
    }

    private void Exlosion()
    {
        isExploded = true;
            if (NetworkServer.active || !NetworkClient.isConnected) // Server or offline
            {
                Collider[] colliderArray = Physics.OverlapSphere(targetPosition, damageRadius);

                foreach (Collider collider in colliderArray)
                {
                    if (collider.TryGetComponent<Unit>(out Unit targetUnit))
                    {
                        NetworkSync.ApplyDamageToUnit(targetUnit, damage, targetPosition, this.GetActorId());
                    }
                    if (collider.TryGetComponent<DestructibleObject>(out DestructibleObject targetObject))
                    {
                        NetworkSync.ApplyDamageToObject(targetObject, damage, targetPosition, this.GetActorId());
                    }
                }
            }

        // Screen Shake
        OnAnyGranadeExploded?.Invoke(this, EventArgs.Empty);

        SpawnRouter.SpawnLocal(
            grenadeExplodeVFXPrefab.gameObject,
            targetPosition + Vector3.up * 1f,
            Quaternion.identity,
            source: transform   // <- scene päätellään lähteestä
            );

        if (!NetworkServer.active)
        {
            Destroy(gameObject);
            return;
        }

        // Online: Hide Granade before destroy it, so that client have time to create own explode VFX from orginal Granade pose.
        SetSoftHiddenLocal(true);
        RpcSetSoftHidden(true);

        // Kerro asiakkaille missä scenessä VFX pitää luoda
        RpcExplodeVFX(gameObject.scene.name, targetPosition);
            
        StartCoroutine(DestroyAfter(0.30f));
    }
     
    private IEnumerator DestroyAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcSetSoftHidden(bool hidden)
    {
        SetSoftHiddenLocal(hidden);
    }

    [ClientRpc]
    private void RpcExplodeVFX(string sceneName, Vector3 pos)
    {
        OnAnyGranadeExploded?.Invoke(this, EventArgs.Empty);

        // Luodaan VFX oikeaan Level-sceeneen clientillä
        SpawnRouter.SpawnLocal(
            grenadeExplodeVFXPrefab.gameObject,
            pos + Vector3.up * 1f,
            Quaternion.identity,
            source: null,
            sceneName: sceneName
        );
    }

    private void SetSoftHiddenLocal(bool hidden)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = !hidden;
        }
    }
}
