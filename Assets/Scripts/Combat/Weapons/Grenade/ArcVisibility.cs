using UnityEngine;

public static class ArcVisibility
{
    /// True jos kaari mahtuu start→end osumatta “riittävän korkeisiin” esteisiin.
    public static bool IsArcClear(
        Vector3 start,
        Vector3 end,
        ThrowArcConfig cfg,
        int segments,
        LayerMask mask,
        Transform ignoreRoot = null,
        float lift = 0.2f,              // pieni nosto irti lattiasta
        float capsuleRadius = 12f,    // “paksuus” (kranaatin säde)
        float heightClearance = 0.05f,  // toleranssi
        float tStart = 0.02f,           // leikkaa alku- ja loppukärjet pois
        float tEnd = 0.98f,
        AnimationCurve fallbackCurve = null,
        float cellSizeWU = 2f,
        float fullTilePerc = 0.8f,
        float tallWallY = 0.6f,
        float? apexOverrideWU = null        // ← UUSI
        )

    {
   
        if (segments < 2) segments = 2;

    // Apex: käytä overridea, muutoin nominaali
        float dWU = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z));
        float apexWU = apexOverrideWU.HasValue
            ? apexOverrideWU.Value
            : (cfg != null ? cfg.EvaluateApex(dWU, cfg.farRangeWU)
                        : Mathf.Lerp(7f, 1.2f, Mathf.Clamp01(dWU / 12f)));

        var curve = (cfg != null && cfg.arcYCurve != null) ? cfg.arcYCurve : (fallbackCurve ?? AnimationCurve.Linear(0,0,1,0));

        Transform root = ignoreRoot ? ignoreRoot.root : null;

        for (int i = 0; i < segments; i++)
        {
            float t0 = Mathf.Lerp(tStart, tEnd,  i      / (float)segments);
            float t1 = Mathf.Lerp(tStart, tEnd, (i + 1) / (float)segments);

            Vector3 p0 = GetArcPoint(start, end, curve, apexWU, t0, lift);
            Vector3 p1 = GetArcPoint(start, end, curve, apexWU, t1, lift);

            // Keskiarvokorkeus päätökseen “ylittääkö”
            float segMidY = 0.5f * (p0.y + p1.y);

            // Hae kaikki collidereiden osumat kapselilla (havaitsee myös “start inside”)
            var hits = Physics.OverlapCapsule(p0, p1, capsuleRadius, mask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) continue;

            foreach (var col in hits)
            {
                if (!col) continue;
                if (root && col.transform.root == root) continue; // ohita oma hahmo

                // Jos esteen yläreuna on segmentin korkeuden tasalla tai yli → blokkaa
                float topY = col.bounds.max.y; // AABB, mutta riittää konservatiiviseksi päätökseksi
                if (topY >= segMidY - heightClearance)
                    return false;
            }
        }

        return true;
    }

    private static Vector3 GetArcPoint(Vector3 start, Vector3 end, AnimationCurve curve, float apexWU, float t, float lift)
    {
        Vector3 p = Vector3.Lerp(start, end, t);
        float baselineY = Mathf.Lerp(start.y, end.y, t);
        p.y = baselineY + curve.Evaluate(t) * apexWU + lift;
        return p;
    }
}
