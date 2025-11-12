using System;
using UnityEngine;
using Mirror;
using System.Collections;

public abstract class BaseGrenadeProjectile : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField] protected GrenadeDefinition grenadeDefinition;
    
    [Header("Arc Configuration")]
    [SerializeField] protected ThrowArcConfig throwArcConfig;
    [SerializeField] protected LayerMask ceilingMask;
    [SerializeField] protected LayerMask floorMask;
    [SerializeField] protected float ceilingClearance = 0.08f;
    [SerializeField] protected float fallbackMaxThrowRangeWU = 12f;
    [SerializeField] protected float horizontalSpeed = 10f;
    
    [Header("Deflection: Toggle & Mask")]
    [Tooltip("Kytkee deterministisen kimpoilun päälle vain offline-tilassa")]
    [SerializeField] protected bool useDeterministicDeflectionOffline = false;
    [SerializeField] protected LayerMask deflectionMask;
    
    [Header("Floor Bounce Randomization (OFFLINE visuals only)")]
    [SerializeField] protected Vector2 floorBounceDistanceRangeWU = new Vector2(0.1f, 0.35f);
    [SerializeField] protected Vector2 minFloorBounceDistanceRangeWU = new Vector2(0.01f, 0.15f);
    [SerializeField] protected Vector2 floorBounceRandomYawRangeDeg = new Vector2(0f, 35f);
    
    [Header("Deflection: Bounce Count & Distance")]
    [SerializeField] protected int maxBounces = 2;
    
    [Header("Deflection: Timing & Speed")]
    [SerializeField] protected float minSegmentDuration = 0.25f;
    [SerializeField] protected float maxSegmentDuration = 0.55f;
    [SerializeField, Range(0f, 1f)] protected float bounceSpeedScale = 0.75f;
    
    [Header("Deflection: Arc Shaping (Bounce Apex)")]
    [SerializeField, Range(0f, 1f)] protected float apexBounceScale = 0.2f;
    [SerializeField] protected float maxBounceApexWU = 0.2f;
    [SerializeField] protected float apexEvalDistanceFloorWU = 4f;
    
    [Header("Deflection: Direction Control")]
    [SerializeField] protected float minBounceSeparation = 0.06f;
    [SerializeField] protected float pushOffWU = 0.02f;
    [SerializeField, Range(0.6f, 0.99f)] protected float floorOnlyMinNormalY = 0.7f;
    [SerializeField, Range(0f, 25f)] protected float floorBounceYawTowardGoalDeg = 0f;
    
    [Header("Flight Feel (time-warp around apex)")]
    [SerializeField, Range(0.1f, 1f)] protected float minSpeedScaleAtApex = 0.8f;
    [SerializeField, Range(0.5f, 5f)] protected float apexSlowExponent = 4.0f;
    [SerializeField] protected bool useApexSlowdown = true;
    
    [Header("Distance-Scaled Speed")]
    [SerializeField] protected bool useDistanceScaledSpeed = true;
    [SerializeField] protected float speedAtOneTile = 2f;
    [SerializeField] protected float speedPerTile = 2.5f;
    [SerializeField] protected float speedMinClamp = 1.5f;
    [SerializeField] protected float speedMaxClamp = 14f;
    [SerializeField] protected float tileSizeWU = 2f;
    
    [Header("Curve Fallback")]
    [SerializeField] protected AnimationCurve arcYAnimationCurve;
    
    [SyncVar] public uint actorUnitNetId;
    [SyncVar] public uint ownerPlayerNetId;
    [SyncVar] public int ownerTeamId = -1;
    [SyncVar(hook = nameof(OnTargetChanged))] protected Vector3 targetPosition;
    
    public static event EventHandler OnAnyGranadeExploded;
    
    protected Vector3 _startPos;
    protected Vector3 _endPos;
    protected float _apexWU;
    protected float _travelT;
    protected float _travelDuration;
    protected float _maxRangeWU;
    protected float _currentHorizSpeed;
    
    protected bool isExploded = false;
    protected bool _explosionScheduled;
    protected bool _armed;
    protected double _explodeAt;
    protected bool _damageDone;
    
    protected float _throwFloorBounceDistWU;
    protected float _throwMinFloorBounceDistWU;
    protected float _throwRandomYawDeg;
    protected bool _randomizedThisThrow;
    
    protected int _bounces;
    protected Collider _lastHitCollider;
    protected float _lastHitTime;
    
    protected GrenadeBeaconEffect beaconEffect;
    protected bool _subscribed;
    protected int timer;
    protected int turnBasedTimer;
    
    protected virtual void Awake()
    {
        var sc = GetComponent<SphereCollider>();
        if (sc && throwArcConfig)
            sc.radius = throwArcConfig.projectileRadiusWU;
        
        if (!throwArcConfig && arcYAnimationCurve == null)
            arcYAnimationCurve = AnimationCurve.Linear(0, 0, 1, 0);
        
        beaconEffect = GetComponentInChildren<GrenadeBeaconEffect>();
        
        if (grenadeDefinition != null)
        {
            timer = grenadeDefinition.timer;
            turnBasedTimer = grenadeDefinition.timer;
            
            if (beaconEffect != null)
                beaconEffect.SetTurnsUntilExplosion(timer);
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
    }
    
    public virtual void Setup(Vector3 targetWorld, float maxThrowRangeWU = -1f)
    {
        _maxRangeWU = (maxThrowRangeWU > 0f) ? maxThrowRangeWU : fallbackMaxThrowRangeWU;
        targetPosition = targetWorld;
        _explosionScheduled = false;
        isExploded = false;
        
        RecomputeDerived();
        
        if (NetMode.ServerOrOff && !_subscribed && grenadeDefinition != null)
        {
            _subscribed = true;
            if (grenadeDefinition.actionBasedTimer)
            {
                Unit.ActionPointUsed += Unit_ActionPointsUsed;
                TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
            }
            else
            {
                TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
            }
        }
        
        if (grenadeDefinition != null)
        {
            if (!grenadeDefinition.actionBasedTimer && GameModeManager.SelectedMode == GameMode.CoOp)
                timer *= 2;
            
            if (grenadeDefinition.actionBasedTimer && GameModeManager.SelectedMode == GameMode.CoOp)
                turnBasedTimer += 1;
        }
    }
    
    protected virtual void Unit_ActionPointsUsed(object sender, EventArgs e)
    {
        if (ownerTeamId == -1 || _explosionScheduled || isExploded || ownerTeamId == TeamsID.CurrentTurnTeamId())
            return;
        
        timer -= 1;
        
        if (NetworkServer.active)
            RpcBeaconTick();
        else if (!NetworkClient.active)
            beaconEffect?.OnTurnAdvanced();
        
        if (timer > 0) return;
        
        _explosionScheduled = true;
        
        if (NetworkServer.active)
        {
            RpcBeaconArmNow();
            ServerArmExplosion();
        }
        else if (!NetworkClient.active)
        {
            beaconEffect?.TriggerFinalCountdown();
            StartCoroutine(LocalExplodeAfterJitter());
        }
    }
    
    protected virtual void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        if (_explosionScheduled || isExploded) return;
        
        turnBasedTimer -= 1;
        
        if (NetworkServer.active)
            RpcBeaconTick();
        else if (!NetworkClient.active)
            beaconEffect?.OnTurnAdvanced();
        
        if (turnBasedTimer > 0) return;
        
        _explosionScheduled = true;
        
        if (NetworkServer.active)
        {
            RpcBeaconArmNow();
            ServerArmExplosion();
        }
        else if (!NetworkClient.active)
        {
            beaconEffect?.TriggerFinalCountdown();
            StartCoroutine(LocalExplodeAfterJitter());
        }
    }
    
    protected virtual void OnDestroy()
    {
        if (!_subscribed || grenadeDefinition == null) return;
        
        if (grenadeDefinition.actionBasedTimer)
            Unit.ActionPointUsed -= Unit_ActionPointsUsed;
        else
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        
        _subscribed = false;
    }
    
    protected virtual void OnTargetChanged(Vector3 _old, Vector3 _new)
    {
        RecomputeDerived();
    }
    
    protected virtual void RecomputeDerived()
    {
        _startPos = transform.position;
        _endPos = targetPosition;
        
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
        
        _currentHorizSpeed = useDistanceScaledSpeed
            ? ComputeDistanceScaledSpeed(dWU)
            : horizontalSpeed;
        
        _travelDuration = Mathf.Max(minSegmentDuration, dWU / Mathf.Max(0.01f, _currentHorizSpeed));
        _travelT = 0f;
        
        enabled = true;
    }
    
    protected virtual void Update()
    {
        if (_travelDuration <= 0f) return;
        
        float baseStep = Time.deltaTime / _travelDuration;
        float tNext = _travelT + baseStep;
        
        if (useApexSlowdown)
        {
            var curve = (throwArcConfig && throwArcConfig.arcYCurve != null) ? throwArcConfig.arcYCurve : arcYAnimationCurve;
            float peak = curve.Evaluate(_travelT);
            float k = Mathf.Pow(peak, apexSlowExponent);
            float slow = Mathf.Lerp(1f, Mathf.Clamp(minSpeedScaleAtApex, 0.1f, 1f), k);
            tNext = _travelT + baseStep * slow;
        }
        
        _travelT = Mathf.Clamp01(tNext);
        float t = _travelT;
        
        Vector3 prev = transform.position;
        Vector3 pos = Vector3.Lerp(_startPos, _endPos, t);
        var useCurve = (throwArcConfig && throwArcConfig.arcYCurve != null) ? throwArcConfig.arcYCurve : arcYAnimationCurve;
        float baselineY = Mathf.Lerp(_startPos.y, _endPos.y, t);
        pos.y = baselineY + useCurve.Evaluate(t) * _apexWU;
        
        if (useDeterministicDeflectionOffline)
        {
            if (TryDeterministicDeflectOffline(prev, pos)) return;
        }
        
        Vector3 dir = pos - prev;
        transform.position = pos;
        if (dir.sqrMagnitude > 0.0001f) transform.forward = dir.normalized;
        
        if (t >= 1f)
        {
            Vector3 p = _endPos;
            float y = p.y;
            
            var origin = p + Vector3.up * 3f;
            var hits = Physics.RaycastAll(origin, Vector3.down, 12f, floorMask, QueryTriggerInteraction.Collide);
            
            float highest = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                if (TryGetComponent<Collider>(out var selfCol) && hits[i].collider == selfCol)
                    continue;
                if (hits[i].point.y > highest) highest = hits[i].point.y;
            }
            if (highest > float.NegativeInfinity) y = highest;
            
            float half = 0f;
            if (TryGetComponent<Collider>(out var col)) half = col.bounds.extents.y;
            y += half + 0.005f;
            
            var landed = new Vector3(p.x, y, p.z);
            transform.position = landed;
            _endPos = landed;
            
            enabled = false;
        }
    }
    
    protected virtual bool TryDeterministicDeflectOffline(Vector3 prevPos, Vector3 nextPos)
    {
        if (_bounces >= maxBounces) return false;
        
        float stepDist = Vector3.Distance(prevPos, nextPos);
        if (stepDist <= 1e-6f) return false;
        
        float radius = 0.12f;
        if (TryGetComponent<SphereCollider>(out var sc)) radius = sc.radius;
        else if (throwArcConfig) radius = throwArcConfig.projectileRadiusWU;
        
        Vector3 stepDir = (nextPos - prevPos).normalized;
        
        if (!Physics.SphereCast(prevPos, radius, stepDir, out var hit, stepDist, deflectionMask, QueryTriggerInteraction.Ignore))
            return false;
        
        if (hit.collider == _lastHitCollider && (Time.time - _lastHitTime) < minBounceSeparation)
            return false;
        
        if (hit.normal.y < floorOnlyMinNormalY)
            return false;
        
        Vector3 preDir = stepDir;
        Vector3 vRef = Vector3.Reflect(preDir, hit.normal);
        
        vRef.y = 0f; preDir.y = 0f;
        if (vRef.sqrMagnitude < 1e-6f) vRef = preDir;
        vRef.Normalize(); preDir.Normalize();
        
        float randomYaw = (_throwRandomYawDeg > 0f)
            ? UnityEngine.Random.Range(-_throwRandomYawDeg, _throwRandomYawDeg)
            : 0f;
        vRef = Quaternion.AngleAxis(randomYaw, Vector3.up) * vRef;
        vRef.Normalize();
        
        Vector3 toGoal = _endPos - hit.point; toGoal.y = 0f;
        if (toGoal.sqrMagnitude > 1e-6f)
        {
            toGoal.Normalize();
            float delta = Vector3.SignedAngle(vRef, toGoal, Vector3.up);
            float yaw = Mathf.Clamp(delta, -floorBounceYawTowardGoalDeg, floorBounceYawTowardGoalDeg);
            vRef = Quaternion.AngleAxis(yaw, Vector3.up) * vRef;
            vRef.Normalize();
        }
        
        float remWU = Mathf.Max(_throwMinFloorBounceDistWU, _throwFloorBounceDistWU);
        Vector3 candidateEnd = hit.point + vRef * remWU;
        
        float endY = candidateEnd.y;
        if (Physics.Raycast(candidateEnd + Vector3.up * 2f, Vector3.down, out var hitFloor, 10f, floorMask, QueryTriggerInteraction.Ignore))
            endY = hitFloor.point.y;
        candidateEnd.y = endY;
        
        _startPos = hit.point + vRef * pushOffWU;
        _endPos = candidateEnd;
        
        float dWU = Vector2.Distance(
            new Vector2(_startPos.x, _startPos.z),
            new Vector2(_endPos.x, _endPos.z));
        
        int samples = (throwArcConfig != null)
            ? Mathf.Clamp(throwArcConfig.EvaluateSegments(dWU, 2f), 12, 40)
            : Mathf.Clamp(12 + Mathf.RoundToInt(dWU / 2f) * 4, 12, 40);
        
        float farWU = (_maxRangeWU > 0f) ? _maxRangeWU
                                        : (throwArcConfig != null ? throwArcConfig.farRangeWU : dWU);
        
        _apexWU = ArcApexSolver.ComputeCeilingClampedApex(
            _startPos, _endPos, throwArcConfig, ceilingMask,
            ceilingClearance, samples, farWUOverride: farWU
        );
        
        float nominalFloorApex = (throwArcConfig != null)
            ? throwArcConfig.EvaluateApex(Mathf.Max(dWU, apexEvalDistanceFloorWU), farWU)
            : _apexWU;
        
        _apexWU = Mathf.Min(_apexWU, nominalFloorApex);
        _apexWU *= Mathf.Clamp01(apexBounceScale);
        _apexWU = Mathf.Min(_apexWU, maxBounceApexWU);
        
        _currentHorizSpeed = Mathf.Max(0.01f, _currentHorizSpeed * Mathf.Clamp01(bounceSpeedScale));
        
        float minDur = (_bounces == 0) ? minSegmentDuration : Mathf.Max(0.12f, 0.75f * minSegmentDuration);
        _travelDuration = Mathf.Clamp(dWU / _currentHorizSpeed, minDur, maxSegmentDuration);
        _travelT = 0f;
        
        transform.position = _startPos;
        transform.forward = vRef;
        
        _bounces++;
        _lastHitCollider = hit.collider;
        _lastHitTime = Time.time;
        
        return true;
    }
    
    protected float ComputeDistanceScaledSpeed(float dWU)
    {
        float tiles = dWU / Mathf.Max(1e-3f, tileSizeWU);
        tiles = Mathf.Max(1f, tiles);
        float v = speedAtOneTile + speedPerTile * (tiles - 1f);
        return Mathf.Clamp(v, speedMinClamp, speedMaxClamp);
    }
    
    protected IEnumerator LocalExplodeAfterJitter()
    {
        if (grenadeDefinition == null) yield break;
        
        float delay = UnityEngine.Random.Range(grenadeDefinition.explosionJitterMin, grenadeDefinition.explosionJitterMax);
        yield return new WaitForSeconds(delay);
        LocalExplode();
    }
    
    protected void PlayExplosionSound(Vector3 position)
    {
        if (grenadeDefinition == null || grenadeDefinition.explosionSounds == null || grenadeDefinition.explosionSounds.Length == 0) 
            return;
        
        AudioClip clip = grenadeDefinition.explosionSounds[UnityEngine.Random.Range(0, grenadeDefinition.explosionSounds.Length)];
        if (clip == null) return;
        
        GameObject audioGO = new GameObject("ExplosionAudio_Temp");
        audioGO.transform.position = position;
        
        AudioSource source = audioGO.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = grenadeDefinition.explosionVolume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.maxDistance = grenadeDefinition.explosionMaxHearingDistance;
        source.minDistance = 5f;
        source.dopplerLevel = 0f;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, grenadeDefinition.explosionVolumeRolloff);
        
        source.Play();
        Destroy(audioGO, clip.length + 0.1f);
    }
    
    [ClientRpc]
    protected void RpcBeaconTick()
    {
        if (!beaconEffect) beaconEffect = GetComponentInChildren<GrenadeBeaconEffect>(true);
        beaconEffect?.OnTurnAdvanced();
    }
    
    [ClientRpc]
    protected void RpcBeaconArmNow()
    {
        if (!beaconEffect) beaconEffect = GetComponentInChildren<GrenadeBeaconEffect>(true);
        beaconEffect?.TriggerFinalCountdown();
    }
    
    [Server]
    protected void ServerArmExplosion()
    {
        if (grenadeDefinition == null) return;
        
        float jitter = UnityEngine.Random.Range(grenadeDefinition.explosionJitterMin, grenadeDefinition.explosionJitterMax);
        _explodeAt = NetworkTime.time + jitter;
        
        RpcArmExplosion(gameObject.scene.name, targetPosition, _explodeAt);
        StartCoroutine(ServerExplodeAt(_explodeAt));
    }
    
    [ClientRpc]
    protected void RpcArmExplosion(string sceneName, Vector3 pos, double explodeAtServerTime)
    {
        if (_armed) return;
        _armed = true;
        _explodeAt = explodeAtServerTime;
        
        StartCoroutine(SpawnVFXAt(sceneName, pos, explodeAtServerTime));
    }
    
    [Server]
    protected IEnumerator ServerExplodeAt(double explodeAtServerTime)
    {
        var targetGridPosition = LevelGrid.Instance.GetGridPosition(targetPosition);
        
        float wait = Mathf.Max(0f, (float)(explodeAtServerTime - NetworkTime.time));
        if (wait > 0f) yield return new WaitForSeconds(wait);
        
        if (_damageDone) yield break;
        _damageDone = true;
        if (isExploded) yield break;
        isExploded = true;
        
        OnExplode(targetGridPosition);
        
        SetSoftHiddenLocal(true);
        RpcSetSoftHidden(true);
        
        StartCoroutine(DestroyAfter(0.30f));
    }
    
    protected IEnumerator SpawnVFXAt(string sceneName, Vector3 pos, double explodeAtServerTime)
    {
        float wait = Mathf.Max(0f, (float)(explodeAtServerTime - NetworkTime.time));
        if (wait > 0f) yield return new WaitForSeconds(wait);
        
        PlayExplosionSound(pos);
        OnAnyGranadeExploded?.Invoke(this, EventArgs.Empty);
        
        if (grenadeDefinition != null && grenadeDefinition.explosionVFXPrefab != null)
        {
            SpawnRouter.SpawnLocal(
                grenadeDefinition.explosionVFXPrefab.gameObject,
                pos + Vector3.up * 1f,
                Quaternion.identity,
                source: null,
                sceneName: sceneName
            );
        }
    }
    
    protected void LocalExplode()
    {
        if (isExploded) return;
        isExploded = true;
        
        var targetGridPosition = LevelGrid.Instance.GetGridPosition(targetPosition);
        OnExplode(targetGridPosition);
        
        PlayExplosionSound(targetPosition);
        OnAnyGranadeExploded?.Invoke(this, EventArgs.Empty);
        
        if (grenadeDefinition != null && grenadeDefinition.explosionVFXPrefab != null)
        {
            SpawnRouter.SpawnLocal(
                grenadeDefinition.explosionVFXPrefab.gameObject,
                targetPosition + Vector3.up * 1f,
                Quaternion.identity,
                source: transform
            );
        }
        
        Destroy(gameObject);
    }
    
    [ClientRpc]
    protected void RpcSetSoftHidden(bool hidden)
    {
        SetSoftHiddenLocal(hidden);
    }
    
    protected void SetSoftHiddenLocal(bool hidden)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = !hidden;
        }
    }
    
    protected IEnumerator DestroyAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        
        if (NetworkServer.active)
            NetworkServer.Destroy(gameObject);
        else
            Destroy(gameObject);
    }
    
    public uint GetActorId() => actorUnitNetId;
    
    protected abstract void OnExplode(GridPosition explosionCenter);
}
