using UnityEngine;

public static class CoverService
{
    public enum CoverType { None, Low, High }

    public static int GetCoverMitigationBase(CoverType t)
        => t == CoverType.High ? 50 : (t == CoverType.Low ? 25 : 0);

    public static int GetCoverMitigationPoints(CoverType t)
    {
        int basePts = GetCoverMitigationBase(t);
        return Mathf.RoundToInt(basePts);
    }
    
    public static CoverType EvaluateCoverHalfPlane(GridPosition attacker, GridPosition target, PathNode node)
    {
        
        if (attacker.floor != target.floor) return CoverType.None; // pidä yksinkertaisena

        int dx = attacker.x - target.x;
        int dz = attacker.z - target.z;

        if (node == null) return CoverType.None;

        bool ge = false; // "greater or equal" rajalla?
        bool facesN = ge ? (dz >= 0) : (dz > 0);
        bool facesS = ge ? (dz <= 0) : (dz < 0);
        bool facesE = ge ? (dx >= 0) : (dx > 0);
        bool facesW = ge ? (dx <= 0) : (dx < 0);

        bool high =
            (facesN && node.HasHighCover(CoverMask.N)) ||
            (facesS && node.HasHighCover(CoverMask.S)) ||
            (facesE && node.HasHighCover(CoverMask.E)) ||
            (facesW && node.HasHighCover(CoverMask.W));

        if (high) return CoverType.High;

        bool low =
            (facesN && node.HasLowCover(CoverMask.N)) ||
            (facesS && node.HasLowCover(CoverMask.S)) ||
            (facesE && node.HasLowCover(CoverMask.E)) ||
            (facesW && node.HasLowCover(CoverMask.W));

        return low ? CoverType.Low : CoverType.None;
    }

}
