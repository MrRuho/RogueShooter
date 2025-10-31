using UnityEngine;

[CreateAssetMenu(menuName = "RogueShooter/Throw Arc Config")]
public class ThrowArcConfig : ScriptableObject
{
    [Header("Arc shape (0→1→0, huippu ~0.5)")]
    public AnimationCurve arcYCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 4f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -4f, 0f)
    );

    [Header("Apex (world-yksiköissä)")]
    [Tooltip("Kuinka korkea kaaren huippu on lyhyellä heitolla.")]
    public float apexNearWU = 7f;
    [Tooltip("Kuinka matala kaaren huippu on maksietäisyydellä.")]
    public float apexFarWU  = 1.2f;

    [Header("Smoothing")]
    [Tooltip("0.5-1.0: pienempi → voimakkaampi ero near vs far")]
    [Range(0.25f, 2f)] public float smoothingK = 0.75f;

    [Header("Preview")]
    public int baseSegments = 12;
    public int segmentsPerTile = 4;
    public int minSegments = 12;
    public int maxSegments = 40;

    public float EvaluateApex(float distanceWU, float farRangeWU)
    {
        float x = farRangeWU > 0f ? Mathf.Clamp01(distanceWU / farRangeWU) : 1f;
        // "Käänteinen" smoothstep: lähellä korkea, kaukana matala
        float s = 1f - Mathf.Pow(1f - x, smoothingK);
        return Mathf.Lerp(apexNearWU, apexFarWU, s);
    }

    public int EvaluateSegments(float distanceWU, float cellSizeWU)
    {
        int tiles = Mathf.Max(0, Mathf.RoundToInt(distanceWU / Mathf.Max(0.01f, cellSizeWU)));
        int segs = baseSegments + tiles * segmentsPerTile;
        return Mathf.Clamp(segs, minSegments, maxSegments);
    }
}
