using UnityEngine;

public static class ArcApexSolver
{
    /// Laskee apexWU:n, joka MAHTUU katon alle: min( nominaaliApex, kattoCap )
    public static float ComputeCeilingClampedApex(
        Vector3 start, Vector3 end,
        ThrowArcConfig cfg,
        LayerMask ceilingMask,
        float ceilingClearance = 0.08f,   // jätä hiukan ilmaa kaaren ja katon väliin
        int samples = 16,
        float? farWUOverride = null,
        AnimationCurve fallback = null,
        float targetAboveClearance = 0.05f )
    {
        // 1) nominaali apex (ulkona)
        float dWU = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z));

        // Käytä callerin antamaa farWU:ta jos tulee (esim. _maxRangeWU), muuten assetin cfg.farRangeWU
        float farWU = farWUOverride ?? (cfg != null ? cfg.farRangeWU : dWU);

        float apexNominal = (cfg != null)
        ? cfg.EvaluateApex(dWU, farWU)        // ← ennen käytit aina cfg.farRangeWU
        : Mathf.Lerp(7f, 1.2f, Mathf.Clamp01(dWU / 12f));

        if (apexNominal <= 0f) return 0f;

        var curve = (cfg != null && cfg.arcYCurve != null) ? cfg.arcYCurve : (fallback ?? AnimationCurve.Linear(0,0,1,0));

        // 2) etsi kattoCap: pienin t:ssä sallittu apex = (katonKorkeus - baselineY - clearance)/curve(t)
        float apexCap = float.PositiveInfinity;

        // vältä aivan päitä (ettei osuta lähtöpisteen omaan geometriaan)
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (samples + 1f); // (0,1) sisällä
            Vector3 baseP = Vector3.Lerp(start, end, t);
            float baselineY = Mathf.Lerp(start.y, end.y, t);

            // Ray ylös kattoon
            if (Physics.Raycast(baseP + Vector3.up * 0.01f, Vector3.up, out var hit, 50f, ceilingMask, QueryTriggerInteraction.Collide))
            {
                
                if (hit.point.y <= end.y + targetAboveClearance)
                    continue;
                
                float allowedArc = (hit.point.y - ceilingClearance) - baselineY;
                float c = Mathf.Max(1e-4f, curve.Evaluate(t)); // kaaren suhteellinen korkeustermi
                float localCap = allowedArc / c;
                if (localCap < apexCap) apexCap = localCap;
            }
        }

        if (float.IsPositiveInfinity(apexCap))
            return apexNominal; // ei kattoa reitillä

        // 3) lopullinen apex = pienempi näistä
        return Mathf.Max(0f, Mathf.Min(apexNominal, apexCap));
    }
}
