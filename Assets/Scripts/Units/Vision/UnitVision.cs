using System.Collections.Generic;
using UnityEngine;
//using Mirror;

[DisallowMultipleComponent]
public class UnitVision : MonoBehaviour
{
    [Header("Config")]
    public UnitArchetype visionSkill;
    public int teamId = 0;

    private Unit _unit;
    private Transform _tr;
    private HashSet<GridPosition> _lastVisible = new();
    private int _unitKey;
    private bool _initialized = false;
    private int _currentTeamId = 0;

    void Awake()
    {
        _tr = transform;
        _unit = GetComponent<Unit>();
        _unitKey = GetInstanceID();

    }

    void Start()
    {
        // Jos SpawnUnitsCoordinator ehti jo kutsua InitializeVisionia, ei tehdä mitään
        if (!_initialized)
            StartCoroutine(Co_AutoInitLocal());
    }

    void OnDisable()
    {

        if (TeamVisionService.Instance != null)
        {
            TeamVisionService.Instance.RemoveUnitVision(teamId, _unitKey);
        }
        _lastVisible.Clear();
    }

    /// <summary>
    /// Yritä alustaa näkyvyys paikallisesti sillä clientillä, jolle tämä unit kuuluu.
    /// Ei tee mitään dediserverillä eikä vastustajan uniteille.
    /// </summary>
    private System.Collections.IEnumerator Co_AutoInitLocal()
    {
        // Odota 1 frame: varmistetaan että NetworkIdentity/isOwned & LevelGrid ovat valmiit
        yield return null;

        if (_initialized) yield break;

        // Dedicated server: ei ole paikallista pelaajaa tai ruutua → älä tee client-puolen overlay-initialisointia
        // Jos tämä on dedi-serveri, lopeta tähän)
        if (NetMode.IsDedicatedServer) yield break; //NetworkServer.active && !NetworkClient.active

        int? team = ResolveLocalTeamForThisUnit();
        if (team == null) yield break; // ei ole tämän clientin omistama unit

        if (_unit == null) _unit = GetComponent<Unit>();
        // Anna archetype täältä (Unitista) — InitializeVision hoitaa ensimmäisen päivityksen
        InitializeVision(team.Value, _unit ? _unit.archetype : visionSkill);
    }

    /// <summary>
    /// Päättele paikallinen teamId tälle unitille tällä koneella.
    /// Palauttaa 0/1 tai null jos tämä ei ole minun unit (ei alusteta täällä).
    /// </summary>
    private int? ResolveLocalTeamForThisUnit()
    {
        if (NetMode.Offline) return 0;
  
        return NetworkSync.TryResolveLocalTeamForUnit(
            GameModeManager.SelectedMode,
            this.GetActorId()
        );
    }

    public void InitializeVision(int setTeamId, UnitArchetype archetype)
    {
     
        if (_initialized && setTeamId != _currentTeamId && TeamVisionService.Instance != null)
            TeamVisionService.Instance.RemoveUnitVision(_currentTeamId, _unitKey);

        _currentTeamId = setTeamId;
        teamId = setTeamId;
        if (archetype != null) visionSkill = archetype;

        _initialized = true;

        StartCoroutine(Co_DeferredFirstVision());
    }

    private System.Collections.IEnumerator Co_DeferredFirstVision()
    {
        yield return null;
        UpdateVisionNow();
    }

    public void NotifyMoved()
    {
        UpdateVisionNow();
    }

    public bool IsInitialized => _initialized;

    public void UpdateVisionNow()
    {
        if (!ShouldPublishVisionLocally())
            return;

        if (!_initialized)
        {
            Debug.LogWarning($"[UnitVision UPDATE SKIP] {name} - not initialized");
            return;
        }

        if (visionSkill == null)
        {
            Debug.LogWarning($"[UnitVision UPDATE SKIP] {name} - no visionSkill");
            return;
        }

        var lg = LevelGrid.Instance;
        var cfg = LoSConfig.Instance;
        var tvs = TeamVisionService.Instance;

        if (lg == null || cfg == null || tvs == null)
        {
            Debug.LogWarning($"[UnitVision UPDATE SKIP] {name} - missing services: LG={lg != null}, CFG={cfg != null}, TVS={tvs != null}");
            return;
        }

        var wp = _tr != null ? _tr.position : transform.position;
        var origin = lg.GetGridPosition(wp);

        if (_unit != null)
        {
            var uf = _unit.GetGridPosition();
            origin = new GridPosition(origin.x, origin.z, uf.floor);
        }

        HashSet<GridPosition> vis;
        if (visionSkill.useHeightAware)
        {
            vis = RaycastVisibility.ComputeVisibleTilesRaycastHeightAware(
                origin, visionSkill.visionRange,
                cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU,
                ignoreRoot: _tr
            );
        }
        else
        {
            vis = RaycastVisibility.ComputeVisibleTilesRaycast(
                origin, visionSkill.visionRange,
                cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU
            );
        }

        _lastVisible = vis ?? _lastVisible;

        tvs.ReplaceUnitVision(teamId, _unitKey, _lastVisible);
    }

    private bool ShouldPublishVisionLocally()
    {
        // Offline: aina ok
        if (NetworkSync.IsOffline) return true;

        // Online: vain UI:ta omaava prosessi (host/client), ei dediserver
        if (!NetworkSync.IsClient) return false;

        // VERSUS/CO-OP: julkaise vain omistetut unitit tällä koneella,
        // jotta toisen pelaajan unitit eivät koskaan "työnnä" visionia väärälle puolelle
        var ni = NetworkSync.FindIdentity(this.GetActorId());   // ActorIdUtil → uint → NI
        return NetworkSync.IsOwnedHere(ni);
    }
    
}
