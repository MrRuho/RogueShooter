/*
using System;
using System.Collections.Generic;
using UnityEngine;

public class TeamVisionService : MonoBehaviour
{
    public static TeamVisionService Instance { get; private set; }

    public event Action<int> OnTeamVisionChanged;
    
    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log("[TeamVisionService] Initialized");
    }

    private readonly Dictionary<int, VisionAccumulator> _teams = new();

    public IReadOnlyCollection<GridPosition> GetVisibleTilesSnapshot(int teamId)
    {
        var snapshot = GetAcc(teamId).GetVisibleSnapshot();
        Debug.Log($"[TeamVisionService] GetVisibleTilesSnapshot for team {teamId}: {snapshot.Count} tiles");
        return snapshot;
    }

    private void NotifyTeamChanged(int teamId)
    {
        Debug.Log($"[TeamVisionService] Team {teamId} vision changed, notifying listeners");
        OnTeamVisionChanged?.Invoke(teamId);
    }
    
    private VisionAccumulator GetAcc(int teamId)
    {
        if (!_teams.TryGetValue(teamId, out var acc))
        {
            _teams[teamId] = acc = new VisionAccumulator();
            Debug.Log($"[TeamVisionService] Created new VisionAccumulator for team {teamId}");
        }
        return acc;
    }

    public void ReplaceUnitVision(int teamId, int unitKey, HashSet<GridPosition> newSet)
    {
        Debug.Log($"[TeamVisionService] ReplaceUnitVision - Team {teamId}, Unit {unitKey}, {newSet.Count} tiles");
        GetAcc(teamId).ReplaceUnitSet(unitKey, newSet);
        NotifyTeamChanged(teamId);
    }

    public void RemoveUnitVision(int teamId, int unitKey)
    {
        Debug.Log($"[TeamVisionService] RemoveUnitVision - Team {teamId}, Unit {unitKey}");
        GetAcc(teamId).RemoveUnitSet(unitKey);
        NotifyTeamChanged(teamId);
    }

    public bool IsVisibleToTeam(int teamId, GridPosition gp)
        => GetAcc(teamId).IsVisible(gp);

    private class VisionAccumulator
    {
        private readonly Dictionary<int, HashSet<GridPosition>> _unitSets = new();
        private readonly Dictionary<GridPosition, int> _counts = new();

        public void ReplaceUnitSet(int unitKey, HashSet<GridPosition> newSet)
        {
            if (_unitSets.TryGetValue(unitKey, out var oldSet))
            {
                foreach (var gp in oldSet)
                {
                    if (_counts.TryGetValue(gp, out int c))
                    {
                        c--; if (c <= 0) _counts.Remove(gp); else _counts[gp] = c;
                    }
                }
            }

            _unitSets[unitKey] = newSet;

            foreach (var gp in newSet)
            {
                _counts.TryGetValue(gp, out int c);
                _counts[gp] = c + 1;
            }
        }

        public void RemoveUnitSet(int unitKey)
        {
            if (!_unitSets.TryGetValue(unitKey, out var oldSet)) return;
            foreach (var gp in oldSet)
            {
                if (_counts.TryGetValue(gp, out int c))
                {
                    c--; if (c <= 0) _counts.Remove(gp); else _counts[gp] = c;
                }
            }
            _unitSets.Remove(unitKey);
        }

        public IReadOnlyCollection<GridPosition> GetVisibleSnapshot()
        {
            return new List<GridPosition>(_counts.Keys);
        }

        public bool IsVisible(GridPosition gp) => _counts.ContainsKey(gp);

    }
}
*/
using System;
using System.Collections.Generic;
using UnityEngine;

public class TeamVisionService : MonoBehaviour
{
    public static TeamVisionService Instance { get; private set; }

    public event Action<int> OnTeamVisionChanged;
    
    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log("[TeamVisionService] Initialized");
    }

    private readonly Dictionary<int, VisionAccumulator> _teams = new();

    public IReadOnlyCollection<GridPosition> GetVisibleTilesSnapshot(int teamId)
    {
        var snapshot = GetAcc(teamId).GetVisibleSnapshot();
        Debug.Log($"[TeamVisionService] GetVisibleTilesSnapshot for team {teamId}: {snapshot.Count} tiles");
        return snapshot;
    }

    private void NotifyTeamChanged(int teamId)
    {
        Debug.Log($"[TeamVisionService] Team {teamId} vision changed, notifying listeners");
        OnTeamVisionChanged?.Invoke(teamId);
    }
    
    private VisionAccumulator GetAcc(int teamId)
    {
        if (!_teams.TryGetValue(teamId, out var acc))
        {
            _teams[teamId] = acc = new VisionAccumulator();
            Debug.Log($"[TeamVisionService] Created new VisionAccumulator for team {teamId}");
        }
        return acc;
    }

    public void ReplaceUnitVision(int teamId, int unitKey, HashSet<GridPosition> newSet)
    {
        Debug.Log($"[TeamVisionService] ReplaceUnitVision - Team {teamId}, Unit {unitKey}, {newSet.Count} tiles");
        GetAcc(teamId).ReplaceUnitSet(unitKey, newSet);
        NotifyTeamChanged(teamId);
    }

    public void RemoveUnitVision(int teamId, int unitKey)
    {
        Debug.Log($"[TeamVisionService] RemoveUnitVision - Team {teamId}, Unit {unitKey}");
        GetAcc(teamId).RemoveUnitSet(unitKey);
        NotifyTeamChanged(teamId);
    }

    public void ClearTeamVision(int teamId)
    {
        if (_teams.TryGetValue(teamId, out var acc))
        {
            acc.Clear();
            NotifyTeamChanged(teamId);
            Debug.Log($"[TeamVisionService] Cleared Team {teamId} vision");
        }
    }

    public bool IsVisibleToTeam(int teamId, GridPosition gp)
        => GetAcc(teamId).IsVisible(gp);

    private class VisionAccumulator
    {
        private readonly Dictionary<int, HashSet<GridPosition>> _unitSets = new();
        private readonly Dictionary<GridPosition, int> _counts = new();

        public void ReplaceUnitSet(int unitKey, HashSet<GridPosition> newSet)
        {
            if (_unitSets.TryGetValue(unitKey, out var oldSet))
            {
                foreach (var gp in oldSet)
                {
                    if (_counts.TryGetValue(gp, out int c))
                    {
                        c--; if (c <= 0) _counts.Remove(gp); else _counts[gp] = c;
                    }
                }
            }

            _unitSets[unitKey] = newSet;

            foreach (var gp in newSet)
            {
                _counts.TryGetValue(gp, out int c);
                _counts[gp] = c + 1;
            }
        }

        public void RemoveUnitSet(int unitKey)
        {
            if (!_unitSets.TryGetValue(unitKey, out var oldSet)) return;
            foreach (var gp in oldSet)
            {
                if (_counts.TryGetValue(gp, out int c))
                {
                    c--; if (c <= 0) _counts.Remove(gp); else _counts[gp] = c;
                }
            }
            _unitSets.Remove(unitKey);
        }

        public void Clear()
        {
            _unitSets.Clear();
            _counts.Clear();
        }

        public IReadOnlyCollection<GridPosition> GetVisibleSnapshot()
        {
            return new List<GridPosition>(_counts.Keys);
        }

        public bool IsVisible(GridPosition gp) => _counts.ContainsKey(gp);
    }
}

