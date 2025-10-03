using System;
using UnityEngine;
using Mirror;
using System.Collections;

public class GrenadeProjectile : NetworkBehaviour
{
    public static event EventHandler OnAnyGranadeExploded;

    [SerializeField] private Transform granadeExplodeVFXPrefab;
    [SerializeField] private float damageRadius = 4f;
    [SerializeField] private int damage = 30;
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private AnimationCurve arcYAnimationCurve;

    [SyncVar(hook = nameof(OnTargetChanged))] private Vector3 targetPosition;

    private float totalDistance;
    private Vector3 positionXZ;
    private const float MIN_DIST = 0.01f;

    private bool isExploded = false;

    private bool _ready;

    public override void OnStartClient()
    {
        base.OnStartClient();
    }

    public void Setup(Vector3 targetWorld)
    {
        var groundTarget = SnapToGround(targetWorld);
        // Aseta SyncVar, hook kutsutaan kaikilla (server + clientit)
        targetPosition = groundTarget;
        RecomputeDerived(); // varmistetaan serverillä heti
        _ready = true;
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


        if ((Vector3.Distance(positionXZ, targetPosition) < reachedTargetDistance) && !isExploded)
        {
            isExploded = true;
            if (NetworkServer.active || !NetworkClient.isConnected) // Server or offline
            {
                Collider[] colliderArray = Physics.OverlapSphere(targetPosition, damageRadius);

                foreach (Collider collider in colliderArray)
                {
                    if (collider.TryGetComponent<Unit>(out Unit targetUnit))
                    {
                        NetworkSync.ApplyDamageToUnit(targetUnit, damage, targetPosition);
                    }
                    if (collider.TryGetComponent<DestructibleObject>(out DestructibleObject targetObject))
                    {
                         NetworkSync.ApplyDamageToObject(targetObject, damage, targetPosition);
                    }
                }
            }
           
            // Screen Shake
            OnAnyGranadeExploded?.Invoke(this, EventArgs.Empty);
            // Explode VFX
            Instantiate(granadeExplodeVFXPrefab, targetPosition + Vector3.up * 1f, Quaternion.identity);

            if (!NetworkServer.active)
            {
                Destroy(gameObject);
                return;
            }

            // Online: Hide Granade before destroy it, so that client have time to create own explode VFX from orginal Granade pose.
            SetSoftHiddenLocal(true);
            RpcSetSoftHidden(true);
            StartCoroutine(DestroyAfter(0.30f));      
        }
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

    private void SetSoftHiddenLocal(bool hidden)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = !hidden;
        }
    }
}
