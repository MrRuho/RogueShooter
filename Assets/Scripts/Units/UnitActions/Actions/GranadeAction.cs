using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GranadeAction : BaseAction
{
    [Header("Preview")]
    [SerializeField] private GrenadeArcPreview arcPreview; // LineRenderer + GrenadeArcPreview
    [SerializeField] private float cellSizeWU = 2f;        // esim. LevelGrid.Instance.CellSize
    [SerializeField] private Transform throwPoint;         // esim. UnitAnimator.rightHandTransform (optional)
    [SerializeField] private ThrowArcConfig throwArcConfig; // sama assetti kuin projektiililla ja preview'lla
    [SerializeField] private LayerMask arcBlockMask;        // esim. "Environment" tms. (EI Units)
    [SerializeField] private int minSegments = 12;
    [SerializeField] private int maxSegments = 36;


    // --- LASKEUTUMISEN SÄÄDÖT ---
    [SerializeField] private LayerMask landingObstacleMask;   // esim. Obstacles (ruudussa olevat laatikot yms.)
    [SerializeField] private float maxLandingObstacleHeight = 0.35f; // kuinka korkea "matala este" voi olla (WU)
    [SerializeField] private float landingBoxShrink = 0.49f;  // 0.45–0.49: ettei nappaa naapuriruutua
    [SerializeField] private float landingBoxHalfY = 0.75f;   // ruudun yläpuolelle ulottuva haku (WU)

    // --- ARC SÄÄDÖT (jos et jo määrittänyt) ---
    [SerializeField] private float grenadeRadius = 0.12f;     // kranaatin "paksuus" törmäystesteille
    [SerializeField] private float arcLift = 0.2f;            // pieni nosto, ettei suutele lattiaa
    [SerializeField] private float heightClearance = 0.05f;   // toleranssi esteen yläreunaan


    // (Prefabi-viite saa jäädä, mutta tätä tiedostoa ei käytetä spawniin)
    [SerializeField] private Transform grenadeProjectilePrefab;

    [SerializeField] private LayerMask mousePlaneMask;   // vain MousePlane-kerrokset
    [SerializeField] private float mousePlaneHalfThickness = 0.05f; // pystysuunnan puolipaksuus
    [SerializeField] private float mousePlaneYOffset = 0.01f;      // pieni nosto keskelle laatikkoa

    [SerializeField] private LayerMask ceilingMask;

    public event EventHandler ThrowGranade;
    public event EventHandler ThrowReady;

    public Vector3 TargetWorld { get; private set; }

    private GridPosition? _lastHover;
   // private bool _wasActive;  // reunan tunnistus
    private bool _wasSelected;

    protected override void Awake()
    {
        base.Awake();
        // Jos throwPointia ei ole asetettu inspectorissa, yritetään hakea UnitAnimatorilta
        if (throwPoint == null)
        {
            var anim = GetComponentInChildren<UnitAnimator>(true);
            if (anim != null && anim.GetrightHandTransform() != null)
                throwPoint = anim.GetrightHandTransform();
        }

        // Alusta preview'n perusasetukset kerran
        if (arcPreview != null)
        {
            arcPreview.SetOrigin(throwPoint != null ? throwPoint : transform);
            arcPreview.SetCellSize(cellSizeWU);
        }
    }
    void OnEnable()  => GrenadeProjectile.OnAnyGranadeExploded += OnGrenadeEnded;

    

    private void OnDisable()
    {
        GrenadeProjectile.OnAnyGranadeExploded -= OnGrenadeEnded;
        if(arcPreview != null)
        {
            arcPreview.Hide();
        }
        _lastHover = null; 
     //   _wasActive = false;
    }

    private void Update()
    {
        // 0) Onko tämä action tällä hetkellä valittuna?
        bool isSelected =
            UnitActionSystem.Instance != null &&
            UnitActionSystem.Instance.GetSelectedAction() == this;

        // 1) Reunan tunnistus valituksi tulossa/poistumassa
        if (isSelected && !_wasSelected)
        {
            // hae Core-scenessä oleva preview, jos viite puuttuu
            if (arcPreview == null)
                arcPreview = FindFirstObjectByType<GrenadeArcPreview>(FindObjectsInactive.Exclude);

            // hae heittopiste, jos puuttuu
            if (throwPoint == null)
            {
                var anim = GetComponentInChildren<UnitAnimator>(true);
                if (anim != null && anim.GetrightHandTransform() != null)
                    throwPoint = anim.GetrightHandTransform();
            }

            if (arcPreview != null)
            {
                arcPreview.SetOrigin(throwPoint != null ? throwPoint : transform);
                arcPreview.SetCellSize(cellSizeWU);
            }

            _lastHover = null; // pakota eka päivitys
        }
        else if (!isSelected && _wasSelected)
        {
            arcPreview?.Hide();
            _lastHover = null;
        }
        _wasSelected = isSelected;

        if (!isSelected) return;

        // 2) Päivitä viiva vain, kun hover-ruutu vaihtuu ja ruutu on validi
        GridPosition gp = LevelGrid.Instance.GetGridPosition(MouseWorld.GetMouseWorldPosition());
        if (!LevelGrid.Instance.IsValidGridPosition(gp))
        {
            arcPreview?.Hide();
            _lastHover = null;
            return;
        }

        if (_lastHover.HasValue && _lastHover.Value == gp) return;
        _lastHover = gp;

        // 3) Piirrä kaari
        Vector3 targetW = LevelGrid.Instance.GetWorldPosition(gp);
        arcPreview?.ShowArcTo(targetW);
    }

    public override string GetActionName() => "Granade";

    private void OnGrenadeEnded(object sender, EventArgs e)
    {
        GetValidGridPositionList();            // teillä jo oleva metodi
        GridSystemVisual.Instance?.UpdateGridVisuals();
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
        => new EnemyAIAction { gridPosition = gridPosition, actionValue = 0 };


    public override List<GridPosition> GetValidGridPositionList()
    {
        unit ??= GetComponent<Unit>();
        var result = new List<GridPosition>();
        if (unit == null || LevelGrid.Instance == null) return result;

        int floorsUp = 2;      // montako ylös skannataan
        int floorsDown = 2;    // montako alas (0 jos et halua)

        // Heittopiste
        Transform originT = throwPoint;
        if (originT == null)
        {
            var anim = GetComponentInChildren<UnitAnimator>(true);
            if (anim != null && anim.GetrightHandTransform() != null)
                originT = anim.GetrightHandTransform();
        }
        if (originT == null) originT = transform;
        Vector3 origin = originT.position;

        GridPosition unitGP = unit.GetGridPosition();
        int rangeTiles = unit.archetype.throwingRange;

        var topmostByXZ = new Dictionary<string, GridPosition>();

        for (int dx = -rangeTiles; dx <= rangeTiles; dx++)
        for (int dz = -rangeTiles; dz <= rangeTiles; dz++)
        {
            int cost = SircleCalculator.Sircle(dx, dz);
            if (cost > 10 * rangeTiles) continue;

            for (int df = -floorsDown; df <= floorsUp; df++)
            {
                var test = new GridPosition(unitGP.x + dx, unitGP.z + dz, unitGP.floor + df);
                if (!LevelGrid.Instance.IsValidGridPosition(test)) continue;

                // 1) VAATIMUS: ruudussa täytyy olla MousePlane ko. kerroksessa
                if (!HasMousePlaneAt(test)) continue;
                
                // 2) kohderuudun laskeutuminen ok?
                if (!CheckDestinationClearance(test, origin)) continue;

                // 2) ARC-CHECK vasta sen jälkeen (säästää fysiikkaa)
                Vector3 targetW = LevelGrid.Instance.GetWorldPosition(test);

                int segs;
                    if (throwArcConfig != null)
                    {
                        float d = Vector2.Distance(new Vector2(origin.x, origin.z), new Vector2(targetW.x, targetW.z));
                        segs = Mathf.Clamp(throwArcConfig.EvaluateSegments(d, Mathf.Max(0.01f, cellSizeWU)), minSegments, maxSegments);
                    }
                    else segs = minSegments;
                
                float apexClamped = ArcApexSolver.ComputeCeilingClampedApex(
                    origin, targetW, throwArcConfig, ceilingMask,
                    ceilingClearance: 0.08f, samples: segs);

                bool clear = ArcVisibility.IsArcClear(
                    start: origin,
                    end: targetW,
                    cfg: throwArcConfig,
                    segments: segs,
                    mask: arcBlockMask,
                    ignoreRoot: (throwPoint ? throwPoint : transform).root,
                    lift: 0.2f,
                    capsuleRadius: throwArcConfig.GetCapsuleRadius(cellSizeWU),
                    heightClearance: 0.05f,
                    tStart: 0.02f,
                    tEnd: 0.95f,
                    cellSizeWU: cellSizeWU,
                    fullTilePerc: 0.8f,
                    tallWallY: 0.6f,
                    apexOverrideWU: apexClamped 
                );
                    if (!clear) continue;
                
                
                string key = $"{test.x},{test.z},{test.floor}";
                if (!topmostByXZ.ContainsKey(key))
                    topmostByXZ[key] = test;
                
            }
        }

        foreach (var kv in topmostByXZ) result.Add(kv.Value);
        return result;
    }

    private bool HasMousePlaneAt(GridPosition gp)
    {
        Vector3 center = LevelGrid.Instance.GetWorldPosition(gp);
        // laatikko hieman pienempi kuin laatta, ettei “nuolaise” naapuria
        Vector3 halfExtents = new Vector3(cellSizeWU * 0.49f, mousePlaneHalfThickness, cellSizeWU * 0.49f);
        center.y += mousePlaneYOffset; // varmuus: korkeudessa pieni toleranssi

        // Collide: lasketaan myös trigger-MousePlane't
        return Physics.CheckBox(center, halfExtents, Quaternion.identity, mousePlaneMask, QueryTriggerInteraction.Collide);
    }

    private bool CheckDestinationClearance(GridPosition gp, Vector3 origin)
    {
        Vector3 center = LevelGrid.Instance.GetWorldPosition(gp);
        float floorY = center.y;

        Vector3 half = new Vector3(cellSizeWU * landingBoxShrink, landingBoxHalfY, cellSizeWU * landingBoxShrink);
        var cols = Physics.OverlapBox(center + Vector3.up * (landingBoxHalfY * 0.5f),
                                    half,
                                    Quaternion.identity,
                                    landingObstacleMask,
                                    QueryTriggerInteraction.Collide);

        if (cols == null || cols.Length == 0)
            return true; // tyhjä ruutu → ok

        float maxTopRel = 0f;
        bool hasAllowingOverride = false;

        foreach (var c in cols)
        {
            if (!c) continue;
            float topRel = c.bounds.max.y - floorY;
            if (topRel > maxTopRel) maxTopRel = topRel;
        }

        // Perussääntö korkeudelle
        bool heightOK = (maxTopRel <= maxLandingObstacleHeight) || hasAllowingOverride;
        if (!heightOK) return false;

        // BONUS: jos ruudussa oli JOTAIN (maxTopRel > 0), tarkista myös laskeutumiskulma
        if (maxTopRel > 0.01f && throwArcConfig != null)
        {
            float angleDeg = ArcMath.ComputeDescentAngleDeg(
                origin, center, throwArcConfig, lift: arcLift);

            if (angleDeg < throwArcConfig.minLandingAngleDegForObstacles)
                return false; // liian loiva → ei laskeuduta esteen päälle
        }

        return true;
    }
    
    public float GetMaxThrowRangeWU()
    {
        // Yritä käyttää ThrowArcConfigin farRangeWU:ta jos se on asetettu
        if (throwArcConfig != null && throwArcConfig.farRangeWU > 0f)
            return throwArcConfig.farRangeWU;

        // Muuten: tiles * cellsize (oma cellSizeWU toimii varana jos LevelGrid null)
        float cs = (LevelGrid.Instance != null) ? LevelGrid.Instance.GetCellSize() : cellSizeWU;
        var u = unit != null ? unit : GetComponent<Unit>();
        int tiles = (u != null && u.archetype != null) ? u.archetype.throwingRange : 6;
        return tiles * cs;
    }


    // 2) Kutsukääre radan tarkastukselle (käyttää jo teillä olevaa ArcVisibilityä)
    private bool CheckArcClear(Vector3 start, Vector3 end, int segs)
    {
        // Huom: leikataan loppukärkeä hieman (tEnd=0.95), jotta laskeutumisruudun esineet
        // eivät blokkaa rataa — niiden arvio tekee CheckDestinationClearance erikseen.
        return ArcVisibility.IsArcClear(
            start: start,
            end: end,
            cfg: throwArcConfig,
            segments: segs,
            mask: arcBlockMask,                 // Walls/Ceilings/Thick obstacles (EI Units)
            ignoreRoot: transform,
            lift: arcLift,
            capsuleRadius: grenadeRadius,
            heightClearance: heightClearance,
            tStart: 0.02f,
            tEnd: 0.95f
        );
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        GetUnit().UseGrenade();
        ActionStart(onActionComplete);

        TargetWorld = LevelGrid.Instance.GetWorldPosition(gridPosition);

        // Päätös tehty → piilota preview nyt (spawn ja animaatiot tapahtuvat muualla kuten ennen)
        if (arcPreview != null) arcPreview.Hide();

        StartCoroutine(TurnAndThrow(0.5f, TargetWorld));
    }

    public override int GetActionPointsCost()
    {
        return 2;
    }

    private IEnumerator TurnAndThrow(float delay, Vector3 targetWorld)
    {
        float waitAfterAligned = 0.1f;
        float alignedTime = 0f;
        
        // "Deadman Switch" Varmistaa heiton tietyn ajan kuluttua vaikka kääntyminen kohti kohdetta ei olisi täydellinen.
        float maxAlignTime = 1.5f;
        float elapsed = 0f;
        
        while (true)
        {
            bool aligned = RotateTowards(targetWorld, 750);
            if (aligned)
            {
                alignedTime += Time.deltaTime;
                if (alignedTime >= waitAfterAligned) break;
            }
            else
            {
                alignedTime = 0f;

                elapsed += Time.deltaTime;
                if (elapsed >= maxAlignTime)
                {
                    Debug.LogWarning("[Grenade] Align timeout → throwing anyway.");
                    break;
                }
            }
            yield return null;
        }

        // Täsmälleen sama eventti kuin ennen — UnitAnimator hoitaa näkyvyydet ja AE-triggerin
        ThrowGranade?.Invoke(this, EventArgs.Empty);
    }

    public void OnGrenadeBehaviourComplete()
    {
        ThrowReady?.Invoke(this, EventArgs.Empty);
        ActionComplete();
    }
}
