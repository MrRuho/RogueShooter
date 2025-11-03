using UnityEngine;

public static class ArcApexSolver
{
    public static float ComputeCeilingClampedApex(
        Vector3 start, Vector3 end,
        ThrowArcConfig cfg,
        LayerMask ceilingMask,
        float ceilingClearance = 0.08f,
        int samples = 16,
        float? farWUOverride = null,
        AnimationCurve fallback = null,
        float targetAboveClearance = 0.05f )
    {
        float dWU = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z));
        float farWU = farWUOverride ?? (cfg != null ? cfg.farRangeWU : dWU);

        float apexNominal = (cfg != null)
        ? cfg.EvaluateApex(dWU, farWU)
        : Mathf.Lerp(7f, 1.2f, Mathf.Clamp01(dWU / 12f));

        if (apexNominal <= 0f) return 0f;

        var curve = (cfg != null && cfg.arcYCurve != null) ? cfg.arcYCurve : (fallback ?? AnimationCurve.Linear(0,0,1,0));

        float maxHeadroom = Mathf.Max(start.y, end.y) + apexNominal;
        
        float apexCap = float.PositiveInfinity;

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (samples + 1f);
            Vector3 baseP = Vector3.Lerp(start, end, t);
            float baselineY = Mathf.Lerp(start.y, end.y, t);

            // Rajoita raycastin pituus: etsi katto vain relevantin alueen sisältä
            float maxRayDistance = Mathf.Max(5f, maxHeadroom - baseP.y + 1f);
            
            if (Physics.Raycast(baseP + Vector3.up * 0.01f, Vector3.up, out var hit, maxRayDistance, ceilingMask, QueryTriggerInteraction.Collide))
            {
                // UUSI EHTO: Hyväksy katto vain jos se on reitin "yläpuolella"
                // eli korkeammalla kuin sekä lähtö- että maalipiste
                float minRelevantHeight = Mathf.Max(start.y, end.y);
                if (hit.point.y <= minRelevantHeight + targetAboveClearance)
                    continue;
                
                float allowedArc = (hit.point.y - ceilingClearance) - baselineY;
                
                // Jos katto on liian matalalla (negatiivinen allowedArc), ohita
                if (allowedArc <= 0f)
                    continue;
                
                float c = Mathf.Max(1e-4f, curve.Evaluate(t));
                float localCap = allowedArc / c;
                if (localCap < apexCap) apexCap = localCap;
            }
        }

        if (float.IsPositiveInfinity(apexCap))
            return apexNominal;

        return Mathf.Max(0f, Mathf.Min(apexNominal, apexCap));
    }
}
