using System.Collections.Generic;
using UnityEngine;
using static GridSystemVisual;

[DisallowMultipleComponent]
public class UnitVision : MonoBehaviour
{
    [Header("Config")]
    public UnitArchetype visionSkill;
    public int teamId = 0;

    private Unit _unit;
    private Transform _tr;
    private HashSet<GridPosition> _lastVisibleTiles = new();
    private int _unitKey;
    private bool _initialized = false;
    private int _currentTeamId = 0;
    private HealthSystem _healthSystem;

    void Awake()
    {
        _tr = transform;
        _unit = GetComponent<Unit>();
        _healthSystem = GetComponent<HealthSystem>();
        _unitKey = GetInstanceID();
    }

    void OnEnable()
    {
        if (_healthSystem != null)
            _healthSystem.OnDying += HandleUnitDying;
    }

    void Start()
    {
        if (!_initialized)
            StartCoroutine(Co_AutoInitLocal());
    }

    void OnDisable()
    {
        if (_healthSystem != null)
            _healthSystem.OnDying -= HandleUnitDying;
        
        CleanupVision();
    }

    private void HandleUnitDying(object sender, System.EventArgs e)
    {
        CleanupVision();

         // UUSI: poista pysyvä OW-maalaus ja watcher-lista
        GridSystemVisual.Instance.RemovePersistentOverwatch(_unit);
        StatusCoordinator.Instance.RemoveWatcher(_unit);
    }

    private void CleanupVision()
    {
        if (TeamVisionService.Instance != null && _initialized)
        {
            TeamVisionService.Instance.RemoveUnitVision(teamId, _unitKey);
        }
        _lastVisibleTiles.Clear();
    }

    private System.Collections.IEnumerator Co_AutoInitLocal()
    {
        yield return null;

        if (_initialized) yield break;
        if (NetMode.IsDedicatedServer) yield break;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            int? team = _unit.GetTeamID();

            if (team != null)
            {
                if (_unit == null) _unit = GetComponent<Unit>();
                InitializeVision(team.Value, _unit ? _unit.archetype : visionSkill);
                yield break;
            }

            yield return null;
        }
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
        // KRIITTINEN: Älä päivitä visionia jos Unit on kuolemassa tai kuollut
        if (_unit != null && (_unit.IsDying() || _unit.IsDead()))
            return;
        
        if (_healthSystem != null && (_healthSystem.IsDying() || _healthSystem.IsDead()))
            return;

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

        var levelGrid = LevelGrid.Instance;
        var loSConfig = LoSConfig.Instance;
        var teamVision = TeamVisionService.Instance;

        if (levelGrid == null || loSConfig == null || teamVision == null)
        {
            Debug.LogWarning($"[UnitVision UPDATE SKIP] {name} - missing services: LG={levelGrid != null}, CFG={loSConfig != null}, TVS={teamVision != null}");
            return;
        }

        var wp = _tr != null ? _tr.position : transform.position;
        var origin = levelGrid.GetGridPosition(wp);

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
                loSConfig.losBlockersMask, loSConfig.eyeHeight, loSConfig.samplesPerCell, loSConfig.insetWU,
                ignoreRoot: _tr
            );
        }
        else
        {
            vis = RaycastVisibility.ComputeVisibleTilesRaycast(
                origin, visionSkill.visionRange,
                loSConfig.losBlockersMask, loSConfig.eyeHeight, loSConfig.samplesPerCell, loSConfig.insetWU
            );
        }

        _lastVisibleTiles = vis ?? _lastVisibleTiles;


        teamVision.ReplaceUnitVision(teamId, _unitKey, _lastVisibleTiles);    
    }

    private bool ShouldPublishVisionLocally()
    {
        if (NetworkSync.IsOffline) return true;
        if (!NetworkSync.IsClient) return false;

        var ni = NetworkSync.FindIdentity(this.GetActorId());
        return NetworkSync.IsOwnedHere(ni);
    }

    public HashSet<GridPosition> GetUnitVisionGrids()
    {
        return _lastVisibleTiles;
    }

    public void ShowUnitPersonalVision()
    {
        var tiles = GetUnitVisionGrids();                  // HashSet<GridPosition> (näkyvät ruudut)
        if (tiles == null) return;

        GridSystemVisual.Instance.ShowGridPositionList(
            new List<GridPosition>(tiles),                 // tai suoraan tiles, jos metodi ottaa IEnumerable
            GridVisualType.UnitPersonalVision              // aseta tämä kenttä/parametri oikein
        );
    }

    public void ShowUnitOverWachVision(Vector3 facingWorld, float coneAngleDeg)
    {
        var tiles = GetConeVisibleTiles(facingWorld, coneAngleDeg);
        if (tiles == null || tiles.Count == 0) return;

        // Käytä erillistä tyyppiä, jota EI pyyhitä valinnan vaihtuessa
         GridSystemVisual.Instance.AddPersistentOverwatch(_unit, tiles);
    }

    public List<GridPosition> GetConeVisibleTiles(Vector3 facingWorld, float coneAngleDeg /*, int rangeTiles = 0*/)
    {
        // 1) Hae oma näkyvyys-cache (se sama jota nyt käytät)
        var visible = GetUnitVisionGrids(); // HashSet<GridPosition> (=_lastVisible)
        var levelGrid = LevelGrid.Instance;
        var res = new List<GridPosition>();
        if (visible == null || visible.Count == 0 || levelGrid == null) return res;

        // 2) Litteä suuntavektori (XZ)
        Vector2 f = new Vector2(facingWorld.x, facingWorld.z);
        if (f.sqrMagnitude < 1e-6f)
            f = new Vector2(transform.forward.x, transform.forward.z);
        f.Normalize();

        float cosHalf = Mathf.Cos(0.5f * coneAngleDeg * Mathf.Deg2Rad);
        int myFloor = _unit.GetGridPosition().floor;
        Vector3 myPos = transform.position;

        // 3) Käy näkyvät ruudut läpi ja pidä vain kulmaan mahtuvat
        foreach (var gp in visible)
        {
            if (gp.floor != myFloor) continue; // pidä kerros kurissa, jos teillä on kerrokset

            Vector3 wp = levelGrid.GetWorldPosition(gp);
            Vector2 to = new Vector2(wp.x - myPos.x, wp.z - myPos.z);
            float len2 = to.sqrMagnitude;
            if (len2 < 1e-6f) { res.Add(gp); continue; } // oma ruutu

            Vector2 toN = to / Mathf.Sqrt(len2);
            float dot = Vector2.Dot(f, toN);
            if (dot >= cosHalf)
                res.Add(gp);
        }

        return res;
    }

    public float GetDynamicConeAngle(int apLeft, float overwatchAngleDeg = 80f)
    {
        if (apLeft >= 3) return 360f;
        if (apLeft == 2) return 360f - overwatchAngleDeg; // “leikkautuu perästä yhden OW-kartiollisen verran”
        if (apLeft == 1) return 180f;
        return overwatchAngleDeg;                          // 0 AP → sama kuin OW
    }

    public bool IsTileInCone(Vector3 facingWorld, float coneAngleDeg, GridPosition gp)
    {
        var levelGrid = LevelGrid.Instance;
        if (levelGrid == null || _unit == null) return false;

        int myFloor = _unit.GetGridPosition().floor;
        if (gp.floor != myFloor) return false;

        Vector3 myPos = transform.position;
        Vector3 wp = levelGrid.GetWorldPosition(gp);

        Vector2 f = new Vector2(facingWorld.x, facingWorld.z);
        if (f.sqrMagnitude < 1e-6f) f = new Vector2(transform.forward.x, transform.forward.z);
        f.Normalize();

        Vector2 to = new Vector2(wp.x - myPos.x, wp.z - myPos.z);
        float len2 = to.sqrMagnitude;
        if (len2 < 1e-6f) return true;

        float cosHalf = Mathf.Cos(0.5f * coneAngleDeg * Mathf.Deg2Rad);
        Vector2 toN = to / Mathf.Sqrt(len2);
        return Vector2.Dot(f, toN) >= cosHalf;
    }

    public void ApplyAndPublishDirectionalVision(Vector3 facingWorld, float coneAngleDeg)
    {
        var tiles = GetConeVisibleTiles(facingWorld, coneAngleDeg);
        if (tiles == null) return;

        // Päivitä oma julkaistu cache ja työnnä TeamVisioniin
        _lastVisibleTiles = new HashSet<GridPosition>(tiles);
        TeamVisionService.Instance.ReplaceUnitVision(teamId, _unitKey, _lastVisibleTiles);
    }
}
