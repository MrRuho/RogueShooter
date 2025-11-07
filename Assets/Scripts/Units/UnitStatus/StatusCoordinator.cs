using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StatusCoordinator : MonoBehaviour
{
    public static StatusCoordinator Instance { get; private set; }

    private readonly Dictionary<int, HashSet<Unit>> overwatchByTeam = new();

    // Vain yksi overwach action toiminto per liikuttu tile.
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

    /*
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

                if (angle >= 359.5f) { vision.UpdateVisionNow(); continue; }

                Vector3 facing = u.transform.forward;
                if (u.TryGetComponent<OverwatchAction>(out var ow) && ow.IsOverwatch())
                {
                    var dir = ow.TargetWorld - u.transform.position; dir.y = 0f;
                    if (dir.sqrMagnitude > 1e-4f) facing = dir.normalized;
                    angle = vision.GetDynamicConeAngle(0, 80f);
                }

                vision.ApplyAndPublishDirectionalVision(facing, angle);
            }

            SetOverWatch(teamUnits);
        }
    */
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

            if (angle >= 359.5f) { vision.UpdateVisionNow(); continue; }

            Vector3 facing = u.transform.forward;
            
            if (u.TryGetComponent<OverwatchAction>(out var ow) && ow.IsOverwatch())
            {
                // ✅ Lue AINA payloadista, älä TargetWorld:sta!
                if (u.TryGetComponent<UnitStatusController>(out var status) &&
                    status.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var payload))
                {
                    facing = payload.facingWorld;
                    if (facing.sqrMagnitude < 1e-4f) facing = u.transform.forward;
                }
                else
                {
                    // Fallback jos payloadia ei ole vielä
                    var dir = ow.TargetWorld - u.transform.position; dir.y = 0f;
                    if (dir.sqrMagnitude > 1e-4f) facing = dir.normalized;
                }
                
                angle = vision.GetDynamicConeAngle(0, 80f);
            }

            vision.ApplyAndPublishDirectionalVision(facing, angle);
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
        }
    }

    /*
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

                var setup = new OverwatchPayload
                {
                    facingWorld = unit.transform.forward,
                    coneAngleDeg = 80f,
                    rangeTiles = 8
                };

                status.AddOrUpdate(UnitStatusType.Overwatch, setup);
                AddWatcher(unit);
            }

        }
    }
    */
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

                // ✅ ÄLÄ ylikirjoita payloadia jos se on jo olemassa!
                if (!status.Has(UnitStatusType.Overwatch))
                {
                    // Payload puuttuu - luo fallback (ei pitäisi tapahtua normaalisti)
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

    /*
    public void CheckOverwatchStep(Unit mover, GridPosition newGridPos)
    {
  
        int enemyTeamId = (mover.GetTeamID() == 0) ? 1 : 0;
        var watchers = GetWatchers(enemyTeamId);
        
        foreach (var watcher in watchers)
        {
            if (!watcher || watcher.IsDead() || watcher.IsDying())
            {
                continue;
            }

            if (watcher.TryGetComponent<UnitVision>(out var vision) && vision.IsInitialized)
            {

                var visible = vision.GetUnitVisionGrids();

                if (visible == null || visible.Count == 0)
                {
                    // Pakota serverillä/offlinessa kerta-laskenta ilman julkaisuja
                    vision.UpdateVisionNow();                      // julkaisee vain publish==true; serverillä tämä vain täyttää välimuistin
                    visible = vision.GetUnitVisionGrids();
                }
                                
                if (visible == null || !visible.Contains(newGridPos))
                {
                    continue;
                }

                Vector3 facingWorld;
                if (watcher.TryGetComponent<OverwatchAction>(out var ow) && ow.IsOverwatch())
                {
                    var dir = ow.TargetWorld - watcher.transform.position;
                    dir.y = 0f;
                    facingWorld = (dir.sqrMagnitude > 1e-4f) ? dir.normalized : watcher.transform.forward;
                }
                else
                {
                    facingWorld = watcher.transform.forward;
                }

                var coneTiles = vision.GetConeVisibleTiles(facingWorld, 80f);
                if (!coneTiles.Contains(newGridPos))
                {
                    continue;
                }

                var target = LevelGrid.Instance.GetUnitAtGridPosition(newGridPos);
                if (!target || target.IsDying() || target.IsDead())
                {
                    continue;
                }

                if (watcher.GetReactionPoints() <= 0)
                {
                    continue;
                }

                watcher.SpendReactionPoints();
                NetworkSync.TriggerOverwatchShot(watcher, target, newGridPos);

                if (onlyOneOverwatchAttackPerMovedTile)
                {
                    break;
                }
            }
        }
    }
*/
// StatusCoordinator.cs

    public void CheckOverwatchStep(Unit mover, GridPosition newGridPos)
    {
        int enemyTeamId = (mover.GetTeamID() == 0) ? 1 : 0;
        var watchers = GetWatchers(enemyTeamId);

        foreach (var watcher in watchers)
        {
            if (!watcher || watcher.IsDead() || watcher.IsDying()) continue;

            if (!watcher.TryGetComponent<UnitVision>(out var vision) || !vision.IsInitialized) continue;

            // 1) näkyvyys tiimin unionista (varmistaa yhdenmukaisuuden UI:n kanssa)
            var tvs = TeamVisionService.Instance;
            if (tvs == null || !tvs.IsVisibleToTeam(watcher.GetTeamID(), newGridPos))
                continue; // ruutu ei ole tiimille näkyvä

            // 2) kartiosuodatus: ensisijaisesti status-payloadista
            Vector3 facingWorld; float coneDeg = 80f;

            if (watcher.TryGetComponent<UnitStatusController>(out var status) &&
                status.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var payload))
            {
                facingWorld = payload.facingWorld;
                coneDeg     = payload.coneAngleDeg;
            }
            else if (watcher.TryGetComponent<OverwatchAction>(out var ow) && ow.IsOverwatch())
            {
                var dir = ow.TargetWorld - watcher.transform.position; dir.y = 0f;
                facingWorld = (dir.sqrMagnitude > 1e-4f) ? dir.normalized : watcher.transform.forward;
            }
            else
            {
                facingWorld = watcher.transform.forward;
            }

            // 3) tarkista onko ruutu kartiossa
            if (!vision.IsTileInCone(facingWorld, coneDeg, newGridPos))
                continue;

            // 4) muut tarkistukset + laukaisu
            var target = LevelGrid.Instance.GetUnitAtGridPosition(newGridPos);
            if (!target || target.IsDying() || target.IsDead()) continue;
            if (watcher.GetReactionPoints() <= 0) continue;

            watcher.SpendReactionPoints();
            NetworkSync.TriggerOverwatchShot(watcher, target, newGridPos);
            if (onlyOneOverwatchAttackPerMovedTile) break;
        }
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
    }
}
