/*
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StatusCoordinator : MonoBehaviour
{
    public static StatusCoordinator Instance { get; private set; }

    private readonly Dictionary<int, HashSet<Unit>> overwatchByTeam = new();

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
        yield return null; // odota että GridSystemVisual.Start ehtii alusta
        PurgeDeadAndNullWatchers();
        ClearAllWatchers(); // overwatchByTeam.Clear();
        GridSystemVisual.Instance.ClearAllPersistentOverwatch();
    }


    // Kutsu tätä, kun pelaaja painaa End Turniä (oman tiimin loppu)
    public void UnitTurnEndStatus(IEnumerable<Unit> teamUnits)
    {
        PurgeDeadAndNullWatchers();

        foreach (var u in teamUnits)
        {
            var vision = u.GetComponent<UnitVision>();
            if (vision == null || !vision.IsInitialized) continue;

            int ap = u.GetActionPoints();
            float angle = vision.GetDynamicConeAngle(ap, 80f);

            // 360° → ei tarvi suodattaa, pidä täysi visio
            if (angle >= 359.5f) { vision.UpdateVisionNow(); continue; }

            // facing: Overwatch-suunta jos aseistettu, muuten forward
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

    // Kutsu tätä heti, kun oma vuoro alkaa
    public void UnitTurnStartStatus(IEnumerable<Unit> teamUnits)
    {
        PurgeDeadAndNullWatchers();
        if (teamUnits == null) return;

        // Vältä mahdolliset samanaikaiset muutokset: ota snapshot (valinnainen, mutta turvallinen)
        foreach (var u in teamUnits)
        {
            if (!u) continue; // destroyed tai null

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
            if (!unit) continue; // destroyed tai null

            // Poista status turvallisesti
            if (unit.TryGetComponent<UnitStatusController>(out var status) &&
                status.Has(UnitStatusType.Overwatch))
            {
                status.Remove(UnitStatusType.Overwatch);
            }

            // Poista pysyvä OW-maalaus (varmistus)
            GridSystemVisual.Instance.RemovePersistentOverwatch(unit);

            // Peru intentti, jos action vielä olemassa
            if (unit.TryGetComponent<OverwatchAction>(out var action))
            {
                action.CancelOverwatchIntent();
            }

            RemoveWatcher(unit);
        }
    }

    private void SetOverWatch(IEnumerable<Unit> teamUnits)
    {

        foreach (var unit in teamUnits)
        {
            // Tarkistetaan OverWatch tilat.
            var action = unit.GetComponent<OverwatchAction>();
            if (action != null && action.IsOverwatch())
            {
                var status = unit.GetComponent<UnitStatusController>();
                if (status == null) continue;

                // Yksinkertainen payload nyt: käytä unitin nykyistä suuntaa
                var setup = new OverwatchPayload
                {
                    facingWorld = unit.transform.forward,
                    coneAngleDeg = 80f,
                    rangeTiles = 8
                };

                status.AddOrUpdate(UnitStatusType.Overwatch, setup);
                Debug.Log($"[Overwatch] ARMED at end turn: {unit.name}");

                //Aseta tiimilistaan.
                AddWatcher(unit);
            }
        }
    }

    // Lisää unit tiimin watchlistaan
    public void AddWatcher(Unit unit)
    {
        int teamId = unit.GetTeamID();

        if (!overwatchByTeam.TryGetValue(teamId, out var set))
        {
            set = new HashSet<Unit>();
            overwatchByTeam[teamId] = set;
        }

        set.Add(unit); // HashSet estää duplikaatit
    }

    // Poista unit tiimin watchlistasta
    public void RemoveWatcher(Unit unit)
    {
        int teamId = unit.GetTeamID();

        if (overwatchByTeam.TryGetValue(teamId, out var set))
        {
            set.Remove(unit);
            if (set.Count == 0) overwatchByTeam.Remove(teamId); // siisti tyhjät
        }
    }

    public IEnumerable<Unit> GetWatchers(int teamId)
    {
        return overwatchByTeam.TryGetValue(teamId, out var set) ? set : System.Array.Empty<Unit>();
    }

    public void CheckOverwatchStep(Unit mover, GridPosition newGridPos)
    {
        int enemyTeamId = (mover.GetTeamID() == 0) ? 1 : 0;

        foreach (var watcher in GetWatchers(enemyTeamId))
        {
            if (!watcher || watcher.IsDead() || watcher.IsDying()) continue;

            if (watcher.TryGetComponent<UnitVision>(out var vision) && vision.IsInitialized)
            {
                // 1) näkyvyyscache
                var visible = vision.GetUnitVisionGrids();
                if (visible == null || !visible.Contains(newGridPos)) continue;

                // 2) cone
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
                if (!coneTiles.Contains(newGridPos)) continue;

                // 3) varmista kohde on yhä olemassa JA elossa
                var target = LevelGrid.Instance.GetUnitAtGridPosition(newGridPos);
                if (!target || target.IsDying() || target.IsDead()) continue;

                // 4) AP-vartija
                if (watcher.GetActionPoints() <= 0) continue;

                // 5) Ota nimet talteen NYT (älä koske Unity-olioihin callbackissa)
                string watcherName = watcher ? watcher.name : "<null>";
                string targetName = target ? target.name : "<dead>";

                if (watcher.TryGetComponent<ShootAction>(out var shoot))
                {
                    shoot.TakeAction(newGridPos, () =>
                    {
                        // ÄLÄ viittaa watcher/target/mover -olioihin täällä
                        Debug.Log($"[OW] {watcherName} ampui {targetName} @ {newGridPos}");
                    });

                    break; // yksi reaktio / askel
                }
            }
        }
    }

    private void OnAnyUnitDead(object sender, EventArgs e)
    {
        var dead = sender as Unit;

        // Poista OW-maalaus ja peru intentti varmuuden vuoksi
        if (dead)
        {
            GridSystemVisual.Instance?.RemovePersistentOverwatch(dead);
            if (dead.TryGetComponent<OverwatchAction>(out var ow))
                ow.CancelOverwatchIntent();
        }

        // Siivoa watchlistat (myös mahdolliset "tyhjät" viitteet)
        PurgeDeadAndNullWatchers(dead);
    }
    
    private void PurgeDeadAndNullWatchers(Unit maybeDead = null)
    {
        if (overwatchByTeam.Count == 0) return;

        // ota avaimet snapshotiksi, jotta poisto ei riko iteraatiota
        var teams = new List<int>(overwatchByTeam.Keys);
        foreach (var team in teams)
        {
            if (!overwatchByTeam.TryGetValue(team, out var set) || set == null)
            {
                overwatchByTeam.Remove(team);
                continue;
            }

            // Poista: tuhotut (!u), juuri kuollut, sekä kuolleet/dying
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
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StatusCoordinator : MonoBehaviour
{
    public static StatusCoordinator Instance { get; private set; }

    private readonly Dictionary<int, HashSet<Unit>> overwatchByTeam = new();

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

    private void SetOverWatch(IEnumerable<Unit> teamUnits)
    {

        foreach (var unit in teamUnits)
        {
            if (!unit) continue;

            var action = unit.GetComponent<OverwatchAction>();
            if (action != null && action.IsOverwatch())
            {
                var status = unit.GetComponent<UnitStatusController>();
                if (status == null) continue;

                var setup = new OverwatchPayload
                {
                    facingWorld = unit.transform.forward,
                    coneAngleDeg = 80f,
                    rangeTiles = 8
                };

                status.AddOrUpdate(UnitStatusType.Overwatch, setup);
//                Debug.Log($"[Overwatch] ARMED at end turn: {unit.name}");

                AddWatcher(unit);
            }
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

    public IEnumerable<Unit> GetWatchers(int teamId)
    {
        return overwatchByTeam.TryGetValue(teamId, out var set) ? set : System.Array.Empty<Unit>();
    }

    public void CheckOverwatchStep(Unit mover, GridPosition newGridPos)
    {
        int enemyTeamId = (mover.GetTeamID() == 0) ? 1 : 0;

        foreach (var watcher in GetWatchers(enemyTeamId))
        {
            if (!watcher || watcher.IsDead() || watcher.IsDying()) continue;

            if (watcher.TryGetComponent<UnitVision>(out var vision) && vision.IsInitialized)
            {
                var visible = vision.GetUnitVisionGrids();
                if (visible == null || !visible.Contains(newGridPos)) continue;

                
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
                if (!coneTiles.Contains(newGridPos)) continue;

                var target = LevelGrid.Instance.GetUnitAtGridPosition(newGridPos);
                if (!target || target.IsDying() || target.IsDead()) continue;

                if (watcher.GetReactionPoints() <= 0) continue;
                watcher.SpendReactionPoints();

                string watcherName = watcher ? watcher.name : "<null>";
                string targetName = target ? target.name : "<dead>";
                if (watcherName == "<null>")
                {
                    Debug.LogWarning("[StatusCoordinator] Dead Unit try to shoot!");
                }
                if(targetName == "<dead>")
                {
                    Debug.LogWarning("[StatusCoordinator] Unit try to shoot but target is allready dead!");
                }
                
                
                if (watcher.TryGetComponent<ShootAction>(out var shoot))
                {
                    shoot.TakeAction(newGridPos, () =>
                    {
                      // Debug.Log($"[OW] {watcherName} ampui {targetName} @ {newGridPos}");
                    });

                  //  break;
                }
            }
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

        //Päivitä vision.
    }


    public void ClearAllWatchers()
    {
        overwatchByTeam.Clear();
    }
}
