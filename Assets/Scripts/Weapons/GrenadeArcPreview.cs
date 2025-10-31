using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GrenadeArcPreview : MonoBehaviour
{
    [SerializeField] private ThrowArcConfig throwArcConfig;
    [SerializeField] private AnimationCurve arcYAnimationCurve; // käytetään jos config puuttuu
    [SerializeField] private Transform origin;                   // tyypillisesti UnitAnimator.ThrowPoint
    [SerializeField] private float fallbackMaxThrowRangeWU = 12f;
    [SerializeField] private float cellSizeWU = 2f;

    private LineRenderer _lr;

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.enabled = false;
        if (throwArcConfig != null && throwArcConfig.arcYCurve != null)
            arcYAnimationCurve = throwArcConfig.arcYCurve;
    }

    public void ShowArcTo(Vector3 targetWorldPos, float maxThrowRangeWU = -1f)
    {
        if (origin == null)
        {
            Debug.Log("Ei aloituspistettä, ei kaarta!");
            return;
        }
        
        _lr.enabled = true;

        Vector3 start = origin.position;
        Vector3 end   = targetWorldPos;

        // Vaakasuora etäisyys
        Vector2 s = new Vector2(start.x, start.z);
        Vector2 e = new Vector2(end.x,   end.z);
        float dWU = Vector2.Distance(s, e);

        float farWU = (maxThrowRangeWU > 0f) ? maxThrowRangeWU : fallbackMaxThrowRangeWU;
        float apexWU = (throwArcConfig != null)
            ? throwArcConfig.EvaluateApex(dWU, farWU)
            : Mathf.Lerp(7f, 1.2f, Mathf.Clamp01(dWU / Mathf.Max(0.01f, farWU)));

        int segs = (throwArcConfig != null)
            ? throwArcConfig.EvaluateSegments(dWU, Mathf.Max(0.01f, cellSizeWU))
            : Mathf.Clamp(12 + Mathf.RoundToInt(dWU / Mathf.Max(0.01f, cellSizeWU)) * 4, 12, 40);

        _lr.positionCount = segs + 1;

        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;

            Vector3 p = Vector3.Lerp(start, end, t);
            float baselineY = Mathf.Lerp(start.y, end.y, t);
            float yArc      = (throwArcConfig ? throwArcConfig.arcYCurve : arcYAnimationCurve).Evaluate(t) * apexWU;

            p.y = baselineY + yArc;
            _lr.SetPosition(i, p);
        }
    }

    public void Hide() => _lr.enabled = false;

    // Aseta nämä dynaamisesti Actionista tarvittaessa:
    public void SetOrigin(Transform t) => origin = t;
    public void SetCellSize(float size) => cellSizeWU = size;
}
