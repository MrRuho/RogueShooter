using System.Collections.Generic;
using UnityEngine;

public static class VisibilityService
{
    // Toleranssi
    private const float EPS = 1e-4f;

    // Välimuisti ruudun "onko korkea blokkeri" -tiedolle
    // Tyhjennä esim. vuoron vaihtuessa tai kun kenttä muuttuu
    private static readonly Dictionary<GridPosition, bool> _tallBlockerCache = new();

    /// <summary>Tyhjennä korkeablokkeri-välimuisti (kutsu esim. vuoron vaihtuessa, kun yksiköt/liikuteltavat esteet liikkuvat tai kun map spawnaa asioita).</summary>
    public static void ResetTallBlockerCache() => _tallBlockerCache.Clear();

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
            if (e2 < dx)  { err += dx; z0 += sz; }
            yield return new GridPosition(x0, z0, from.floor);
        }
    }

#if UNITY_EDITOR
    private static void DebugCheckEdgeSymmetry(GridPosition prev, int dx, int dz)
    {
        // Tarkistetaan vain diagonaaliaskeleella
        if (dx == 0 || dz == 0) return;

        var horiz = dx > 0 ? EdgeMask.E : EdgeMask.W;
        var mid   = new GridPosition(prev.x + dx, prev.z, prev.floor);
        var vert  = dz > 0 ? EdgeMask.N : EdgeMask.S;

        // Vastareunat naapurisoluista
        var neighborH = new GridPosition(prev.x + dx, prev.z, prev.floor);
        var oppH      = dx > 0 ? EdgeMask.W : EdgeMask.E;

        var neighborV = new GridPosition(mid.x, mid.z + dz, mid.floor);
        var oppV      = dz > 0 ? EdgeMask.S : EdgeMask.N;

        bool hA = EdgeOcclusion.HasTallWall(prev,      horiz);
        bool hB = EdgeOcclusion.HasTallWall(neighborH, oppH);
        bool vA = EdgeOcclusion.HasTallWall(mid,       vert);
        bool vB = EdgeOcclusion.HasTallWall(neighborV, oppV);

        if (hA != hB || vA != vB)
        {
            Debug.LogWarning(
                $"[LoS] EdgeOcclusion ei näytä olevan symmetrinen diagonaaliaskeleella.\n" +
                $"  H: ({prev.x},{prev.z},f{prev.floor}).{horiz}={hA}  vs  " +
                $"({neighborH.x},{neighborH.z},f{neighborH.floor}).{oppH}={hB}\n" +
                $"  V: ({mid.x},{mid.z},f{mid.floor}).{vert}={vA}  vs  " +
                $"({neighborV.x},{neighborV.z},f{neighborV.floor}).{oppV}={vB}"
            );
        }
    }
#endif

    public static bool HasLineOfSight(GridPosition from, GridPosition to, bool occludeByUnits = true)
    {
        if (from.floor != to.floor) return false;

        var lg = LevelGrid.Instance;
        if (lg == null) return false;

        // Early-out: LoS itseensä
        if (from.Equals(to)) return true;

        bool first = true;
        GridPosition prev = default;

        foreach (var p in Line(from, to))
        {
            if (first) { first = false; prev = p; continue; }

            // 1) Kokoruutu-korkeat esteet blokkaavat "pehmeästi" kuten ennenkin
            if (LoSBlockerRegistry.TileHasTallBlocker(p))
            {
#if UNITY_EDITOR
                Debug.Log($"[LoS] Blokattu FULL-TILE esteestä ruudussa ({p.x},{p.z},f{p.floor})  reitillä ({from.x},{from.z})->({to.x},{to.z})");
#endif
                return false;
            }

            // 2) Ohuet seinät: tarkista Bresenham-askeleen ylittämä(t) reuna(t)
            int dx = p.x - prev.x;
            int dz = p.z - prev.z;

#if UNITY_EDITOR
            // Tarkista bake-symmetria diagonaaliaskeleella (vain editorissa)
            DebugCheckEdgeSymmetry(prev, dx, dz);
#endif

            if (dx != 0)
            {
                var horiz = dx > 0 ? EdgeMask.E : EdgeMask.W;
                if (EdgeOcclusion.HasTallWall(prev, horiz))
                {
#if UNITY_EDITOR
                    Debug.Log($"[LoS] Blokattu OHUEN seinän HORIZ reuna: prev=({prev.x},{prev.z},f{prev.floor}) edge={horiz}  reitillä ({from.x},{from.z})->({to.x},{to.z})");
#endif
                    return false;
                }

                // Diagonaalisteppi: ylitetään myös pystyreuna "väliruudusta"
                if (dz != 0)
                {
                    var mid = new GridPosition(prev.x + dx, prev.z, prev.floor);
                    var vert = dz > 0 ? EdgeMask.N : EdgeMask.S;
                    if (EdgeOcclusion.HasTallWall(mid, vert))
                    {
#if UNITY_EDITOR
                        Debug.Log($"[LoS] Blokattu OHUEN seinän VERT reuna: mid=({mid.x},{mid.z},f{mid.floor}) edge={vert}  reitillä ({from.x},{from.z})->({to.x},{to.z})");
#endif
                        return false;
                    }
                }
            }
            else if (dz != 0)
            {
                var vert = dz > 0 ? EdgeMask.N : EdgeMask.S;
                if (EdgeOcclusion.HasTallWall(prev, vert))
                {
#if UNITY_EDITOR
                    Debug.Log($"[LoS] Blokattu OHUEN seinän VERT reuna: prev=({prev.x},{prev.z},f{prev.floor}) edge={vert}  reitillä ({from.x},{from.z})->({to.x},{to.z})");
#endif
                    return false;
                }
            }

            // 3) Välissä seisovat unitit voivat edelleen blokata (valinnainen)
            if (occludeByUnits && !p.Equals(to) && lg.HasAnyUnitOnGridPosition(p))
            {
#if UNITY_EDITOR
                Debug.Log($"[LoS] Blokattu UNITIN vuoksi ruudussa ({p.x},{p.z},f{p.floor})  reitillä ({from.x},{from.z})->({to.x},{to.z})");
#endif
                return false;
            }

            prev = p;
        }

        return true;
    }
}
