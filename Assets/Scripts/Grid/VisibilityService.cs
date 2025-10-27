using System.Collections.Generic;
using UnityEngine;

public static class VisibilityService
{

    // Kynnyksen voi myöhemmin lukea EdgeBakerista, nyt selkeä vakio
    private const float LOS_BLOCK_HEIGHT_Y = 1.7f;

    // Välimuisti ruudun "onko korkea blokkeri" -tiedolle
    // Tyhjennä esim. vuoron vaihtuessa tai kun kenttä muuttuu
    private static readonly Dictionary<GridPosition, bool> _tallBlockerCache = new();

    /// <summary>
    /// Palauttaa näkyvät ruudut (sama floor) originista maxRangeen.
    /// Estäjät: 1) koko-ruudun korkeat esteet (PF: !walkable), 2) välissä seisovat unitit.
    /// Ei käytä EdgeBakereita tässä vaiheessa.
    /// </summary>
    public static HashSet<GridPosition> ComputeVisibleTiles(GridPosition origin, int maxRange, bool occludeByUnits = true)
    {
        var visible = new HashSet<GridPosition>();
        var lg = LevelGrid.Instance;
        var pf = PathFinding.Instance;
        if (lg == null || pf == null) return visible;

        for (int dx = -maxRange; dx <= maxRange; dx++)
        {
            for (int dz = -maxRange; dz <= maxRange; dz++)
            {
                var cost = SircleCalculator.Sircle(dx, dz);
                if (cost > 10 * maxRange) continue;

                var gp = new GridPosition(origin.x + dx, origin.z + dz, origin.floor);
                if (!lg.IsValidGridPosition(gp)) continue;

                if (HasLineOfSight(origin, gp, occludeByUnits))
                    visible.Add(gp);
            }
        }
        return visible;
    }

    /// <summary>
    /// Palauttaa true, jos ruudussa on Obstacles-layerin kollidereita,
    /// joiden "top" ylittää kynnyskorkeuden ruudun pohjasta laskien.
    /// </summary>
    private static bool TileHasTallBlocker(GridPosition p)
    {
        if (_tallBlockerCache.TryGetValue(p, out bool cached))
            return cached;

        var lg = LevelGrid.Instance;
        if (lg == null) return false;

        // Ruudun "pohja" – käytä samaa metodia, millä sijoitat unittien y:n
        Vector3 basePos = lg.GetWorldPosition(p);     // ruudun keskipiste pohjatasolla
        float baseY = basePos.y;

        // Kapea laatikko ruudun alueelle, korkeus riittää useimpiin esteisiin
        float half = lg.GetCellSize() * 0.5f * 0.9f;
        Vector3 halfExtents = new(half, 3.0f, half);  // 6 m kokonaiskorkeus
        Vector3 center = basePos + Vector3.up * halfExtents.y;

        // Vain Obstacles-layer
        int obstaclesMask = LayerMask.GetMask("Obstacles");

        var cols = Physics.OverlapBox(
            center, halfExtents, Quaternion.identity,
            obstaclesMask, QueryTriggerInteraction.Ignore
        );

        float maxTopAboveBase = 0f;
        for (int i = 0; i < cols.Length; i++)
        {
            // Kuinka korkealle kollideri ulottuu tämän ruudun "pohjasta" mitattuna
            float topRel = cols[i].bounds.max.y - baseY;
            if (topRel > maxTopAboveBase) maxTopAboveBase = topRel;
        }

        bool isTall = maxTopAboveBase >= LOS_BLOCK_HEIGHT_Y;
        _tallBlockerCache[p] = isTall;
        return isTall;
    }

/// <summary>Tyhjennä korkeablokkeri-välimuisti (kutsu esim. vuoron vaihtuessa, kun yksiköt/liikuteltavat esteet liikkuvat tai kun map spawnaa asioita).</summary>
public static void ResetTallBlockerCache() => _tallBlockerCache.Clear();


    /// <summary>
    /// Näkötesti ruuturajojen yli. Ei käytä fysiikkaa; nojaa PF:n nodeihin ja unit-miehitykseen.
    /// </summary>
    public static bool HasLineOfSight(GridPosition from, GridPosition to, bool occludeByUnits = true)
    {
        if (from.floor != to.floor) return false; // pidetään helppona tässä vaiheessa

        var lg = LevelGrid.Instance;
        var pf = PathFinding.Instance;
        if (lg == null || pf == null) return false;

        GridPosition prev = from;

        foreach (var p in Line(from, to))
        {
            if (p.Equals(from))
            {
                prev = p;
                continue;
            }
            // lähtöruutu ei estä
            if (!lg.IsValidGridPosition(p)) return false;
            
            if (CrossesTallEdge(prev, p))
                return false;

            if (LoSBlockerRegistry.TileHasTallBlocker(p))
                return false;

            // 2) Välissä seisova unit estää LoS:n (mutta kohderuutu saa sisältää unittinsa)
            if (occludeByUnits && !p.Equals(to) && lg.HasAnyUnitOnGridPosition(p))
                return false;
            
            prev = p;
        }
        return true;
    }

    /// <summary>
    /// Bresenham 2D viiva ruutujen (x,z) yli; floor säilyy samana.
    /// </summary>
    private static IEnumerable<GridPosition> Line(GridPosition from, GridPosition to)
    {
        int x0 = from.x, z0 = from.z;
        int x1 = to.x, z1 = to.z;

        int dx = Mathf.Abs(x1 - x0);
        int dz = Mathf.Abs(z1 - z0);
        int sx = x0 < x1 ? 1 : -1;
        int sz = z0 < z1 ? 1 : -1;
        int err = dx - dz;

        // mukaan myös lähtö
        yield return from;

        while (x0 != x1 || z0 != z1)
        {
            int e2 = 2 * err;
            if (e2 > -dz) { err -= dz; x0 += sx; }
            if (e2 < dx) { err += dx; z0 += sz; }
            yield return new GridPosition(x0, z0, from.floor);
        }
    }
    
    // Jos haluat sallivamman kulmapeekkaamisen, aseta false (blokkaa vasta kun MOLEMMAT reunat ovat TallWall)
    private const bool DIAGONAL_BLOCK_IF_ANY = true;

    /// <summary>
    /// Palauttaa true, jos siirtymä ruudusta 'a' ruutuun 'b' ylittää TallWall-reunan.
    /// Käyttää EdgeOcclusion.HasTallWall -dataa (N/E/S/W-bitit per reuna).
    /// </summary>
    private static bool CrossesTallEdge(GridPosition a, GridPosition b)
    {
        int dx = b.x - a.x;
        int dz = b.z - a.z;
        if (dx == 0 && dz == 0) return false;

        // Kardinaaliset askeleet (Bresenham tuottaa yksiaskeleisia steppejä)
        if (dx ==  1 && dz ==  0) return EdgeOcclusion.HasTallWall(a, EdgeMask.E);
        if (dx == -1 && dz ==  0) return EdgeOcclusion.HasTallWall(a, EdgeMask.W);
        if (dx ==  0 && dz ==  1) return EdgeOcclusion.HasTallWall(a, EdgeMask.N);
        if (dx == 0 && dz == -1) return EdgeOcclusion.HasTallWall(a, EdgeMask.S);

        // Diagonaalit: tarkista kaksi mahdollista reunaa kulmassa
        
        if (dx == 1 && dz == 1)
        {
            bool e = EdgeOcclusion.HasTallWall(a, EdgeMask.E);
            bool n = EdgeOcclusion.HasTallWall(a, EdgeMask.N);  // Muutettu: käytä ruutua 'a'
            return DIAGONAL_BLOCK_IF_ANY ? (e || n) : (e && n);
        }
        if (dx == 1 && dz == -1)
        {
            bool e = EdgeOcclusion.HasTallWall(a, EdgeMask.E);
            bool s = EdgeOcclusion.HasTallWall(a, EdgeMask.S);  // Muutettu: käytä ruutua 'a'
            return DIAGONAL_BLOCK_IF_ANY ? (e || s) : (e && s);
        }
        if (dx == -1 && dz == 1)
        {
            bool w = EdgeOcclusion.HasTallWall(a, EdgeMask.W);
            bool n = EdgeOcclusion.HasTallWall(a, EdgeMask.N);  // Muutettu: käytä ruutua 'a'
            return DIAGONAL_BLOCK_IF_ANY ? (w || n) : (w && n);
        }
        if (dx == -1 && dz == -1)
        {
            bool w = EdgeOcclusion.HasTallWall(a, EdgeMask.W);
            bool s = EdgeOcclusion.HasTallWall(a, EdgeMask.S);  // Muutettu: käytä ruutua 'a'
            return DIAGONAL_BLOCK_IF_ANY ? (w || s) : (w && s);
        }
           
        // Turvakäsittely: jos askel olisi jostain syystä > 1 (ei pitäisi tapahtua Bresenhamilla),
        // pilko se yksiaskeleisiin ja tarkista rekursiivisesti.
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
        var prev = a;
        int stepx = Mathf.Clamp(dx, -1, 1);
        int stepz = Mathf.Clamp(dz, -1, 1);
        for (int i = 1; i <= steps; i++)
        {
            var mid = new GridPosition(a.x + stepx * i, a.z + stepz * i, a.floor);
            if (CrossesTallEdge(prev, mid)) return true;
            prev = mid;
        }
        return false;
    }

}
