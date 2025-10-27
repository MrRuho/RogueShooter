using System.Collections.Generic;

public static class LoSBlockerRegistry
{
    // Kuinka monella "tall-blockerilla" ruutu on peitetty
    private static readonly Dictionary<GridPosition, int> _counts = new();

    public static void Reset() => _counts.Clear();

    public static void AddTiles(IEnumerable<GridPosition> tiles)
    {
        foreach (var t in tiles)
        {
            _counts.TryGetValue(t, out int c);
            _counts[t] = c + 1;
        }
    }

    public static void RemoveTiles(IEnumerable<GridPosition> tiles)
    {
        foreach (var t in tiles)
        {
            if (!_counts.TryGetValue(t, out int c)) continue;
            c--; 
            if (c <= 0) _counts.Remove(t);
            else _counts[t] = c;
        }
    }

    public static bool TileHasTallBlocker(GridPosition p)
        => _counts.TryGetValue(p, out int c) && c > 0;
}
