using UnityEngine;

public static class CoverService
{
    public enum CoverType { None, Low, High }
    public enum Dir { N, E, S, W }

    // Suunnan kvadrantti ampujasta kohteeseen
    public static Dir GetIncomingDir(Vector3 shooterWorld, Vector3 targetWorld)
    {
        Vector3 v = shooterWorld - targetWorld;
        v.y = 0;
        if (v.sqrMagnitude < 0.0001f) return Dir.N;  // mielivalta
        v.Normalize();

        // vertaillaan kardinaaleihin
        float dn = Vector3.Dot(v, Vector3.forward); // +Z = N
        float de = Vector3.Dot(v, Vector3.right);   // +X = E
        float ds = Vector3.Dot(v, Vector3.back);    // -Z = S
        float dw = Vector3.Dot(v, Vector3.left);    // -X = W

        // suurin dot voittaa
        if (dn > de && dn > ds && dn > dw) return Dir.N;
        if (de > ds && de > dw) return Dir.E;
        if (ds > dw) return Dir.S;
        return Dir.W;
    }

    public static CoverType GetCoverTypeAt(PathNode node, Dir dir)
    {
        switch (dir)
        {
            case Dir.N: if (node.HasHighCover(CoverMask.N)) return CoverType.High; if (node.HasLowCover(CoverMask.N)) return CoverType.Low; break;
            case Dir.E: if (node.HasHighCover(CoverMask.E)) return CoverType.High; if (node.HasLowCover(CoverMask.E)) return CoverType.Low; break;
            case Dir.S: if (node.HasHighCover(CoverMask.S)) return CoverType.High; if (node.HasLowCover(CoverMask.S)) return CoverType.Low; break;
            case Dir.W: if (node.HasHighCover(CoverMask.W)) return CoverType.High; if (node.HasLowCover(CoverMask.W)) return CoverType.Low; break;
        }
        return CoverType.None;
    }

    public static int GetCoverMitigationBase(CoverType t)
        => t == CoverType.High ? 50 : (t == CoverType.Low ? 25 : 0);
    
    public static int GetCoverMitigationPoints(CoverType t)
    {
        int basePts = GetCoverMitigationBase(t);
        return Mathf.RoundToInt(basePts);
    }

}
