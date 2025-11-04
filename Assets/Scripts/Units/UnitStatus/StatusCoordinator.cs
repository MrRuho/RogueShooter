using System.Collections.Generic;
using UnityEngine;

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
    }

    // Kutsu tätä, kun pelaaja painaa End Turniä (oman tiimin loppu)
    public void UnitTurnEndStatus(IEnumerable<Unit> teamUnits)
    {
        SetOverWatch(teamUnits);
    }

    // Kutsu tätä heti, kun oma vuoro alkaa
    public void UnitTurnStartStatus(IEnumerable<Unit> teamUnits)
    {
        RemoveOverWatch(teamUnits);
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

    private void RemoveOverWatch(IEnumerable<Unit> teamUnits)
    {
        foreach (var unit in teamUnits)
        {
            // Perutaan OverWatch tilat.
            var status = unit.GetComponent<UnitStatusController>();
            if (status != null && status.Has(UnitStatusType.Overwatch))
            {
                status.Remove(UnitStatusType.Overwatch);
                Debug.Log($"[Overwatch] CLEARED on own turn start: {unit.name}");

                // Perutaan myös Actionin tila.
                var action = unit.GetComponent<OverwatchAction>();
                action.CancelOverwatchIntent();
                RemoveWatcher(unit);
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
                var visible = vision.GetUnitVisionGrids();
                if (visible != null && visible.Contains(newGridPos))
                {
                    Debug.Log($"[OW] {watcher.name} NÄKEE {mover.name} @ {newGridPos}");
                    // (myöhemmin tähän kartio/LoS/AP-reaktio)
                }
            }
        }
    }
}
