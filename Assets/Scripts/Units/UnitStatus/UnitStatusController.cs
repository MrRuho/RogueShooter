using System;
using System.Collections.Generic;
using UnityEngine;

public enum UnitStatusType { Overwatch /*, Stunned, Wounded, Suppressed, ...*/ }

public interface IStatusPayload { }

// Overwatchin parametrit statuksena
public struct OverwatchPayload : IStatusPayload {
    public Vector3 facingWorld;   // tai esim. yaw-aste
    public float coneAngleDeg;    // esim. 80
    public int   rangeTiles;      // esim. 8
}

public class UnitStatusController : MonoBehaviour {
    
    private readonly Dictionary<UnitStatusType, object> _map = new();

    public event Action<UnitStatusType> OnAdded;
    public event Action<UnitStatusType> OnRemoved;
    public event Action<UnitStatusType> OnChanged;

    public bool Has(UnitStatusType statusType) => _map.ContainsKey(statusType);

    public bool TryGet<T>(UnitStatusType statusType, out T setup) where T: struct, IStatusPayload {
        if (_map.TryGetValue(statusType, out var obj) && obj is T p) { setup = p; return true; }
        setup = default; return false;
    }

    public void AddOrUpdate<T>(UnitStatusType statusType, T setup) where T: struct, IStatusPayload {
        bool existed = _map.ContainsKey(statusType);
        _map[statusType] = setup;
        if (existed) OnChanged?.Invoke(statusType); else OnAdded?.Invoke(statusType);
    }

    public void Remove(UnitStatusType statusType) {
        if (_map.Remove(statusType)) OnRemoved?.Invoke(statusType);
    }
}
