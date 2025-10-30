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
    }

    private readonly Dictionary<int, VisionAccumulator> _teams = new();

    public IReadOnlyCollection<GridPosition> GetVisibleTilesSnapshot(int teamId)
    {
        var snapshot = GetAcc(teamId).GetVisibleSnapshot();
        return snapshot;
    }

    private void NotifyTeamChanged(int teamId)
    {
        OnTeamVisionChanged?.Invoke(teamId);
    }
    
    private VisionAccumulator GetAcc(int teamId)
    {
        if (!_teams.TryGetValue(teamId, out var acc))
        {
            _teams[teamId] = acc = new VisionAccumulator();
        }
        return acc;
    }

    public void ReplaceUnitVision(int teamId, int unitKey, HashSet<GridPosition> newSet)
    {

        GetAcc(teamId).ReplaceUnitSet(unitKey, newSet);
        NotifyTeamChanged(teamId);
    }

    public void RemoveUnitVision(int teamId, int unitKey)
    {

        GetAcc(teamId).RemoveUnitSet(unitKey);
        NotifyTeamChanged(teamId);
    }

    public void ClearTeamVision(int teamId)
    {
        if (_teams.TryGetValue(teamId, out var acc))
        {
            acc.Clear();
            NotifyTeamChanged(teamId);
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

