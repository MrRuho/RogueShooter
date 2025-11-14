using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Global, scene-persistent service that maintains each team's aggregate
/// line-of-sight/visibility as the union of all units' vision sets.
/// </summary>
/// <remarks>
/// - Lives across scene loads via <see cref="Object.DontDestroyOnLoad(UnityEngine.Object)"/>.
/// - Visibility is tracked per <c>teamId</c>.
/// - Each unit contributes a set of <see cref="GridPosition"/> values.
/// - Internally, overlapping vision is handled with reference counting,
///   so a tile stays visible while at least one unit still sees it.
/// - Raises <see cref="OnTeamVisionChanged"/> whenever a team's aggregate
///   vision changes.
/// - Intended for use from Unity's main thread only.
/// </remarks>
public class TeamVisionService : MonoBehaviour
{
    /// <summary>
    /// Singleton instance of the service.
    /// </summary>
    public static TeamVisionService Instance { get; private set; }

    /// <summary>
    /// Fired when a team's aggregate vision changes.
    /// The argument is the affected <c>teamId</c>.
    /// </summary>
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

    /// <summary>
    /// Per-team accumulators of visible tiles.
    /// </summary>
    private readonly Dictionary<int, VisionAccumulator> _teams = new();

    /// <summary>
    /// Returns a snapshot (copy) of all tiles currently visible to the given team.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    /// <returns>Read-only collection of visible <see cref="GridPosition"/> tiles.</returns>
    public IReadOnlyCollection<GridPosition> GetVisibleTilesSnapshot(int teamId)
    {
        var snapshot = GetAcc(teamId).GetVisibleSnapshot();
        return snapshot;
    }

    /// <summary>
    /// Notifies listeners that a team's vision changed.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    private void NotifyTeamChanged(int teamId)
    {
        OnTeamVisionChanged?.Invoke(teamId);
    }

    /// <summary>
    /// Gets (or lazily creates) the accumulator for the given team.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    /// <returns>Accumulator for that team.</returns>
    private VisionAccumulator GetAcc(int teamId)
    {
        if (!_teams.TryGetValue(teamId, out var acc))
        {
            _teams[teamId] = acc = new VisionAccumulator();
        }
        return acc;
    }

    /// <summary>
    /// Replaces a unit's visible-tile set for a team, updating the team's aggregate vision.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    /// <param name="unitKey">
    /// Stable key identifying the unit (e.g., instance ID, net ID, or other unique handle).
    /// </param>
    /// <param name="newSet">The unit's current visible tiles.</param>
    public void ReplaceUnitVision(int teamId, int unitKey, HashSet<GridPosition> newSet)
    {
        GetAcc(teamId).ReplaceUnitSet(unitKey, newSet);
        NotifyTeamChanged(teamId);
    }

    /// <summary>
    /// Removes a unit's contribution to a team's vision (e.g., on despawn or death).
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    /// <param name="unitKey">Stable key identifying the unit.</param>
    public void RemoveUnitVision(int teamId, int unitKey)
    {
        GetAcc(teamId).RemoveUnitSet(unitKey);
        NotifyTeamChanged(teamId);
    }

    /// <summary>
    /// Clears all visibility data for a team.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    public void ClearTeamVision(int teamId)
    {
        if (_teams.TryGetValue(teamId, out var acc))
        {
            acc.Clear();
            NotifyTeamChanged(teamId);
        }
    }

    /// <summary>
    /// Returns whether the specified tile is currently visible to the team.
    /// </summary>
    /// <param name="teamId">Team identifier.</param>
    /// <param name="gp">Grid position to query.</param>
    /// <returns><c>true</c> if visible; otherwise <c>false</c>.</returns>
    public bool IsVisibleToTeam(int teamId, GridPosition gp)
        => GetAcc(teamId).IsVisible(gp);

    /// <summary>
    /// Internal helper that aggregates unit vision for a single team using
    /// reference counting per tile.
    /// </summary>
    private class VisionAccumulator
    {
        /// <summary>
        /// Per-unit visible-tile sets.
        /// </summary>
        private readonly Dictionary<int, HashSet<GridPosition>> _unitSets = new();

        /// <summary>
        /// Reference counts per tile across all units in the team.
        /// </summary>
        private readonly Dictionary<GridPosition, int> _counts = new();

        /// <summary>
        /// Replaces the stored set for one unit and updates per-tile reference counts.
        /// </summary>
        /// <param name="unitKey">Unit identifier.</param>
        /// <param name="newSet">The unit's current visible tiles.</param>
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

        /// <summary>
        /// Removes a unit's contribution entirely and updates reference counts.
        /// </summary>
        /// <param name="unitKey">Unit identifier.</param>
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

        /// <summary>
        /// Clears all unit data and all reference counts for the team.
        /// </summary>
        public void Clear()
        {
            _unitSets.Clear();
            _counts.Clear();
        }

        /// <summary>
        /// Returns a snapshot (copy) of all tiles that are currently visible
        /// to at least one unit in the team.
        /// </summary>
        public IReadOnlyCollection<GridPosition> GetVisibleSnapshot()
        {
            return new List<GridPosition>(_counts.Keys);
        }

        /// <summary>
        /// Returns whether a tile has a non-zero reference count (i.e., is visible).
        /// </summary>
        /// <param name="gp">Grid position to query.</param>
        public bool IsVisible(GridPosition gp) => _counts.ContainsKey(gp);
    }
    
    public void RebuildTeamVisionLocal(int teamId, bool midTurnUpdate = false)
    {
        var list = UnitManager.Instance?.GetAllUnitList();
        if (list == null) return;

        int currentTurnTeamId = -1;
        if (midTurnUpdate && TurnSystem.Instance != null)
        {
            currentTurnTeamId =TeamsID.CurrentTurnTeamId();
        }

        foreach (var unit in list)
        {
            if (!unit) continue;
            int unitTeam = unit.GetTeamID();
            if (unitTeam != teamId) continue;
            if (unit.IsDead() || unit.IsDying()) continue;

            var vision = unit.GetComponent<UnitVision>();
            if (vision == null || !vision.IsInitialized) continue;

            vision.UpdateVisionNow();

            var statusCtrl = unit.GetComponent<UnitStatusController>();
            bool isStunned = statusCtrl != null && statusCtrl.Has(UnitStatusType.Stunned);

            Vector3 facing = unit.transform.forward;
            float angle;

            bool isCurrentTurnTeam = unitTeam == currentTurnTeamId;

            if (midTurnUpdate && isCurrentTurnTeam)
            {
                if (isStunned)
                {
                    angle = vision.VisionPenaltyWhenUsingAP(0);
                }
                else
                {
                    angle = 360f;
                }
            }
            else
            {
                int actionpoints = unit.GetActionPoints();
                angle = vision.VisionPenaltyWhenUsingAP(actionpoints);

                if (unit.TryGetComponent<OverwatchAction>(out var ow) && ow.IsOverwatch())
                {
                    angle = vision.VisionPenaltyWhenUsingAP(0);
                    var dir = ow.TargetWorld - unit.transform.position; 
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 1e-4f) facing = dir.normalized;
                }
            }

            vision.ApplyAndPublishDirectionalVision(facing, angle);
        }
    }
}
