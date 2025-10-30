
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisibilitySystem : MonoBehaviour
{
    [Tooltip("Kuinka usein polletaan varmuuden vuoksi (s) — pitää myös vihollisen omaan liikkeeseen reagoimisen ilman eventtejä.")]
    [SerializeField] private float pollInterval = 0.25f;

    [Tooltip("Piilota kaikki viholliset pelin alussa ja näytä vain jos vision osuu.")]
    [SerializeField] private bool hideEnemiesOnStart = true;

    private readonly HashSet<Unit> _visibleNow = new HashSet<Unit>();
    private int _myTeam;
    private bool _didInitialBaseline;

    void Awake()
    {
        // Sama tiimimäärittely kuin muualla (host=0, client=1 Versuksessa; muuten 0).
        _myTeam = NetworkSync.GetLocalPlayerTeamId(GameModeManager.SelectedMode);
    }

    void OnEnable()
    {
        var tvs = TeamVisionService.Instance;
        if (tvs != null)
            tvs.OnTeamVisionChanged += HandleTeamVisionChanged;

        StartCoroutine(Co_Poll());
    }

    void OnDisable()
    {
        var tvs = TeamVisionService.Instance;
        if (tvs != null)
            tvs.OnTeamVisionChanged -= HandleTeamVisionChanged;

        StopAllCoroutines();
        _visibleNow.Clear();
        _didInitialBaseline = false;
    }

    // Reagoi vain oman tiimin näkyvyysmuutoksiin
    private void HandleTeamVisionChanged(int teamId)
    {
        if (teamId == _myTeam)
            RefreshVisibleEnemies();
    }

    private IEnumerator Co_Poll()
    {
        // Odota 1 frame jotta kaikki unitit ehtivät spawnata
        yield return null;

        if (hideEnemiesOnStart && !_didInitialBaseline)
        {
            HideAllEnemiesImmediate();
            _didInitialBaseline = true;
        }

        // Alustava näkyvyys (tuo näkyviin ne, jotka ovat jo visionissa)
        RefreshVisibleEnemies();

        var wait = new WaitForSeconds(pollInterval);
        while (true)
        {
            RefreshVisibleEnemies(); // kevyt varmistus (react myös vihollisen omaan liikkeeseen)
            yield return wait;
        }
    }

    private void RefreshVisibleEnemies()
    {
        var tvs = TeamVisionService.Instance;
        if (tvs == null) return;

        // Hae kaikki Unitit ilman sorttausta
        var units = UnitManager.Instance.GetUnitList();

        foreach (var u in units)
        {
            if (!u) continue;

            // Omat aina näkyvissä
            if (GetTeamId(u) == _myTeam)
            {
                // Jos baseline piilotti vahingossa omia (esim. tiimi vaihtui ennen tätä), nosta näkyviin
                if (!_visibleNow.Contains(u))
                    SetLocallyVisible(u, true);

                continue;
            }

            // Viholliset: näkyviin vain jos oman tiimin visionissa
            var gp = u.GetGridPosition();
            bool isVisible = tvs.IsVisibleToTeam(_myTeam, gp);

            bool already = _visibleNow.Contains(u);

            if (isVisible && !already)
            {
                _visibleNow.Add(u);
                SetLocallyVisible(u, true);
                Debug.Log($"[VisionDBG] Team {_myTeam} SEES enemy '{u.name}' at {gp}.");
            }
            else if (!isVisible && already)
            {
                _visibleNow.Remove(u);
                SetLocallyVisible(u, false);
                // Jos haluat login myös häviämisestä:
                // Debug.Log($"[VisionDBG] Team {_myTeam} LOST enemy '{u.name}' at {gp}.");
            }
            else if (!isVisible && !already)
            {
                // Baseline: varmista että tuoreet/respawnatut viholliset pysyvät piilossa
                SetLocallyVisible(u, false);
            }
        }
    }

    private void HideAllEnemiesImmediate()
    {
        var units = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (var u in units)
        {
            if (!u) continue;
            if (GetTeamId(u) == _myTeam) continue;
            SetLocallyVisible(u, false);
            _visibleNow.Remove(u);
        }
    }

    private static void SetLocallyVisible(Unit u, bool visible)
    {
        if (!u) return;
        var lv = u.GetComponent<LocalVisibility>();
        if (!lv) lv = u.gameObject.AddComponent<LocalVisibility>();
        lv.Apply(visible);
    }

    // Projektisi todellinen tiimikenttä
    private static int GetTeamId(Unit u) => u.GetTeamId();
}
