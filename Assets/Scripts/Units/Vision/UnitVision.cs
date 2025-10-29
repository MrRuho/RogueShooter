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

    void Awake()
    {
        _tr = transform;
        _unit = GetComponent<Unit>();
        _unitKey = GetInstanceID();
    }

    void OnEnable()
    {
        if (_initialized)
        {
            UpdateVisionNow();
        }
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

    public void UpdateVisionNow()
    {
        if (visionSkill == null)
        {
            Debug.LogWarning($"[UnitVision] {name} visionSkill is not set");
            return;
        }

        var lg = LevelGrid.Instance;
        if (lg == null)
        {
            Debug.LogWarning($"[UnitVision] {name} cannot find LevelGrid instance");
            return;
        }

        var origin = lg.GetGridPosition(_tr.position);

        if (_unit != null)
        {
            var uf = _unit.GetGridPosition().floor;
            origin = new GridPosition(origin.x, origin.z, uf);
        }
        
        int range = visionSkill.visionRange;
        var cfg = LoSConfig.Instance;

        HashSet<GridPosition> vis;
        if (visionSkill.useHeightAware)
        {
            vis = RaycastVisibility.ComputeVisibleTilesRaycastHeightAware(
                origin, range,
                cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU,
                ignoreRoot: _tr
            );
        }
        else
        {
            vis = RaycastVisibility.ComputeVisibleTilesRaycast(
                origin, range,
                cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU
            );
        }

        _lastVisible = vis;
        
        Debug.Log($"[UnitVision] {name} at {origin} (Team {teamId}): Updated vision, {vis.Count} tiles visible");
        
        if (TeamVisionService.Instance != null)
        {
            TeamVisionService.Instance.ReplaceUnitVision(teamId, _unitKey, _lastVisible);
        }
        else
        {
            Debug.LogWarning($"[UnitVision] {name} - TeamVisionService.Instance is null!");
        }
        
        _initialized = true;
    }

    public void NotifyMoved() => UpdateVisionNow();
}
