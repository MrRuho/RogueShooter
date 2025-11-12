
using System;
using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class GrenadeProjectile : NetworkBehaviour
{
   // ====================== DETERMINISTIC DEFLECTION (OFFLINE) ======================
    [Header("Deflection: Toggle & Mask")]
    [Tooltip("Kytkee deterministisen kimpoilun päälle vain offline-tilassa.")]
    [SerializeField] private bool useDeterministicDeflectionOffline = false;

    [Tooltip("Esineet ja asiat, joista kimpoillaan (seinät/ovet).")]
    [SerializeField] private LayerMask deflectionMask;

    [Header("Floor bounce randomization (OFFLINE visuals only)")]
    [Tooltip("Sallitun lattia-pompun pituuden haarukka WU:na (per throw).")]
    [SerializeField] private Vector2 floorBounceDistanceRangeWU = new Vector2(0.18f, 0.35f);

    [Tooltip("Minimipituuden haarukka WU:na, ettei pomppu typähdä nollaan (per throw).")]
    [SerializeField] private Vector2 minFloorBounceDistanceRangeWU = new Vector2(0.08f, 0.15f);

    [Tooltip("Maksimi satunnais-käännön (yaw) haarukka asteina XZ-tasossa (per throw).")]
    [SerializeField] private Vector2 floorBounceRandomYawRangeDeg = new Vector2(0f, 25f);

    private float _throwFloorBounceDistWU;
    private float _throwMinFloorBounceDistWU;
    private float _throwRandomYawDeg;
    private bool _randomizedThisThrow;
    

    // ---------------------- Pomppujen määrä & matkan hallinta ----------------------
    [Header("Deflection: Bounce Count & Distance")]
    [Tooltip("Kuinka monta kimpoa enintään.")]
    [SerializeField] private int maxBounces = 2;

    // ---------------------- Ajoitus & nopeus ----------------------
    [Header("Deflection: Timing & Speed")]
    [Tooltip("Minimiaika yhdelle segmentille (tuntuma).")]
    [SerializeField] private float minSegmentDuration = 0.25f;

    [Tooltip("Maksimiaika yhdelle segmentille (tuntuma).")]
    [SerializeField] private float maxSegmentDuration = 0.55f;

    // ---------------------- Kaaren muoto pompun jälkeen ----------------------
    [Header("Deflection: Arc Shaping (Bounce Apex)")]
    [Tooltip("0..1: Pienennä pompun kaaren huippukorkeutta (apex).")]
    [SerializeField, Range(0f, 1f)] private float apexBounceScale = 0.6f;

    [Tooltip("Kova yläraja pompun apex-korkeudelle (WU).")]
    [SerializeField] private float maxBounceApexWU = 3.0f;

    [Tooltip("Apexin arvioinnissa käytettävä vähimmäisetäisyys, estää lyhyiden segmenttien ylikorkean kaaren.")]
    [SerializeField] private float apexEvalDistanceFloorWU = 4f;


    // ---------------------- Suunnan ohjaus & vakaus ----------------------
    [Tooltip("Sekunteina: estää välittömän uudelleenosuman samaan collideriin (ping-pong).")]
    [SerializeField] private float minBounceSeparation = 0.06f;

    [Tooltip("Pieni irrotus pinnasta uuden segmentin alussa, ehkäisee tarttumista.")]
    [SerializeField] private float pushOffWU = 0.02f;


    [Header("Floor Bounce (only)")]
    [SerializeField, Range(0.6f, 0.99f)]
    private float floorOnlyMinNormalY = 0.7f; // hyväksy pomppu vain lähes vaakasuorasta pinnasta

    [SerializeField, Range(0f, 25f)]
    private float floorBounceYawTowardGoalDeg = 6f; // käännä suuntaa kohti maalia tämän verran (asteina)

    private float _currentHorizSpeed; // segmenttikohtainen vaakanopeus


    [Header("Flight feel (time-warp around apex)")]
    [Tooltip("Pienin suhteellinen nopeus kaaren huipulla. 1 = ei hidastusta, 0.35 = selkeä hidastus.")]
    [SerializeField, Range(0.1f, 1f)] private float minSpeedScaleAtApex = 0.5f;

    // --- Flight feel (time-warp around apex) ---
    [Header("Flight feel (time-warp around apex)")]
    [Tooltip("Miten kapeasti hidastus kohdistuu huipun ympärille. Suurempi = kapeampi vyöhyke.")]
    [SerializeField, Range(0.5f, 5f)] private float apexSlowExponent = 2.0f;

    // --- Deflection timing/speed ---
    [Header("Deflection: Timing & Speed")]
    [Tooltip("< 1 = hidasta joka pompun jälkeen, 1 = pidä sama nopeus.")]
    [SerializeField, Range(0f, 1f)] private float bounceSpeedScale = 0.75f;

    [Tooltip("Ota käyttöön apex-hidastus kaaren aikana.")]
    [SerializeField] private bool useApexSlowdown = true;
    
    [Header("Distance-scaled speed")]
    [Tooltip("Skaalaa heiton vaakanopeus vaakasuoran etäisyyden (ruutujen) mukaan.")]
    [SerializeField] private bool useDistanceScaledSpeed = true;

    [Tooltip("Vaakanopeus 1 ruudun heitolla.")]
    [SerializeField] private float speedAtOneTile = 2f;

    [Tooltip("Kuinka paljon nopeus kasvaa per lisäruutu.")]
    [SerializeField] private float speedPerTile = 2f;

    [Tooltip("Nopeuden minimiklemmari (varmistaa ettei mene liian hitaaksi).")]
    [SerializeField] private float speedMinClamp = 1.5f;

    [Tooltip("Nopeuden maksimiklemmari (esim. max range ~14).")]
    [SerializeField] private float speedMaxClamp = 14f;

    [Tooltip("Ruutukoko WU:na. Jos WU == ruutu, pidä 1.")]
    [SerializeField] private float tileSizeWU = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] explosionSounds;
    [SerializeField] private float explosionVolume = 1f;
    [SerializeField] private float explosionMaxHearingDistance = 80f;
    [SerializeField] private AnimationCurve explosionVolumeRolloff = AnimationCurve.Linear(0, 1, 1, 0.2f);

    // sisäinen tila
    private int _bounces;
    private Collider _lastHitCollider;
    private float _lastHitTime;

    // *********************** Deterministic END *******************************

    [SyncVar] public uint actorUnitNetId;
    [SyncVar] public uint ownerPlayerNetId;
    [SyncVar] public int ownerTeamId = -1;

    public static event EventHandler OnAnyGranadeExploded;

    [SerializeField] private Transform grenadeExplodeVFXPrefab;
    [SerializeField] private float damageRadius = 4f;
    [SerializeField] private int damage = 30;

    [Header("Config")]
    [SerializeField] private ThrowArcConfig throwArcConfig;
    [SerializeField] private LayerMask ceilingMask;
    [SerializeField] private LayerMask floorMask;
    [SerializeField] private float ceilingClearance = 0.08f;
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

    private bool isExploded = false;

    // Synkattu räjähdyksen ajastus
    private bool  _armed;
    private double _explodeAt;    // server-aika jolloin räjähtää
    private bool _damageDone;    // server: damage tehty

    private GrenadeBeaconEffect beaconEffect;

    [Tooltip("Jos off niin räjähtää vihollisen vuoron lopussa")]
    [Header("Timer. How Meny actions Player can make before explosion")]
    [SerializeField] private bool actionBasedTimer = true;
    [SerializeField] private int timer = 2;
    private int turnBasedTimer = 2;

    void Awake()
    {
        var sc = GetComponent<SphereCollider>();
        if (sc && throwArcConfig)
            sc.radius = throwArcConfig.projectileRadiusWU;  // yksi totuus

        // jos haluat varmistaa fallback-käyrän
        if (!throwArcConfig && arcYAnimationCurve == null)
            arcYAnimationCurve = AnimationCurve.Linear(0, 0, 1, 0);

        beaconEffect = GetComponentInChildren<GrenadeBeaconEffect>();
        if (beaconEffect != null)
        {
            beaconEffect.SetTurnsUntilExplosion(timer);
        }
    }
    

    public override void OnStartClient()
    {
        base.OnStartClient();
    }

    bool _subscribed;

    public void Setup(Vector3 targetWorld, float maxThrowRangeWU = -1f)
    {
        _maxRangeWU = (maxThrowRangeWU > 0f) ? maxThrowRangeWU : fallbackMaxThrowRangeWU;
        targetPosition = targetWorld;
        _explosionScheduled = false;
        isExploded = false;

        RecomputeDerived();

        // Tilaa tasan kerran, riippumatta AP/Turn -tilasta
        if (NetMode.ServerOrOff && !_subscribed)
        {
            _subscribed = true;
            if (actionBasedTimer)
            {
                Unit.ActionPointUsed += Unit_ActionPointsUsed;
                TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
            }
            else
            {
                TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
            }
        }

        if (!actionBasedTimer && GameModeManager.SelectedMode == GameMode.CoOp)
        {
            timer *= 2;
        } 
        
        if (actionBasedTimer && GameModeManager.SelectedMode == GameMode.CoOp)
        {
            turnBasedTimer += 1;
        }
    }
    
    private void Unit_ActionPointsUsed(object sender, EventArgs e)
    {

        if (ownerTeamId == -1)
        {
            Debug.LogWarning("No owner");
            return;
        }
        if (_explosionScheduled || isExploded || ownerTeamId == TeamsID.CurrentTurnTeamId())
        {
            return;
        }

        timer -= 1;

        // VISUAALI: server käskee, clientit tekevät
        if (NetworkServer.active) 
        {
            RpcBeaconTick();
        } else if (!NetworkClient.active) 
        {
            // offline
            beaconEffect?.OnTurnAdvanced();
        }

        if (timer > 0) return;

        _explosionScheduled = true;

        if (NetworkServer.active) 
        {
            RpcBeaconArmNow();
            ServerArmExplosion();

        } else if (!NetworkClient.active) 
        {
            beaconEffect?.TriggerFinalCountdown();
            StartCoroutine(LocalExplodeAfterJitter());
        }
    }

    // --- TIMER → SCHEDULING ---
    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        if (_explosionScheduled || isExploded) return;

        turnBasedTimer -= 1;

        // VISUAALI
        if (NetworkServer.active)
        {
            RpcBeaconTick();                    // <<< TEE TÄMÄ
        }
        else if (!NetworkClient.active)
        {
            beaconEffect?.OnTurnAdvanced();
        }
        
        if (turnBasedTimer  > 0) return;

        _explosionScheduled = true;

        if (NetworkServer.active) 
        {
            RpcBeaconArmNow();                  // <<< TEE TÄMÄ
            ServerArmExplosion();
        } else if (!NetworkClient.active) 
        {
            beaconEffect?.TriggerFinalCountdown();
            StartCoroutine(LocalExplodeAfterJitter());
        }
    }

    private void OnDestroy()
    {
        if (!_subscribed) return;
        if (actionBasedTimer)
            Unit.ActionPointUsed -= Unit_ActionPointsUsed;
        else
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        _subscribed = false;
    }

    void OnTargetChanged(Vector3 _old, Vector3 _new)
    {
        // Kun SyncVar saapuu clientille, laske johdetut kentät sielläkin
        RecomputeDerived();
    }

    private void RecomputeDerived()
    {
        _startPos = transform.position;
        _endPos = targetPosition;

        // Per-heitto jitter offlineen
        if (!_randomizedThisThrow)
        {
            if (!NetMode.IsOnline)
            {
                _throwFloorBounceDistWU = UnityEngine.Random.Range(floorBounceDistanceRangeWU.x, floorBounceDistanceRangeWU.y);
                _throwMinFloorBounceDistWU = UnityEngine.Random.Range(minFloorBounceDistanceRangeWU.x, minFloorBounceDistanceRangeWU.y);
                _throwRandomYawDeg = UnityEngine.Random.Range(floorBounceRandomYawRangeDeg.x, floorBounceRandomYawRangeDeg.y);
            }
            else
            {
                _throwFloorBounceDistWU = _throwMinFloorBounceDistWU = _throwRandomYawDeg = 0f;
            }
            _randomizedThisThrow = true;
        }

        float dWU = Vector2.Distance(
            new Vector2(_startPos.x, _startPos.z),
            new Vector2(_endPos.x, _endPos.z));

        int samples = (throwArcConfig != null)
            ? Mathf.Clamp(throwArcConfig.EvaluateSegments(dWU, 2f), 12, 40)
            : Mathf.Clamp(12 + Mathf.RoundToInt(dWU / 2f) * 4, 12, 40);

        float farWU = (_maxRangeWU > 0f) ? _maxRangeWU
                                        : (throwArcConfig ? throwArcConfig.farRangeWU : dWU);

        _apexWU = ArcApexSolver.ComputeCeilingClampedApex(
            _startPos, _endPos, throwArcConfig, ceilingMask,
            ceilingClearance, samples, farWUOverride: farWU);

        // — nopeus & kesto etäisyyden mukaan —
        _currentHorizSpeed = useDistanceScaledSpeed
            ? ComputeDistanceScaledSpeed(dWU)
            : horizontalSpeed;

        _travelDuration = Mathf.Max(minSegmentDuration, dWU / Mathf.Max(0.01f, _currentHorizSpeed));
        _travelT = 0f;

        enabled = true;
    }

    private void Update()
    {
        if (_travelDuration <= 0f) return;

        // perusaskel
        float baseStep = Time.deltaTime / _travelDuration;
        float tNext = _travelT + baseStep;

        // (VALINNAINEN) kevyt huippufiilis kaarikäyrästä
        if (useApexSlowdown) {
            var curve = (throwArcConfig && throwArcConfig.arcYCurve != null) ? throwArcConfig.arcYCurve : arcYAnimationCurve;
            float peak = curve.Evaluate(_travelT);            // 0..1
            float k    = Mathf.Pow(peak, apexSlowExponent);   // korosta huippua
            float slow = Mathf.Lerp(1f, Mathf.Clamp(minSpeedScaleAtApex, 0.1f, 1f), k);
            tNext = _travelT + baseStep * slow;
        }

        _travelT = Mathf.Clamp01(tNext);
        float t = _travelT;

        // kaaren piste
        Vector3 prev = transform.position;
        Vector3 pos  = Vector3.Lerp(_startPos, _endPos, t);
        var useCurve = (throwArcConfig && throwArcConfig.arcYCurve != null) ? throwArcConfig.arcYCurve : arcYAnimationCurve;
        float baselineY = Mathf.Lerp(_startPos.y, _endPos.y, t);
        pos.y = baselineY + useCurve.Evaluate(t) * _apexWU;

        // kimpo
        if (useDeterministicDeflectionOffline) {
            if (TryDeterministicDeflectOffline(prev, pos)) return;
        }

        // siirrä & suuntaa
        Vector3 dir = pos - prev;
        transform.position = pos;
        if (dir.sqrMagnitude > 0.0001f) transform.forward = dir.normalized;

        if (t >= 1f)
        {
            Vector3 p = _endPos;
            float y = p.y;

            // Etsi KORKEIN osumapinta lattia+este -kerroksista
            // (älä ignooraa triggereitä, jos esteet ovat trigger-collidereita)
            var origin = p + Vector3.up * 3f;
            var hits = Physics.RaycastAll(origin, Vector3.down, 12f, floorMask, QueryTriggerInteraction.Collide);

            float highest = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++) {
                // jos haluat varmistaa, ettei “seisota itseään”, ohita oma collider
                if (TryGetComponent<Collider>(out var selfCol) && hits[i].collider == selfCol) 
                    continue;
                if (hits[i].point.y > highest) highest = hits[i].point.y;
            }
            if (highest > float.NegativeInfinity) y = highest;

            // Nosta kranaatti pintaan: oma puoli-korkeus + pieni epsilon, ettei klippaa
            float half = 0f;
            if (TryGetComponent<Collider>(out var col)) half = col.bounds.extents.y;
            y += half + 0.005f;

            // Aseta lopullinen laskeutumispaikka
            var landed = new Vector3(p.x, y, p.z);
            transform.position = landed;

            // Jos räjähdys/efekti käyttää _endPos:ia, pidä ne synkassa:
            _endPos = landed;

            enabled = false;
        }
    }

    private bool TryDeterministicDeflectOffline(Vector3 prevPos, Vector3 nextPos)
    {
        // 0) Pomppuraja
        if (_bounces >= maxBounces) return false;

        float stepDist = Vector3.Distance(prevPos, nextPos);
        if (stepDist <= 1e-6f) return false;

        // 1) SphereCast reitin suuntaan (KÄYTÄ deflectionMaskia, jossa lattia on mukana)
        float radius = 0.12f;
        if (TryGetComponent<SphereCollider>(out var sc)) radius = sc.radius;
        else if (throwArcConfig) radius = throwArcConfig.projectileRadiusWU;

        Vector3 stepDir = (nextPos - prevPos).normalized;

        if (!Physics.SphereCast(prevPos, radius, stepDir, out var hit, stepDist, deflectionMask, QueryTriggerInteraction.Ignore))
            return false;

        // 2) Estä ping-pong samaan collideriin heti perään
        if (hit.collider == _lastHitCollider && (Time.time - _lastHitTime) < minBounceSeparation)
            return false;

        // 3) Hyväksy VAIN lattia (normaalin Y iso)
        if (hit.normal.y < floorOnlyMinNormalY)
            return false;

        // 4) Heijastussuunta XZ-tasossa + deterministinen satunnainen kääntö
        Vector3 preDir = stepDir;
        Vector3 vRef   = Vector3.Reflect(preDir, hit.normal);

        vRef.y = 0f; preDir.y = 0f;
        if (vRef.sqrMagnitude < 1e-6f) vRef = preDir;
        vRef.Normalize(); preDir.Normalize();

        float randomYaw = (_throwRandomYawDeg > 0f)
            ? UnityEngine.Random.Range(-_throwRandomYawDeg, _throwRandomYawDeg)
            : 0f;
        vRef = Quaternion.AngleAxis(randomYaw, Vector3.up) * vRef;
        vRef.Normalize();

        // Käännä hieman kohti alkuperäistä maalia (aste-rajalla)
        Vector3 toGoal = _endPos - hit.point; toGoal.y = 0f;
        if (toGoal.sqrMagnitude > 1e-6f)
        {
            toGoal.Normalize();
            float delta = Vector3.SignedAngle(vRef, toGoal, Vector3.up);
            float yaw   = Mathf.Clamp(delta, -floorBounceYawTowardGoalDeg, floorBounceYawTowardGoalDeg);
            vRef = Quaternion.AngleAxis(yaw, Vector3.up) * vRef;
            vRef.Normalize();
        }

        // 5) Pituus: pysy ruudussa -> käytä per-heitto min/max-arvoa
        float remWU = Mathf.Max(_throwMinFloorBounceDistWU, _throwFloorBounceDistWU);

        // 6) Uusi päätepiste XZ:ssä
        Vector3 candidateEnd = hit.point + vRef * remWU;

        // Lattiakorkeus alas-raylla → oikea kerros
        float endY = candidateEnd.y;
        if (Physics.Raycast(candidateEnd + Vector3.up * 2f, Vector3.down, out var hitFloor, 10f, floorMask, QueryTriggerInteraction.Ignore))
            endY = hitFloor.point.y;
        candidateEnd.y = endY;

        // 7) Aseta uusi segmentti (pieni “irrotus” pinnasta)
        _startPos = hit.point + vRef * pushOffWU;
        _endPos   = candidateEnd;

        // 8) Uuden segmentin apex ja kesto (pidä kaari matalana pompussa)
        float dWU = Vector2.Distance(
            new Vector2(_startPos.x, _startPos.z),
            new Vector2(_endPos.x,   _endPos.z));

        int samples = (throwArcConfig != null)
            ? Mathf.Clamp(throwArcConfig.EvaluateSegments(dWU, 2f), 12, 40)
            : Mathf.Clamp(12 + Mathf.RoundToInt(dWU / 2f) * 4, 12, 40);

        float farWU = (_maxRangeWU > 0f) ? _maxRangeWU
                                        : (throwArcConfig != null ? throwArcConfig.farRangeWU : dWU);

        _apexWU = ArcApexSolver.ComputeCeilingClampedApex(
            _startPos, _endPos, throwArcConfig, ceilingMask,
            ceilingClearance, samples, farWUOverride: farWU
        );

        // (POISTETTU: ComputeArcPeakOut – arvoja ei käytetä tässä)
        
        // Hillitse pompun kaaren korkeutta
        float nominalFloorApex = (throwArcConfig != null)
            ? throwArcConfig.EvaluateApex(Mathf.Max(dWU, apexEvalDistanceFloorWU), farWU)
            : _apexWU;

        _apexWU  = Mathf.Min(_apexWU, nominalFloorApex);
        _apexWU *= Mathf.Clamp01(apexBounceScale);
        _apexWU  = Mathf.Min(_apexWU, maxBounceApexWU);

        // 9) Kesto & resetit
        // CHANGE: vaimenna nykyistä vaakanopeutta pompun jälkeen ja käytä sitä keston laskennassa
        _currentHorizSpeed = Mathf.Max(0.01f, _currentHorizSpeed * Mathf.Clamp01(bounceSpeedScale));

        float minDur = (_bounces == 0) ? minSegmentDuration : Mathf.Max(0.12f, 0.75f * minSegmentDuration);
        _travelDuration = Mathf.Clamp(dWU / _currentHorizSpeed, minDur, maxSegmentDuration);
        _travelT = 0f;

        // Visuaalisesti osumapisteestä eteenpäin
        transform.position = _startPos;
        transform.forward  = vRef;

        // 10) Kirjanpito
        _bounces++;
        _lastHitCollider = hit.collider;
        _lastHitTime     = Time.time;

        return true;
    }

    // Saattaa olla ihan hyödyllinen joskus tulevaisuudessa.
    private void ComputeArcPeakOut(out float apexWorldY, out float tPeak)
    {
        var curve = (throwArcConfig && throwArcConfig.arcYCurve != null)
            ? throwArcConfig.arcYCurve
            : arcYAnimationCurve;

        // Etsi kaaren maksimi arvo ja sitä vastaava t. (näytepohjainen, riittävän kevyt)
        float maxV = float.NegativeInfinity;
        float bestT = 0.5f;
        const int N = 20;
        for (int i = 0; i <= N; i++)
        {
            float t = i / (float)N;
            float v = curve.Evaluate(t);
            if (v > maxV) { maxV = v; bestT = t; }
        }

        // BaselineY huipulla + suhteellinen huippukorkeus * _apexWU
        float baselineY = Mathf.Lerp(_startPos.y, _endPos.y, bestT);
        apexWorldY = baselineY + maxV * _apexWU;
        tPeak = bestT;
    }

    private float ComputeDistanceScaledSpeed(float dWU)
    {
        // Muunna ruuduiksi (1 ruutu minimi, ettei 0-alue hyydytä)
        float tiles = dWU / Mathf.Max(1e-3f, tileSizeWU);
        tiles = Mathf.Max(1f, tiles);

        // Lineaarinen malli: v = v(1 tile) + perTile * (tiles - 1)
        float v = speedAtOneTile + speedPerTile * (tiles - 1f);

        // Pinni
        return Mathf.Clamp(v, speedMinClamp, speedMaxClamp);
    }

    // --- OFFLINE LOCAL EXPLOSION ---
    private IEnumerator LocalExplodeAfterJitter()
    {
        float delay = UnityEngine.Random.Range(explosionJitterMin, explosionJitterMax);
        yield return new WaitForSeconds(delay);
        LocalExplode();
    }

    void ExplodeAt(GridPosition center, float damageRadiusWU)
    {
        int rTiles = Mathf.CeilToInt(damageRadiusWU / LevelGrid.Instance.GetCellSize());
        var tiles   = ExplosionSolver.ComputeReach(center, rTiles);
 

        // 1) Kerää kohteet reach-ruuduista (vältä duplikaatit)
        var targets = new HashSet<Unit>();
        foreach (var gp in tiles)
        {
            var list = LevelGrid.Instance.GetUnitListAtGridPosition(gp);
            if (list == null || list.Count == 0) continue;
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                if (u) targets.Add(u);
            }
        }
        var objects = new HashSet<DestructibleObject>();
        foreach (var gp in tiles)
        {
            var list = LevelGrid.Instance.GetDestructibleListAtGridPosition(gp);
            if (list == null || list.Count == 0) continue;
            for (int i = 0; i < list.Count; i++)
            {
                var @object = list[i];
                if (@object) objects.Add(@object);
            }
        }


        // 2) Sovella damage vasta nyt (lista ei muutu kesken keruun)
        foreach (var u in targets)
        {
            NetworkSync.ApplyDamageToUnit(u, damage, targetPosition, this.GetActorId());
        }
        foreach (var o in objects)
        {
            NetworkSync.ApplyDamageToObject(o, damage, targetPosition, this.GetActorId());
        }
    }

    private void LocalExplode()
    {
        if (isExploded) return;
        isExploded = true;

        var targetGridPosition = LevelGrid.Instance.GetGridPosition(targetPosition);
        ExplodeAt(targetGridPosition, damageRadius);

        // AUDIO
        PlayExplosionSound(targetPosition);

        // VFX paikallisesti
        OnAnyGranadeExploded?.Invoke(this, EventArgs.Empty);
        SpawnRouter.SpawnLocal(
            grenadeExplodeVFXPrefab.gameObject,
            targetPosition + Vector3.up * 1f,
            Quaternion.identity,
            source: transform
        );

        Destroy(gameObject);
    }

    [Server]
    private IEnumerator ServerExplodeAt(double explodeAtServerTime)
    {
        var targetGridPosition = LevelGrid.Instance.GetGridPosition(targetPosition);
        
        // Odota tarkkaan serverkellon mukaan
        float wait = Mathf.Max(0f, (float)(explodeAtServerTime - NetworkTime.time));
        if (wait > 0f) yield return new WaitForSeconds(wait);

        if (_damageDone) yield break;
        _damageDone = true;
        if (isExploded) yield break;
        isExploded = true;

        ExplodeAt(targetGridPosition, damageRadius);
        
        // Piilota itse kranaatti kaikilta
        SetSoftHiddenLocal(true);
        RpcSetSoftHidden(true);

        // Server voi nyt siivota objektin pienen viiveen jälkeen
        StartCoroutine(DestroyAfter(0.30f));
    }

    private IEnumerator SpawnVFXAt(string sceneName, Vector3 pos, double explodeAtServerTime)
    {
        float wait = Mathf.Max(0f, (float)(explodeAtServerTime - NetworkTime.time));
        if (wait > 0f) yield return new WaitForSeconds(wait);

        // AUDIO
        PlayExplosionSound(pos);

        // Vain visuaali — ei peli-impactia clienteillä
        OnAnyGranadeExploded?.Invoke(this, EventArgs.Empty);
        SpawnRouter.SpawnLocal(
            grenadeExplodeVFXPrefab.gameObject,
            pos + Vector3.up * 1f,
            Quaternion.identity,
            source: null,
            sceneName: sceneName
        );
    }
    
    private void PlayExplosionSound(Vector3 position)
    {
        if (explosionSounds == null || explosionSounds.Length == 0) return;

        AudioClip clip = explosionSounds[UnityEngine.Random.Range(0, explosionSounds.Length)];
        if (clip == null) return;

        GameObject audioGO = new GameObject("ExplosionAudio_Temp");
        audioGO.transform.position = position;

        AudioSource source = audioGO.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = explosionVolume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.maxDistance = explosionMaxHearingDistance;
        source.minDistance = 5f;
        source.dopplerLevel = 0f;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, explosionVolumeRolloff);

        source.Play();

        Destroy(audioGO, clip.length + 0.1f);
    }



    [ClientRpc]
    private void RpcBeaconTick()
    {
        if (!beaconEffect) beaconEffect = GetComponentInChildren<GrenadeBeaconEffect>(true);
        beaconEffect?.OnTurnAdvanced();
    }

    [ClientRpc]
    private void RpcBeaconArmNow()
    {
        if (!beaconEffect) beaconEffect = GetComponentInChildren<GrenadeBeaconEffect>(true);
        beaconEffect?.TriggerFinalCountdown();
    }

    [Server]
    private void ServerArmExplosion()
    {
        // Jitter päälle (server päättää)
        float jitter = UnityEngine.Random.Range(explosionJitterMin, explosionJitterMax);
        _explodeAt = NetworkTime.time + jitter;

        // Kerro kaikille clienteille (hostin local client mukaan lukien)
        RpcArmExplosion(gameObject.scene.name, targetPosition, _explodeAt);

        // Server odottaa samaan serveriaikaan ja tekee vahingot
        StartCoroutine(ServerExplodeAt(_explodeAt));
    }

    [ClientRpc]
    private void RpcArmExplosion(string sceneName, Vector3 pos, double explodeAtServerTime)
    {
        if (_armed) return; // varmistus
        _armed = true;
        _explodeAt = explodeAtServerTime;

        // Ajasta paikallinen VFX täsmälleen samaan aikaan serverin kellossa
        StartCoroutine(SpawnVFXAt(sceneName, pos, explodeAtServerTime));
    }

    private IEnumerator DestroyAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (NetworkServer.active)    // online-server
            NetworkServer.Destroy(gameObject);
        else                         // offline
            Destroy(gameObject);
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
