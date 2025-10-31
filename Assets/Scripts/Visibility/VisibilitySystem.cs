using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class VisibilitySystem : MonoBehaviour
{
    [SerializeField] private float pollInterval = 0.25f;
    [SerializeField] private bool hideEnemiesOnStart = true;

    private readonly HashSet<Unit> _visibleNow = new();
    private int _myTeam = -1;
    private bool _didInitialBaseline;

    void OnEnable()
    {
        var tvs = TeamVisionService.Instance;
        if (tvs != null) tvs.OnTeamVisionChanged += HandleTeamVisionChanged;
        StartCoroutine(Co_Poll());
    }

    void OnDisable()
    {
        var tvs = TeamVisionService.Instance;
        if (tvs != null) tvs.OnTeamVisionChanged -= HandleTeamVisionChanged;
        StopAllCoroutines();
        _visibleNow.Clear();
        _didInitialBaseline = false;
        _myTeam = -1;
    }

    private int ResolveLocalTeam()
    {
        // 1) Normaalisti: käytä keskitettyä apuria
        int id = NetworkSync.GetLocalPlayerTeamId(GameModeManager.SelectedMode);
    
        // 2) Jos ollaan puhdas client (ei host) JA tiimi on vielä epäselvä,
        //    yritä päätellä se ensimmäisestä omistetusta unitista.
        if (NetworkSync.IsClientOnly && UnitManager.Instance != null)
        {
            foreach (var u in UnitManager.Instance.GetUnitList())
            {
                if (!u) continue;
                var ni = u.GetComponent<Mirror.NetworkIdentity>();
                if (NetworkSync.IsOwnedHere(ni))
                    return u.GetTeamId(); // Versus: clientille 1, Co-op: 0
            }
        }

        return id;
    }

    private IEnumerator Co_Poll()
    {
        yield return null;

        _myTeam = ResolveLocalTeam();

        if (hideEnemiesOnStart && !_didInitialBaseline)
        {
            HideAllEnemiesImmediate();
            _didInitialBaseline = true;
        }

        RefreshVisibleEnemies();

        var wait = new WaitForSeconds(pollInterval);
        while (true)
        {
            RefreshVisibleEnemies();
            yield return wait;
        }
    }

    private void HandleTeamVisionChanged(int teamId)
    {
        if (teamId == _myTeam) RefreshVisibleEnemies();
    }

    private void RefreshVisibleEnemies()
    {
        // 0) Hae AJANTASAINEN tiimi jokaisella kutsulla
        int myTeam = NetworkSync.GetLocalPlayerTeamId(GameModeManager.SelectedMode);

        // 0b) Fallback: client-only + Versus → päättele tiimi omistetusta unitista,
        //     jos myTeam vielä 0 (hostin tiimi) ja omia unitteja on jo olemassa.
        if (NetworkSync.IsClientOnly && GameModeManager.SelectedMode == GameMode.Versus && myTeam == 0)
        {
            var um = UnitManager.Instance;
            if (um != null)
            {
                foreach (var unit in um.GetUnitList())
                {
                    if (!unit) continue;
                    var ni = unit.GetComponent<Mirror.NetworkIdentity>();
                    if (NetworkSync.IsOwnedHere(ni)) { myTeam = unit.GetTeamId(); break; }
                }
            }
        }

        // (valinnainen) pidä kenttä vain logeja varten ajantasalla
        _myTeam = myTeam;

        if (myTeam < 0) return;

        var tvs = TeamVisionService.Instance;
        if (tvs == null) { Debug.LogWarning("[VisibilitySystem] TeamVisionService is NULL!"); return; }

        var unitManager = UnitManager.Instance;
        if (unitManager == null) { Debug.LogWarning("[VisibilitySystem] UnitManager is NULL!"); return; }

        var units = unitManager.GetUnitList();

        int ownCount = 0, enemyCount = 0, visibleEnemyCount = 0;

        foreach (var unit in units)
        {
            if (!unit) continue;

            int unitTeam = GetTeamId(unit);
            
            var ni = unit.GetComponent<Mirror.NetworkIdentity>();
            bool isOwn = NetworkSync.IsOwnedHere(ni) || (unitTeam == myTeam); // ← OMISTUS TURVAVERKKO

            if (isOwn)
            {
                ownCount++;
                if (!_visibleNow.Contains(unit))
                {
                    _visibleNow.Add(unit);
                    SetLocallyVisible(unit, true);
                }
                continue;
            }

            enemyCount++;
            var gp = unit.GetGridPosition();
            bool isVisible = tvs.IsVisibleToTeam(myTeam, gp); // ← käytä myTeam
            bool already = _visibleNow.Contains(unit);

            if (isVisible && !already)
            {
                _visibleNow.Add(unit);
                SetLocallyVisible(unit, true);
                visibleEnemyCount++;
            }
            else if (!isVisible && already)
            {
                _visibleNow.Remove(unit);
                SetLocallyVisible(unit, false);
            }
            else if (!isVisible && !already)
            {
                SetLocallyVisible(unit, false);
            }
        }

    }

    private void HideAllEnemiesImmediate()
    {
        var units = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        int hiddenCount = 0;

        foreach (var u in units)
        {
            if (!u) continue;

            int unitTeam = GetTeamId(u);
        
            if (unitTeam == _myTeam)
            {
                continue;
            }

            SetLocallyVisible(u, false);
            _visibleNow.Remove(u);
            hiddenCount++;
        }
    }

    private static void SetLocallyVisible(Unit u, bool visible)
    {
        if (!u) return;
        var lv = u.GetComponent<LocalVisibility>();
        if (!lv) lv = u.gameObject.AddComponent<LocalVisibility>();
        lv.Apply(visible);
    }

    private static int GetTeamId(Unit u) => u.GetTeamId();
}
