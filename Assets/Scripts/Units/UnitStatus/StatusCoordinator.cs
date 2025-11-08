using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StatusCoordinator : MonoBehaviour
{
    public static StatusCoordinator Instance { get; private set; }

    [Header("Overwatch tempo")]
    [SerializeField, Range(0f, 0.25f)] private float reactionJitterMaxSeconds = 0.10f;
    [SerializeField] private float reactionCooldownSeconds = 0.5f;
    
    private readonly Dictionary<Unit, float> _nextReactAt = new();
    private readonly Dictionary<int, HashSet<Unit>> overwatchByTeam = new();

    [SerializeField] private bool onlyOneOverwatchAttackPerMovedTile = false;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("There's more than one StatusCoordinator!");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Unit.OnAnyUnitDead += OnAnyUnitDead;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        Unit.OnAnyUnitDead -= OnAnyUnitDead;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        StartCoroutine(Co_ClearAfterSceneLoad());
    }

    private IEnumerator Co_ClearAfterSceneLoad()
    {
        yield return null;
        PurgeDeadAndNullWatchers();
        ClearAllWatchers();
        GridSystemVisual.Instance.ClearAllPersistentOverwatch();
    }

    public void UnitTurnEndStatus(IEnumerable<Unit> teamUnits)
    {
        PurgeDeadAndNullWatchers();

        foreach (var u in teamUnits)
        {
            if (!u) continue;

            var vision = u.GetComponent<UnitVision>();
            if (vision == null || !vision.IsInitialized) continue;

            int ap = u.GetActionPoints();
            float angle = vision.GetDynamicConeAngle(ap, 80f);

            if (u.TryGetComponent<OverwatchAction>(out var ow) && ow.IsOverwatch())
            {
                vision.UpdateVisionNow();
                
                Vector3 facing = u.transform.forward;
                float coneAngle = 80f;
                
                if (u.TryGetComponent<UnitStatusController>(out var status) &&
                    status.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var payload))
                {
                    facing = payload.facingWorld;
                    coneAngle = payload.coneAngleDeg;
                    if (facing.sqrMagnitude < 1e-4f) facing = u.transform.forward;
                }
                else
                {
                    var dir = ow.TargetWorld - u.transform.position; dir.y = 0f;
                    if (dir.sqrMagnitude > 1e-4f) facing = dir.normalized;
                }
                
                var coneTiles = vision.GetConeVisibleTiles(facing, coneAngle);
                if (coneTiles != null && TeamVisionService.Instance != null)
                {
                    int teamId = u.GetTeamID();
                    int unitKey = vision.GetInstanceID();
                    TeamVisionService.Instance.ReplaceUnitVision(teamId, unitKey, new HashSet<GridPosition>(coneTiles));
                }
                
                continue;
            }

            if (angle >= 359.5f)
            {
                vision.UpdateVisionNow();
                continue;
            }

            Vector3 facingNormal = u.transform.forward;
            vision.ApplyAndPublishDirectionalVision(facingNormal, angle);
        }

        SetOverWatch(teamUnits);
    }

    public void UnitTurnStartStatus(IEnumerable<Unit> teamUnits)
    {
        PurgeDeadAndNullWatchers();
        if (teamUnits == null) return;

        foreach (var u in teamUnits)
        {
            if (!u) continue;

            if (u.TryGetComponent<UnitVision>(out var vision) && vision.IsInitialized)
            {
                vision.UpdateVisionNow();
            }
        }

        RemoveOverWatchSafe(teamUnits);
    }

    private void RemoveOverWatchSafe(IEnumerable<Unit> teamUnits)
    {
        if (teamUnits == null) return;

        foreach (var unit in teamUnits)
        {
            if (!unit) continue;

            if (unit.TryGetComponent<UnitStatusController>(out var status) &&
                status.Has(UnitStatusType.Overwatch))
            {
                status.Remove(UnitStatusType.Overwatch);
            }

            GridSystemVisual.Instance.RemovePersistentOverwatch(unit);

            if (unit.TryGetComponent<OverwatchAction>(out var action))
            {
                action.CancelOverwatchIntent();
            }

            RemoveWatcher(unit);
        }
    }

    public void AddWatcher(Unit unit)
    {
        int teamId = unit.GetTeamID();

        if (!overwatchByTeam.TryGetValue(teamId, out var set))
        {
            set = new HashSet<Unit>();
            overwatchByTeam[teamId] = set;
        }

        set.Add(unit);
    }

    public void RemoveWatcher(Unit unit)
    {
        int teamId = unit.GetTeamID();

        if (overwatchByTeam.TryGetValue(teamId, out var set))
        {
            set.Remove(unit);
            if (set.Count == 0) overwatchByTeam.Remove(teamId);
            _nextReactAt.Remove(unit);
        }
    }

    private void SetOverWatch(IEnumerable<Unit> teamUnits)
    {
        if (!NetworkServer.active && !NetworkSync.IsOffline)
        {
            Debug.LogWarning("[OW-StatusCoord] SetOverWatch called on non-server in online game - this should not happen!");
            return;
        }

        foreach (var unit in teamUnits)
        {
            if (!unit) continue;

            var action = unit.GetComponent<OverwatchAction>();
            if (action != null && action.IsOverwatch())
            {
                var status = unit.GetComponent<UnitStatusController>();
                if (status == null)
                {
                    Debug.LogWarning($"[OW-StatusCoord] {unit.name} has no UnitStatusController!");
                    continue;
                }

                if (!status.Has(UnitStatusType.Overwatch))
                {
                    var setup = new OverwatchPayload
                    {
                        facingWorld = unit.transform.forward,
                        coneAngleDeg = 80f,
                        rangeTiles = 8
                    };
                    status.AddOrUpdate(UnitStatusType.Overwatch, setup);
                }
                
                AddWatcher(unit);
            }
        }
    }

    public IEnumerable<Unit> GetWatchers(int teamId)
    {
        return overwatchByTeam.TryGetValue(teamId, out var set) ? set : System.Array.Empty<Unit>();
    }

    private bool CanReactNow(Unit w) => !_nextReactAt.TryGetValue(w, out var t) || Time.time >= t;

    private void MarkReactedNow(Unit w) { if (w) _nextReactAt[w] = Time.time + reactionCooldownSeconds; }

    public void CheckOverwatchStep(Unit mover, GridPosition newGridPos)
    {
        int enemyTeamId = (mover.GetTeamID() == 0) ? 1 : 0;
        var watchers = GetWatchers(enemyTeamId);

        foreach (var watcher in watchers)
        {
            if (!watcher || watcher.IsDead() || watcher.IsDying()) continue;

            if (!watcher.TryGetComponent<UnitVision>(out var vision) || !vision.IsInitialized) continue;

            Vector3 facingWorld;
            float coneDeg = 80f;

            if (watcher.TryGetComponent<UnitStatusController>(out var status) &&
                status.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var payload))
            {
                facingWorld = new Vector3(payload.facingWorld.x, 0f, payload.facingWorld.z).normalized;
                coneDeg = payload.coneAngleDeg;
            }
            else
            {
                facingWorld = watcher.transform.forward;
            }

            var personal = vision.GetUnitVisionGrids();
            if (personal == null || personal.Count == 0)
            {
                vision.UpdateVisionNow();
                personal = vision.GetUnitVisionGrids();
            }
            if (personal == null || !personal.Contains(newGridPos))
                continue;

            if (!vision.IsTileInCone(facingWorld, coneDeg, newGridPos))
                continue;

            if (!CanReactNow(watcher))
            {
                continue;
            }
            MarkReactedNow(watcher);

            var target = LevelGrid.Instance.GetUnitAtGridPosition(newGridPos);

            StartCoroutine(Co_TriggerOverwatchWithJitter(watcher, target, newGridPos));

            if (onlyOneOverwatchAttackPerMovedTile) break;
        }
    }
    
    private IEnumerator Co_TriggerOverwatchWithJitter(Unit watcher, Unit target, GridPosition posAtTrigger)
    {
        float delay = UnityEngine.Random.Range(0f, reactionJitterMaxSeconds);
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (!NetworkSync.IsOffline && !Mirror.NetworkServer.active) yield break;
        if (!watcher || watcher.IsDead() || watcher.IsDying()) yield break;
        if (!target  || target.IsDead()  || target.IsDying())  yield break;
        if (watcher.GetReactionPoints() <= 0) yield break;

        watcher.SpendReactionPoints();
        NetworkSync.TriggerOverwatchShot(watcher, target, posAtTrigger);
    }

    private void OnAnyUnitDead(object sender, EventArgs e)
    {
        var dead = sender as Unit;

        if (dead)
        {
            GridSystemVisual.Instance.RemovePersistentOverwatch(dead);
            if (dead.TryGetComponent<OverwatchAction>(out var ow))
                ow.CancelOverwatchIntent();
        }

        PurgeDeadAndNullWatchers(dead);
        _nextReactAt.Remove(dead);
    }
    
    private void PurgeDeadAndNullWatchers(Unit maybeDead = null)
    {
        if (overwatchByTeam.Count == 0) return;

        var teams = new List<int>(overwatchByTeam.Keys);
        foreach (var team in teams)
        {
            if (!overwatchByTeam.TryGetValue(team, out var set) || set == null)
            {
                overwatchByTeam.Remove(team);
                continue;
            }

            set.RemoveWhere(unit => !unit || ReferenceEquals(unit, maybeDead) || unit.IsDead() || unit.IsDying());

            if (set.Count == 0)
                overwatchByTeam.Remove(team);
        }
    }

    public void ClearAllWatchers()
    {
        overwatchByTeam.Clear();
        _nextReactAt.Clear();
    }
}
