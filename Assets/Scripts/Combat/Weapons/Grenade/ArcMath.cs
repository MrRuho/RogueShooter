using UnityEngine;

public static class ArcMath
{
    public static float ComputeDescentAngleDeg(
        Vector3 start, Vector3 end,
        ThrowArcConfig cfg,
        float lift = 0f,
        AnimationCurve fallback = null)
    {
        // Sama apex ja käyrä kuin projektiilissa/previewssa
        float dWU = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z));
        float apexWU = (cfg != null) ? cfg.EvaluateApex(dWU, cfg.farRangeWU) : Mathf.Lerp(7f, 1.2f, Mathf.Clamp01(dWU / 12f));
        var curve = (cfg != null && cfg.arcYCurve != null) ? cfg.arcYCurve : (fallback ?? AnimationCurve.Linear(0,0,1,0));

        float tA = cfg ? Mathf.Clamp01(cfg.angleSampleT1) : 0.92f;
        float tB = cfg ? Mathf.Clamp01(cfg.angleSampleT2) : 0.98f;

        Vector3 pA = GetArcPoint(start, end, curve, apexWU, tA, lift);
        Vector3 pB = GetArcPoint(start, end, curve, apexWU, tB, lift);

        float dy = pA.y - pB.y; // positiivinen = laskeutuu alaspäin
        float dPlanar = Vector2.Distance(new Vector2(pA.x, pA.z), new Vector2(pB.x, pB.z));
        if (dPlanar < 1e-4f) return 90f; // käytännössä pystysuora

        return Mathf.Atan2(Mathf.Abs(dy), dPlanar) * Mathf.Rad2Deg;
    }

    private static Vector3 GetArcPoint(Vector3 start, Vector3 end, AnimationCurve curve, float apexWU, float t, float lift)
    {
        Vector3 p = Vector3.Lerp(start, end, t);
        float baselineY = Mathf.Lerp(start.y, end.y, t);
        p.y = baselineY + curve.Evaluate(t) * apexWU + lift;
        return p;
    }
}
