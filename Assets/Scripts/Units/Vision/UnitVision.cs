using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class UnitVision : MonoBehaviour
{
    [Header("Config")]
    public UnitArchetype visionSkill;
    public int teamId = 0;

    private Unit _unit;
    private Transform _tr;
    private HashSet<GridPosition> _lastVisible = new();
    private int _unitKey;
    private bool _initialized = false;
    private int _currentTeamId = 0;

    void Awake()
    {
        _tr = transform;
        _unit = GetComponent<Unit>();
        _unitKey = GetInstanceID();
    }

    void Start()
    {
        if (!_initialized)
            StartCoroutine(Co_AutoInitLocal());
    }

    void OnDisable()
    {
        if (TeamVisionService.Instance != null)
        {
            TeamVisionService.Instance.RemoveUnitVision(teamId, _unitKey);
        }
        _lastVisible.Clear();
    }

    private System.Collections.IEnumerator Co_AutoInitLocal()
    {
        Debug.Log($"[UnitVision] Aloitetaan automaattinen initialisointi: {name}");
        yield return null;

        if (_initialized) yield break;
        if (NetMode.IsDedicatedServer) yield break;

        // Yritä alustaa useamman framen ajan (Mirror tarvitsee aikaa synkronoida ownership)
        for (int attempt = 0; attempt < 30; attempt++)
        {

            int? team = _unit.GetTeamId();

            if (team != null)
            {
                if (_unit == null) _unit = GetComponent<Unit>();
                InitializeVision(team.Value, _unit ? _unit.archetype : visionSkill);
                yield break;
            }

            yield return null;
        }
    }

    public void InitializeVision(int setTeamId, UnitArchetype archetype)
    {
        if (_initialized && setTeamId != _currentTeamId && TeamVisionService.Instance != null)
            TeamVisionService.Instance.RemoveUnitVision(_currentTeamId, _unitKey);

        _currentTeamId = setTeamId;
        teamId = setTeamId;
        if (archetype != null) visionSkill = archetype;

        _initialized = true;

        StartCoroutine(Co_DeferredFirstVision());
    }

    private System.Collections.IEnumerator Co_DeferredFirstVision()
    {
        yield return null;
        UpdateVisionNow();
    }

    public void NotifyMoved()
    {
        UpdateVisionNow();
    }

    public bool IsInitialized => _initialized;

    public void UpdateVisionNow()
    {
        if (!ShouldPublishVisionLocally())
            return;

        if (!_initialized)
        {
            Debug.LogWarning($"[UnitVision UPDATE SKIP] {name} - not initialized");
            return;
        }

        if (visionSkill == null)
        {
            Debug.LogWarning($"[UnitVision UPDATE SKIP] {name} - no visionSkill");
            return;
        }

        var lg = LevelGrid.Instance;
        var cfg = LoSConfig.Instance;
        var tvs = TeamVisionService.Instance;

        if (lg == null || cfg == null || tvs == null)
        {
            Debug.LogWarning($"[UnitVision UPDATE SKIP] {name} - missing services: LG={lg != null}, CFG={cfg != null}, TVS={tvs != null}");
            return;
        }

        var wp = _tr != null ? _tr.position : transform.position;
        var origin = lg.GetGridPosition(wp);

        if (_unit != null)
        {
            var uf = _unit.GetGridPosition();
            origin = new GridPosition(origin.x, origin.z, uf.floor);
        }

        HashSet<GridPosition> vis;
        if (visionSkill.useHeightAware)
        {
            vis = RaycastVisibility.ComputeVisibleTilesRaycastHeightAware(
                origin, visionSkill.visionRange,
                cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU,
                ignoreRoot: _tr
            );
        }
        else
        {
            vis = RaycastVisibility.ComputeVisibleTilesRaycast(
                origin, visionSkill.visionRange,
                cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU
            );
        }

        _lastVisible = vis ?? _lastVisible;

        tvs.ReplaceUnitVision(teamId, _unitKey, _lastVisible);
    }

    private bool ShouldPublishVisionLocally()
    {
        if (NetworkSync.IsOffline) return true;
        if (!NetworkSync.IsClient) return false;

        var ni = NetworkSync.FindIdentity(this.GetActorId());
        return NetworkSync.IsOwnedHere(ni);
    }
}
