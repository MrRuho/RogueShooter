using System.Collections.Generic;
using UnityEngine;

public static class VisibilityService
{

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
            if (e2 < dx) { err += dx; z0 += sz; }
            yield return new GridPosition(x0, z0, from.floor);
        }
    }
 
    public static bool HasLineOfSight(GridPosition from, GridPosition to, bool occludeByUnits = true)
    {
        if (from.floor != to.floor) return false;

        var lg = LevelGrid.Instance;
        if (lg == null) return false;

        // 1) Tarkista geometrisesti kaikki tall wall reunat jotka linja voisi leikata
        if (!GeometricLineOfSightCheck(from, to))
            return false;

        // 2) Tarkista LoSBlocker-registry (täysikokoiset esteet)
        foreach (var p in Line(from, to))
        {
            if (p.Equals(from)) continue;
            if (!lg.IsValidGridPosition(p)) return false;

            if (LoSBlockerRegistry.TileHasTallBlocker(p))
                return false;

            // Välissä seisova unit estää
            if (occludeByUnits && !p.Equals(to) && lg.HasAnyUnitOnGridPosition(p))
                return false;
        }

        return true;
    }

    // Geometrinen tarkistus: käy läpi kaikki ruudut joiden läpi linja kulkee
    // ja tarkista leikkaako linja niiden tall wall reunoja
    private static bool GeometricLineOfSightCheck(GridPosition from, GridPosition to)
    {
        var lg = LevelGrid.Instance;
        if (lg == null) return true;

        float cell = lg.GetCellSize();

        Vector3 fromWorld = lg.GetWorldPosition(from);
        Vector3 toWorld = lg.GetWorldPosition(to);

        Vector2 lineStart = new Vector2(fromWorld.x, fromWorld.z);
        Vector2 lineEnd = new Vector2(toWorld.x, toWorld.z);

        // Käy läpi kaikki ruudut AABB:n sisällä jota linja koskettaa
        int minX = Mathf.Min(from.x, to.x);
        int maxX = Mathf.Max(from.x, to.x);
        int minZ = Mathf.Min(from.z, to.z);
        int maxZ = Mathf.Max(from.z, to.z);

        // Laajenna yhden ruudun verran varmuuden vuoksi
        minX -= 1;
        maxX += 1;
        minZ -= 1;
        maxZ += 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                var gp = new GridPosition(x, z, from.floor);
                if (!lg.IsValidGridPosition(gp)) continue;

                // Tarkista kaikki 4 reunaa tästä ruudusta
                if (EdgeOcclusion.HasTallWall(gp, EdgeMask.N))
                {
                    if (LineIntersectsEdge(lineStart, lineEnd, gp, EdgeMask.N, cell))
                        return false;
                }
                if (EdgeOcclusion.HasTallWall(gp, EdgeMask.E))
                {
                    if (LineIntersectsEdge(lineStart, lineEnd, gp, EdgeMask.E, cell))
                        return false;
                }
                if (EdgeOcclusion.HasTallWall(gp, EdgeMask.S))
                {
                    if (LineIntersectsEdge(lineStart, lineEnd, gp, EdgeMask.S, cell))
                        return false;
                }
                if (EdgeOcclusion.HasTallWall(gp, EdgeMask.W))
                {
                    if (LineIntersectsEdge(lineStart, lineEnd, gp, EdgeMask.W, cell))
                        return false;
                }
            }
        }

        return true;
    }

    private static bool IsSameOrientationContinuationAtEndpoint(GridPosition c, EdgeMask edge, int endpoint)
    {
        var lg = LevelGrid.Instance;
        GridPosition nb;

        switch (edge)
        {
            case EdgeMask.E:
                // E-reuna jatkuu “ylös/alas” eli z-suunnassa: p1 = south end (z-1), p2 = north end (z+1)
                nb = (endpoint == 1) ? new GridPosition(c.x, c.z - 1, c.floor)
                                    : new GridPosition(c.x, c.z + 1, c.floor);
                return lg.IsValidGridPosition(nb) && EdgeOcclusion.HasTallWall(nb, EdgeMask.E);

            case EdgeMask.W:
                nb = (endpoint == 1) ? new GridPosition(c.x, c.z - 1, c.floor)
                                    : new GridPosition(c.x, c.z + 1, c.floor);
                return lg.IsValidGridPosition(nb) && EdgeOcclusion.HasTallWall(nb, EdgeMask.W);

            case EdgeMask.N:
                // N-reuna jatkuu “oikea/vasen” eli x-suunnassa: p1 = west end (x-1), p2 = east end (x+1)
                nb = (endpoint == 1) ? new GridPosition(c.x - 1, c.z, c.floor)
                                    : new GridPosition(c.x + 1, c.z, c.floor);
                return lg.IsValidGridPosition(nb) && EdgeOcclusion.HasTallWall(nb, EdgeMask.N);

            case EdgeMask.S:
                nb = (endpoint == 1) ? new GridPosition(c.x - 1, c.z, c.floor)
                                    : new GridPosition(c.x + 1, c.z, c.floor);
                return lg.IsValidGridPosition(nb) && EdgeOcclusion.HasTallWall(nb, EdgeMask.S);
        }
        return false;
    }

    // Asetukset: jos haluat sallia corner-peekin (pelkkä kulmapiste EI blokkaa), vaihda false -> true.
    private static bool ALLOW_CORNER_PEEK = true;

    private static bool LineIntersectsEdge(Vector2 lineStart, Vector2 lineEnd, GridPosition cell, EdgeMask edge, float cellSize)
    {
        var lg = LevelGrid.Instance;
        Vector3 cellWorld = lg.GetWorldPosition(cell);
        float half = cellSize * 0.5f;

        Vector2 edgeP1, edgeP2;
        // HUOM: p1/p2 on määritelty niin, että tiedämme kumpi pää on “etelä/ länsi” vs “pohjoinen/ itä”
        switch (edge)
        {
            case EdgeMask.N:
                edgeP1 = new Vector2(cellWorld.x - half, cellWorld.z + half); // west end
                edgeP2 = new Vector2(cellWorld.x + half, cellWorld.z + half); // east end
                break;
            case EdgeMask.E:
                edgeP1 = new Vector2(cellWorld.x + half, cellWorld.z - half); // south end
                edgeP2 = new Vector2(cellWorld.x + half, cellWorld.z + half); // north end
                break;
            case EdgeMask.S:
                edgeP1 = new Vector2(cellWorld.x - half, cellWorld.z - half); // west end
                edgeP2 = new Vector2(cellWorld.x + half, cellWorld.z - half); // east end
                break;
            case EdgeMask.W:
                edgeP1 = new Vector2(cellWorld.x - half, cellWorld.z - half); // south end
                edgeP2 = new Vector2(cellWorld.x - half, cellWorld.z + half); // north end
                break;
            default:
                return false;
        }

        // Otetaan talteen, osuiko osuma jompaan kumpaan päätepisteeseen
        int endpointHit;
        if (!LineSegmentsIntersectWithEndpoint(lineStart, lineEnd, edgeP1, edgeP2, out endpointHit))
            return false; // ei osumaa → ei blokkaa

        // Osuttiin reunan SISÄLLE → blokkaa aina
        if (endpointHit == 0) return true;

        // Osuttiin päätepisteeseen
        // Jos corner-peek on kytketty, salli VAIN jos päätepiste EI ole “jatkuva seinä” samaan suuntaan
        if (ALLOW_CORNER_PEEK)
        {
            if (IsSameOrientationContinuationAtEndpoint(cell, edge, endpointHit))
                return true;   // seinä jatkuu tästä → BLOKKAA
            else
                return false;  // todellinen päätykulma → SALLI
        }

        return true;
    }

    // Toleranssi
    private const float EPS = 1e-4f;

    // Palauttaa true jos segmentit leikkaavat.
    // endpointHit: 0 = ei päätepistettä, 1 = p3 (edgeP1), 2 = p4 (edgeP2)
    private static bool LineSegmentsIntersectWithEndpoint(
        Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out int endpointHit)
    {
        endpointHit = 0;

        float x1 = p1.x, y1 = p1.y;
        float x2 = p2.x, y2 = p2.y;
        float x3 = p3.x, y3 = p3.y;
        float x4 = p4.x, y4 = p4.y;

        float denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);

        // Kolineaarinen / lähes kolineaarinen
        if (Mathf.Abs(denom) < EPS)
        {
            // Onko kolineaarinen (p3 on p1->p2 -linjalla)?
            float cross = (x3 - x1) * (y2 - y1) - (y3 - y1) * (x2 - x1);
            if (Mathf.Abs(cross) > EPS)
                return false; // rinnakkainen mutta eri viiva

            // Limittyvätkö projektiossa? Jos kyllä, tulkitaan osumaksi (blokkaus),
            // ja endpoint1/2:ksi voidaan merkitä 0 (sisäosuma) — ei corner-peekiä
            bool overlap;
            if (Mathf.Abs(x1 - x2) >= Mathf.Abs(y1 - y2))
            {
                float a1 = Mathf.Min(x1, x2), a2 = Mathf.Max(x1, x2);
                float b1 = Mathf.Min(x3, x4), b2 = Mathf.Max(x3, x4);
                overlap = (a1 <= b2 + EPS) && (b1 <= a2 + EPS);
            }
            else
            {
                float a1 = Mathf.Min(y1, y2), a2 = Mathf.Max(y1, y2);
                float b1 = Mathf.Min(y3, y4), b2 = Mathf.Max(y3, y4);
                overlap = (a1 <= b2 + EPS) && (b1 <= a2 + EPS);
            }
            if (!overlap) return false;
            endpointHit = 0; // käsitellään “sisäosumana”
            return true;
        }

        // Yleinen tapaus
        float t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        float u = ((x1 - x3) * (y1 - y2) - (y1 - y3) * (x1 - x2)) / denom;

        bool inside = (t >= -EPS && t <= 1f + EPS && u >= -EPS && u <= 1f + EPS);
        if (!inside) return false;

        // Päätepisteosuma?
        bool atP3 = (u <= EPS);
        bool atP4 = (u >= 1f - EPS);

        if (atP3) { endpointHit = 1; return true; }
        if (atP4) { endpointHit = 2; return true; }

        // Osuma reunan sisällä
        endpointHit = 0;
        return true;
    }
}
