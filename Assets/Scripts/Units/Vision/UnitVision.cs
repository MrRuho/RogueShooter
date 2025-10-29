
/*
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
            if (_initialized)
            StartCoroutine(Co_DeferredFirstVision()); 
    }

    void OnEnable()
    {

    }

    void OnDisable()
    {
        if (TeamVisionService.Instance != null)
        {
            Debug.Log($"[UnitVision] {name} (Team {teamId}) removing vision on disable");
            TeamVisionService.Instance.RemoveUnitVision(teamId, _unitKey);
        }
        _lastVisible.Clear();
    }

    public void InitializeVision(int setTeamId, UnitArchetype archetype)
    {
        // jos oli aiemmin väärä tiimi, siivoa se ensin
        if (_initialized && setTeamId != _currentTeamId && TeamVisionService.Instance != null)
            TeamVisionService.Instance.RemoveUnitVision(_currentTeamId, _unitKey);

        _currentTeamId = setTeamId;
        teamId = setTeamId;
        if (archetype != null) visionSkill = archetype;

        _initialized = true;

        // eka päivitys frame-viiveellä, jotta LevelGrid ym. ovat varmasti valmiit
        StartCoroutine(Co_DeferredFirstVision());
    }
    

    private System.Collections.IEnumerator Co_DeferredFirstVision()
    {
        yield return null;
        UpdateVisionNow();
    }

    public void NotifyMoved() => UpdateVisionNow();

    public bool IsInitialized => _initialized;   // <-- lisää tämä

    public void UpdateVisionNow()
    {
        // 🔒 Älä tee mitään ennen kuin init on tehty ja konfiguraatiot on olemassa
        if (!_initialized) return;
        if (visionSkill == null) return;

        var lg  = LevelGrid.Instance;
        var cfg = LoSConfig.Instance;
        var tvs = TeamVisionService.Instance;
        if (lg == null || cfg == null || tvs == null) return;

        // Käytä world -> grid, jotta toimii myös ennen kuin Unitin oma bufferi päivittyy
        var wp = _tr != null ? _tr.position : transform.position;
        var origin = lg.GetGridPosition(wp);

        if (_unit != null)
        {
            var uf = _unit.GetGridPosition(); // floor talteen jos se on jo tiedossa
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

        // 🔒 tvs on tarkistettu ei-nulliksi: turvallinen
        tvs.ReplaceUnitVision(teamId, _unitKey, _lastVisible);
    }
}
*/

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
        Debug.Log($"[UnitVision AWAKE] {name}, unitKey: {_unitKey}");
    }

    void OnEnable()
    {
        Debug.Log($"[UnitVision ENABLE] {name}, initialized: {_initialized}");
    }

    void OnDisable()
    {
        Debug.Log($"[UnitVision DISABLE] {name}, Team {teamId}");
        if (TeamVisionService.Instance != null)
        {
            TeamVisionService.Instance.RemoveUnitVision(teamId, _unitKey);
        }
        _lastVisible.Clear();
    }

    public void InitializeVision(int setTeamId, UnitArchetype archetype)
    {
        Debug.Log($"[UnitVision INIT] {name}, Team {setTeamId}, archetype: {archetype?.name ?? "null"}");
        
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
        Debug.Log($"[UnitVision DEFERRED] {name}, calling UpdateVisionNow()");
        UpdateVisionNow();
    }

    public void NotifyMoved()
    {
        Debug.Log($"[UnitVision MOVED] {name}");
        UpdateVisionNow();
    }

    public bool IsInitialized => _initialized;

    public void UpdateVisionNow()
    {
        Debug.Log($"[UnitVision UPDATE START] {name}, initialized: {_initialized}, visionSkill: {visionSkill?.name ?? "null"}");
        
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

        var lg  = LevelGrid.Instance;
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

        Debug.Log($"[UnitVision UPDATE DONE] {name} at {origin}, Team {teamId}, {_lastVisible.Count} tiles, unitKey {_unitKey}");
        tvs.ReplaceUnitVision(teamId, _unitKey, _lastVisible);
    }
}
