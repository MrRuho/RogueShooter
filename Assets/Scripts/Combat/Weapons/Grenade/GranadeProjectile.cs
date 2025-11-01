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
    [SerializeField] private float landingJitterRadius = 0.18f;

    [Header("Config")]
    [SerializeField] private ThrowArcConfig throwArcConfig;
    [SerializeField] private LayerMask ceilingMask;
    [SerializeField] private LayerMask floorMask;
    [SerializeField] private float ceilingClearance = 0.08f;
    [SerializeField] private int apexSamples = 24;
    [SerializeField] private float fallbackMaxThrowRangeWU = 12f;   // jos arvoa ei syötetä ulkoa
    [SerializeField] private float horizontalSpeed = 10f;           // tai käytä nykyistäsi

    [Header("Curve (fallback)")]
    [SerializeField] private AnimationCurve arcYAnimationCurve;

    private Vector3 _startPos;
    private Vector3 _endPos;
    private float _apexWU;           // dynaaminen huippukorkeus
    private float _travelT;          // 0..1
    private float _travelDuration;   // aika maaliin
    private float _maxRangeWU; 


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

    void Awake() 
    {
        var sc = GetComponent<SphereCollider>();
        if (sc && throwArcConfig)
            sc.radius = throwArcConfig.projectileRadiusWU;  // yksi totuus
        
        // jos haluat varmistaa fallback-käyrän
        if (!throwArcConfig && arcYAnimationCurve == null)
            arcYAnimationCurve = AnimationCurve.Linear(0,0,1,0);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
    }

    public void Setup(Vector3 targetWorld)
    {
        _maxRangeWU = fallbackMaxThrowRangeWU;

        // 1) SNAP & aseta target ensin
       // var groundTarget = SnapToGround(targetWorld);
        targetPosition = targetWorld; 
       // targetPosition = groundTarget;

        // 2) Laske kaikki johdetut (sis. katon huomioivan apexin)
        RecomputeDerived();

        // 3) Entinen logiikka ennallaan
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        _ready = true;
        if (GameModeManager.SelectedMode == GameMode.CoOp) timer += 1; else timer = 2;
    }

    public void Setup(Vector3 targetWorld, float maxThrowRangeWU)
    {
        _maxRangeWU = (maxThrowRangeWU > 0f) ? maxThrowRangeWU : fallbackMaxThrowRangeWU;

        //var groundTarget = SnapToGround(targetWorld);
       //targetPosition = groundTarget;
        targetPosition = targetWorld; 

        RecomputeDerived();

        // ← nämä puuttuivat 4-param polusta
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        _ready = true;
        if (GameModeManager.SelectedMode == GameMode.CoOp) timer += 1; else timer = 2;
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

    /*
    private void RecomputeDerived()
    {
        // Päivitä alku- ja loppupisteet
        _startPos = transform.position;
        _endPos   = targetPosition;

        // Vaakasuora etäisyys (XZ)
        Vector2 s = new Vector2(_startPos.x, _startPos.z);
        Vector2 e = new Vector2(_endPos.x,   _endPos.z);
        float dWU = Vector2.Distance(s, e);

        // Kuinka monella samplella arvioidaan katto (sama haarukka kuin preview/validointi)
        int samples = (throwArcConfig != null)
            ? Mathf.Clamp(throwArcConfig.EvaluateSegments(dWU, Mathf.Max(0.01f, 2f)), 12, 40)
            : Mathf.Clamp(12 + Mathf.RoundToInt(dWU / Mathf.Max(0.01f, 2f)) * 4, 12, 40);

        // Käytä teidän max-rangea apexin arvioinnissa
        float farWU = (_maxRangeWU > 0f)
            ? _maxRangeWU
            : (throwArcConfig != null ? throwArcConfig.farRangeWU : dWU);

        // Kakkoskaari: clamp apex katon alle jos reitillä on katto
        _apexWU = ArcApexSolver.ComputeCeilingClampedApex(
            _startPos, _endPos, throwArcConfig, ceilingMask,
            ceilingClearance, samples, farWUOverride: farWU
        );

        // Liikkeen aikaparametrit: tasainen vaakanopeus
        _travelDuration = Mathf.Max(0.05f, dWU / Mathf.Max(0.01f, horizontalSpeed));
        _travelT = 0f;

        // Sisäinen apu jo olemassa olevaa laskeutumista varten
        positionXZ = _startPos; positionXZ.y = 0f;
        totalDistance = Mathf.Max(MIN_DIST, Vector3.Distance(positionXZ, _endPos));

        // Varmista että komponentti on päällä
        enabled = true;
    }
*/
    private void RecomputeDerived()
    {
        _startPos = transform.position;
        _endPos   = targetPosition;

        Vector2 s = new(_startPos.x, _startPos.z);
        Vector2 e = new(_endPos.x,   _endPos.z);
        float dWU = Vector2.Distance(s, e);

        int samples = (throwArcConfig != null)
            ? Mathf.Clamp(throwArcConfig.EvaluateSegments(dWU, 2f), 12, 40)
            : Mathf.Clamp(12 + Mathf.RoundToInt(dWU / 2f) * 4, 12, 40);

        float farWU = (_maxRangeWU > 0f) ? _maxRangeWU
                                        : (throwArcConfig != null ? throwArcConfig.farRangeWU : dWU);

        _apexWU = ArcApexSolver.ComputeCeilingClampedApex(
            _startPos, _endPos, throwArcConfig, ceilingMask,
            ceilingClearance, samples, farWUOverride: farWU);
        
        _travelDuration = Mathf.Max(0.05f, dWU / Mathf.Max(0.01f, horizontalSpeed));
        _travelT = 0f;

        enabled = true;
    }

    private void Update()
    {
        if (_travelDuration <= 0f) return;
        _travelT += Time.deltaTime / _travelDuration;
        float t = Mathf.Clamp01(_travelT);

        // Lerp XZ + baseline Y, lisää päälle kaarikerroin
        Vector3 pos = Vector3.Lerp(_startPos, _endPos, t);

        float baselineY = Mathf.Lerp(_startPos.y, _endPos.y, t);
        float yArc = (throwArcConfig ? throwArcConfig.arcYCurve : arcYAnimationCurve).Evaluate(t) * _apexWU;

        pos.y = baselineY + yArc;

        // Suunta eteenpäin (valinnainen)
        Vector3 prev = transform.position;
        transform.position = pos;
        Vector3 dir = pos - prev;
        if (dir.sqrMagnitude > 0.0001f)
            transform.forward = dir.normalized;
        /*
        if (t >= 1f)
        {
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
            // TODO: Explode / OnArrived: sama kuin teillä aiemmin
            // Explode();
            enabled = false;
        }
        */
        if (t >= 1f)
        {
            // a) lähde suoraan _endPos:sta
            Vector3 p = _endPos;

            // b) satunnainen XZ-jitter ruudun sisään
            Vector2 j2 = UnityEngine.Random.insideUnitCircle * landingJitterRadius;
            p.x += j2.x; p.z += j2.y;

            // c) lattia-Y haetaan raylla -> yläkerran lattia jos sen päällä ollaan
            float y = p.y;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 10f, floorMask, QueryTriggerInteraction.Ignore))
                y = hit.point.y;

            if (TryGetComponent<Collider>(out var col)) y += col.bounds.extents.y;
            transform.position = new Vector3(p.x, y, p.z);

            enabled = false; // (räjähdys menee vuoro-timerillä kuten ennen)
        }
    }

    private void Exlosion()
    {
        isExploded = true;
            if (NetMode.ServerOrOff) // Server or offline. NetworkServer.active || !NetworkClient.isConnected
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

        if (!NetMode.IsServer) // NetworkServer.active
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
