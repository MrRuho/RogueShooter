using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GrenadeArcPreview : MonoBehaviour
{
    [SerializeField] private ThrowArcConfig throwArcConfig;
    [SerializeField] private AnimationCurve arcYAnimationCurve; // käytetään jos config puuttuu
    [SerializeField] private Transform origin;                   // tyypillisesti UnitAnimator.ThrowPoint
   // [SerializeField] private float fallbackMaxThrowRangeWU = 12f;
    [SerializeField] private float cellSizeWU = 2f;

    // Katon tunnistus esikatselulle
    [SerializeField] private LayerMask ceilingMask;      // VAIN “Ceiling”-layer
    [SerializeField] private bool clampToCeiling = true; // ON = käytä kakkoskaarta sisällä
    [SerializeField] private float ceilingClearance = 0.08f;

    // Segmenttien rajat
    [SerializeField] private int minSegments = 12;
    [SerializeField] private int maxSegments = 40;

    private LineRenderer _lr;

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.enabled = false;
        if (throwArcConfig != null && throwArcConfig.arcYCurve != null)
            arcYAnimationCurve = throwArcConfig.arcYCurve;
    }

    // Säilytä vanha signatuuri — se delegoi uuteen overloadiin
    public void ShowArcTo(Vector3 targetWorldPos, float maxThrowRangeWU = -1f)
    {
        ShowArcTo(targetWorldPos, null, maxThrowRangeWU);
    }

    // UUSI: mahdollistaa myös apex-override’n (jos joskus haluat syöttää sen suoraan)
    public void ShowArcTo(Vector3 targetWorldPos, float? apexOverrideWU, float maxThrowRangeWU = -1f)
    {
        if (origin == null || _lr == null)
        {
            Debug.LogWarning("[GrenadeArcPreview] Ei originia tai LineRendereriä.");
            return;
        }

        _lr.enabled = true;

        Vector3 start = origin.position;
        Vector3 end = targetWorldPos;

        // Vaakasuora etäisyys
        Vector2 s = new Vector2(start.x, start.z);
        Vector2 e = new Vector2(end.x, end.z);
        float dWU = Vector2.Distance(s, e);

        // Käytä KÄYTÄNNÖSSÄ annettua maxThrowRangeWU:ta, jos se on > 0
        float farWU = (maxThrowRangeWU > 0f) ? maxThrowRangeWU
                                            : (throwArcConfig != null ? throwArcConfig.farRangeWU : dWU);

        // Segmenttien määrä
        int segs = (throwArcConfig != null)
            ? Mathf.Clamp(throwArcConfig.EvaluateSegments(dWU, Mathf.Max(0.01f, cellSizeWU)), minSegments, maxSegments)
            : Mathf.Clamp(12 + Mathf.RoundToInt(dWU / Mathf.Max(0.01f, cellSizeWU)) * 4, minSegments, maxSegments);

        // Apeksin valinta: 1) suora override 2) katto-clamp 3) normaali
        float apexWU;
        if (apexOverrideWU.HasValue)
        {
            apexWU = Mathf.Max(0f, apexOverrideWU.Value);
        }
        else if (clampToCeiling && ceilingMask.value != 0)
        {
            // Kattoa vasten clampattu “kakkoskaari”
            apexWU = ArcApexSolver.ComputeCeilingClampedApex(
                start, end, throwArcConfig, ceilingMask,
                ceilingClearance: ceilingClearance, samples: segs
            );
        }
        else
        {
            // Normaali kaari
            apexWU = (throwArcConfig != null)
                ? throwArcConfig.EvaluateApex(dWU, farWU) // HUOM: käytä farWU’tä (aiemmin jäi hyödyntämättä)
                : Mathf.Lerp(7f, 1.2f, Mathf.Clamp01(dWU / 12f));
        }

        // Piirrä kaari
        _lr.positionCount = segs + 1;
        var curve = (throwArcConfig && throwArcConfig.arcYCurve != null)
            ? throwArcConfig.arcYCurve
            : arcYAnimationCurve;

        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;

            Vector3 p = Vector3.Lerp(start, end, t);
            float baselineY = Mathf.Lerp(start.y, end.y, t);
            float yArc = curve.Evaluate(t) * apexWU;

            p.y = baselineY + yArc;
            _lr.SetPosition(i, p);
        }
    }
    
    public void Hide() => _lr.enabled = false;

    // Aseta nämä dynaamisesti Actionista tarvittaessa:
    public void SetOrigin(Transform t) => origin = t;
    public void SetCellSize(float size) => cellSizeWU = size;
}
