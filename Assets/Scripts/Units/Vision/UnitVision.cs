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
    private HashSet<GridPosition> _lastVisibleTiles = new();
    private int _unitKey;
    private bool _initialized = false;
    private int _currentTeamId = 0;
    private HealthSystem _healthSystem;

    void Awake()
    {
        _tr = transform;
        _unit = GetComponent<Unit>();
        _healthSystem = GetComponent<HealthSystem>();
        _unitKey = GetInstanceID();
    }

    void OnEnable()
    {
        if (_healthSystem != null)
            _healthSystem.OnDying += HandleUnitDying;
    }

    void Start()
    {
        if (!_initialized)
            StartCoroutine(Co_AutoInitLocal());
    }

    void OnDisable()
    {
        if (_healthSystem != null)
            _healthSystem.OnDying -= HandleUnitDying;
        
        CleanupVision();
    }

    private void HandleUnitDying(object sender, System.EventArgs e)
    {
        CleanupVision();
    }

    private void CleanupVision()
    {
        if (TeamVisionService.Instance != null && _initialized)
        {
            TeamVisionService.Instance.RemoveUnitVision(teamId, _unitKey);
        }
        _lastVisibleTiles.Clear();
    }

    private System.Collections.IEnumerator Co_AutoInitLocal()
    {
        yield return null;

        if (_initialized) yield break;
        if (NetMode.IsDedicatedServer) yield break;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            int? team = _unit.GetTeamID();

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
        // KRIITTINEN: Älä päivitä visionia jos Unit on kuolemassa tai kuollut
        if (_unit != null && (_unit.IsDying() || _unit.IsDead()))
            return;
        
        if (_healthSystem != null && (_healthSystem.IsDying() || _healthSystem.IsDead()))
            return;

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

        var levelGrid = LevelGrid.Instance;
        var loSConfig = LoSConfig.Instance;
        var teamVision = TeamVisionService.Instance;

        if (levelGrid == null || loSConfig == null || teamVision == null)
        {
            Debug.LogWarning($"[UnitVision UPDATE SKIP] {name} - missing services: LG={levelGrid != null}, CFG={loSConfig != null}, TVS={teamVision != null}");
            return;
        }

        var wp = _tr != null ? _tr.position : transform.position;
        var origin = levelGrid.GetGridPosition(wp);

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
                loSConfig.losBlockersMask, loSConfig.eyeHeight, loSConfig.samplesPerCell, loSConfig.insetWU,
                ignoreRoot: _tr
            );
        }
        else
        {
            vis = RaycastVisibility.ComputeVisibleTilesRaycast(
                origin, visionSkill.visionRange,
                loSConfig.losBlockersMask, loSConfig.eyeHeight, loSConfig.samplesPerCell, loSConfig.insetWU
            );
        }

        _lastVisibleTiles = vis ?? _lastVisibleTiles;


        teamVision.ReplaceUnitVision(teamId, _unitKey, _lastVisibleTiles);    
    }

    private bool ShouldPublishVisionLocally()
    {
        if (NetworkSync.IsOffline) return true;
        if (!NetworkSync.IsClient) return false;

        var ni = NetworkSync.FindIdentity(this.GetActorId());
        return NetworkSync.IsOwnedHere(ni);
    }

    public HashSet<GridPosition> GetUnitVisionGrids()
    {
        return _lastVisibleTiles;
    }
}
