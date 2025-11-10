using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StatusCoordinator : MonoBehaviour
{
    public static StatusCoordinator Instance { get; private set; }

    [Header("Enemy Overwatch Display")]
    [SerializeField] private float enemyOverwatchVisibilityDuration = 3f;
    
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

    public float GetEnemyOverwatchVisibilityDuration()
    {
        return enemyOverwatchVisibilityDuration;
    }

    public void UnitTurnEndStatus(IEnumerable<Unit> teamUnits)
    {
        PurgeDeadAndNullWatchers();

        foreach (var unit in teamUnits)
        {
            if (!unit) continue;

            var vision = unit.GetComponent<UnitVision>();
            if (vision == null || !vision.IsInitialized) continue;

            int ap = unit.GetActionPoints();
            float angle = vision.VisionPenaltyWhenUsingAP(ap);

            if (unit.TryGetComponent<OverwatchAction>(out var ow) && ow.IsOverwatch())
            {
                vision.UpdateVisionNow();
                
                var settings = ow.GetOverwatchSettings();
                Vector3 facing = unit.transform.forward;
                float coneAngle = settings.coneAngleDeg;
                
                if (unit.TryGetComponent<UnitStatusController>(out var status) &&
                    status.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var payload))
                {
                    facing = payload.facingWorld;
                    coneAngle = payload.coneAngleDeg;
                    if (facing.sqrMagnitude < 1e-4f) facing = unit.transform.forward;
                    
                    if (Mathf.Abs(payload.coneAngleDeg - settings.coneAngleDeg) > 0.1f ||
                        payload.rangeTiles != settings.rangeTiles)
                    {
                        payload.coneAngleDeg = settings.coneAngleDeg;
                        payload.rangeTiles = settings.rangeTiles;
                        status.AddOrUpdate(UnitStatusType.Overwatch, payload);
                    }
                }
                else
                {
                    var dir = ow.TargetWorld - unit.transform.position; dir.y = 0f;
                    if (dir.sqrMagnitude > 1e-4f) facing = dir.normalized;
                }
                
                var coneTiles = vision.GetConeVisibleTiles(facing, coneAngle);
                if (coneTiles != null && TeamVisionService.Instance != null)
                {
                    int teamId = unit.GetTeamID();
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

            Vector3 facingNormal = unit.transform.forward;
            vision.ApplyAndPublishDirectionalVision(facingNormal, angle);
        }

        SetOverWatch(teamUnits);
    }

    public void UnitTurnStartStatus(IEnumerable<Unit> teamUnits)
    {
        PurgeDeadAndNullWatchers();
        if (teamUnits == null) return;

        ForceCleanStateForTurnStart(teamUnits);
        
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
                    var settings = action.GetOverwatchSettings();
                    var setup = new OverwatchPayload
                    {
                        facingWorld = unit.transform.forward,
                        coneAngleDeg = settings.coneAngleDeg,
                        rangeTiles = settings.rangeTiles
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

    private void MarkReactedNow(Unit w, float cooldown) 
    { 
        if (w) _nextReactAt[w] = Time.time + cooldown; 
    }

    public void CheckOverwatchStep(Unit mover, GridPosition newGridPos)
    {
        int enemyTeamId = (mover.GetTeamID() == 0) ? 1 : 0;
        var watchers = GetWatchers(enemyTeamId);

        foreach (var watcher in watchers)
        {
            if (!watcher || watcher.IsDead() || watcher.IsDying()) continue;

            if (!watcher.TryGetComponent<UnitVision>(out var vision) || !vision.IsInitialized) continue;

            Vector3 facingWorld;
            float coneDeg;

            if (watcher.TryGetComponent<UnitStatusController>(out var status) &&
                status.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var payload))
            {
                facingWorld = payload.facingWorld;
                coneDeg = payload.coneAngleDeg;
            }
            else
            {
                facingWorld = OverwatchHelpers.NormalizeFacing(watcher.transform.forward);
                coneDeg = 80f;
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

            var owAction = watcher.GetComponent<OverwatchAction>();
            var settings = owAction != null ? owAction.GetOverwatchSettings() : OverwatchShootingSettings.Default;
            
            MarkReactedNow(watcher, settings.reactionCooldownSeconds);

            var target = LevelGrid.Instance.GetUnitAtGridPosition(newGridPos);

            StartCoroutine(Co_TriggerOverwatchWithJitter(watcher, target, newGridPos, settings.reactionJitterMaxSeconds));

            if (onlyOneOverwatchAttackPerMovedTile) break;
        }
    }
    
    private IEnumerator Co_TriggerOverwatchWithJitter(Unit watcher, Unit target, GridPosition posAtTrigger, float jitterMax)
    {
        float delay = UnityEngine.Random.Range(0f, jitterMax);
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

    public void ForceCleanStateForTurnStart(IEnumerable<Unit> teamUnits)
    {
        if (teamUnits == null) return;

        // 1. Pyyhi kaikki vanhat overwatch-visualisoinnit
        GridSystemVisual.Instance.ClearAllPersistentOverwatch();

        // 2. Pyyhi kaikki vanhat grid-merkinnät
        GridSystemVisual.Instance.HideAllGridPositions();

        // 3. Pakota jokaisen yksikön vision päivitys täyteen 360°
        foreach (var unit in teamUnits)
        {
            if (!unit || unit.IsDead() || unit.IsDying()) continue;

            // Varmista että UnitVision on initialisoitu
            if (unit.TryGetComponent<UnitVision>(out var vision) && vision.IsInitialized)
            {
                // Pakota täysi vision päivitys (360°, ei kartiota)
                vision.UpdateVisionNow();
                
                // Varmista että TeamVisionService saa päivityksen
                int teamId = unit.GetTeamID();
                int unitKey = vision.GetInstanceID();
                var visionTiles = vision.GetUnitVisionGrids();
                
                if (visionTiles != null && TeamVisionService.Instance != null)
                {
                    TeamVisionService.Instance.ReplaceUnitVision(teamId, unitKey, visionTiles);
                }
            }
        }

        // 4. Pakota grid-visualisoinnin päivitys
        if (GridSystemVisual.Instance != null)
        {
            GridSystemVisual.Instance.UpdateGridVisuals();
        }
    }
}
