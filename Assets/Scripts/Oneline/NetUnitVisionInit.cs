using Mirror;
using UnityEngine;

[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(UnitVision))]
public class NetUnitVisionInit : NetworkBehaviour
{
    private Unit _unit;
    private UnitVision _vision;

    void Awake()
    {
        _unit = GetComponent<Unit>();
        _vision = GetComponent<UnitVision>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (isServer) return;
        
        if (_vision != null && !_vision.IsInitialized && _unit != null)
        {
            int teamId = _unit.GetTeamId();
            _vision.InitializeVision(teamId, _unit.archetype);
            Debug.Log($"[NetUnitVisionInit] Client initialized vision for {name}, Team {teamId}");
        }
    }
}