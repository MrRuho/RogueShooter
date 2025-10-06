using Unity.VisualScripting;
using UnityEngine;

[System.Flags]
public enum EdgeMask { None = 0, N = 1, E = 2, S = 4, W = 8 }

[System.Flags]
public enum CoverMask { None=0, N=1, E=2, S=4, W=8 }
public class PathNode
{
    private GridPosition gridPosition;
    private int gCost;
    private int hCost;
    private int fCost;
    private PathNode cameFromPathNode;

    private bool isWalkable = true;
    private EdgeMask walls; // ← ruudun reunaesteet
    private CoverMask highCover;      // täyskorkea suoja suunnittain
    private CoverMask lowCover;       // matala suoja suunnittain

    /*
    public void AddWall(EdgeMask dir) => walls |= dir;
    public void RemoveWall(EdgeMask dir) => walls &= ~dir;
    public bool HasWall(EdgeMask dir) => (walls & dir) != 0;
    public void ClearWalls() => walls = EdgeMask.None;
    */
    public void ClearWalls() => walls = EdgeMask.None;
    public void AddWall(EdgeMask dir) => walls |= dir;
    public bool HasWall(EdgeMask dir) => (walls & dir) != 0;

    public void ClearCover() { highCover = CoverMask.None; lowCover = CoverMask.None; }
    public void AddHighCover(CoverMask d) => highCover |= d;
    public void AddLowCover(CoverMask d) => lowCover |= d;

    public bool HasHighCover(CoverMask d) => (highCover & d) != 0;
    public bool HasLowCover(CoverMask d) => (lowCover & d) != 0;

    public CoverMask GetHighCoverMask() => highCover;
    public CoverMask GetLowCoverMask() => lowCover;

    public PathNode(GridPosition gridPosition)
    {
        this.gridPosition = gridPosition;
    }

    public int LastGenerationID { get; private set; } = -1;
    public void MarkGeneration(int generationID) => LastGenerationID = generationID;

    public override string ToString()
    {
        return gridPosition.ToString();
    }

    public int GetGCost()
    {
        return gCost;
    }

    public int GetHCost()
    {
        return hCost;
    }

    public int GetFCost()
    {
        return fCost;
    }

    public void SetGCost(int gCost)
    {
        this.gCost = gCost;
    }

    public void SetHCost(int hCost)
    {
        this.hCost = hCost;
    }

    public void CalculateFCost()
    {
        fCost = gCost + hCost;
    }

    public void ResetCameFromPathNode()
    {
        cameFromPathNode = null;
    }

    public void SetCameFromPathNode(PathNode pathNode)
    {
        cameFromPathNode = pathNode;
    }

    public PathNode GetCameFromPathNode()
    {
        return cameFromPathNode;
    }

    public GridPosition GetGridPosition()
    {
        return gridPosition;
    }

    public bool GetIsWalkable()
    {
        return isWalkable;
    }

    public void SetIsWalkable(bool isWalkable)
    {
        this.isWalkable = isWalkable;
    }

    public bool IsWalkable()
    {
        return isWalkable;
    }

}
