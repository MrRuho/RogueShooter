using System;
using System.Collections.Generic;


/// <summary>
/// TallWall-occlusion per ruutu: mitkä reunat (N/E/S/W) ovat "korkeita kapeita seiniä".
/// Tallennetaan vain ruudut joissa on vähintään yksi bitti.
/// </summary>
public static class EdgeOcclusion
{
    private static readonly Dictionary<GridPosition, EdgeMask> _tallWalls = new();

    public static void Clear() => _tallWalls.Clear();

    public static bool HasTallWall(GridPosition cell, EdgeMask side)
    {
        if (!_tallWalls.TryGetValue(cell, out var m)) return false;
        return (m & side) != 0;
    }

    public static void AddSymmetric(GridPosition cell, EdgeMask side)
    {
        // aseta celliin
        if (_tallWalls.TryGetValue(cell, out var m)) _tallWalls[cell] = m | side;
        else _tallWalls[cell] = side;

        // aseta myös naapuriin vastakkaiselle reunalle
        var lg = LevelGrid.Instance;
        if (lg == null) return;

        GridPosition n; EdgeMask opposite;
        switch (side)
        {
            case EdgeMask.N: n = new GridPosition(cell.x, cell.z + 1, cell.floor); opposite = EdgeMask.S; break;
            case EdgeMask.E: n = new GridPosition(cell.x + 1, cell.z, cell.floor); opposite = EdgeMask.W; break;
            case EdgeMask.S: n = new GridPosition(cell.x, cell.z - 1, cell.floor); opposite = EdgeMask.N; break;
            case EdgeMask.W: n = new GridPosition(cell.x - 1, cell.z, cell.floor); opposite = EdgeMask.E; break;
            default: return;
        }
        if (!lg.IsValidGridPosition(n)) return;

        if (_tallWalls.TryGetValue(n, out var nm)) _tallWalls[n] = nm | opposite;
        else _tallWalls[n] = opposite;
    }
}
