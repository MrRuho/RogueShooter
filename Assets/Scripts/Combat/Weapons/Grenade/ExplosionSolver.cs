using System.Collections.Generic;
using UnityEngine;
using System.Text;

public static class ExplosionSolver
{
    // --- Julkiset debug-säädöt ---
    public static bool Verbose   = false;     // konsoliloki
    public static bool DebugDraw = false;      // gizmo-snapshot ExplosionSolverGizmosille

    // LOS-parametrit
    public static float LoSHeight    = 1.0f;  // säteen / pienen pallon korkeus maailmassa
    public static float LoSRayRadius = 0.00f; // 0 = Linecast, >0 = SphereCast (esim. 0.15f)

    //public static QueryTriggerInteraction TriggerMode = QueryTriggerInteraction.Ignore;
    public static QueryTriggerInteraction TriggerMode = QueryTriggerInteraction.Collide;

    // Obstacle-maskin voi yliajaa ajossa: ExplosionSolver.SetObstacleLayerMask(LayerMask.GetMask("Obstacle"));
    static int _obstacleMask = -1;
    public static void SetObstacleLayerMask(LayerMask m) => _obstacleMask = m.value;
    static int ObstacleMaskInt => _obstacleMask >= 0 ? _obstacleMask : LayerMask.GetMask("Obstacles", "NarrowObstacles", "Units");

    // --- Snapshot gizmoja varten ---
    public sealed class DebugSnapshot
    {
        public GridPosition origin;
        public int radiusTiles;
        public int r2;
        public HashSet<GridPosition> reached = new();
        public HashSet<GridPosition> stops   = new();
        public List<(GridPosition a, GridPosition b)> steps = new();                    // vihreät viivat
        public List<(GridPosition a, GridPosition b, string reason)> blocks = new();    // punaiset viivat
    }
    public static DebugSnapshot LastDebug { get; private set; }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    static void Log(string msg) { if (Verbose) Debug.Log(msg); }

    static string Fmt(in GridPosition gp) => $"({gp.x},{gp.z},F{gp.floor})";

    /// TIUKKA LOS: jokaiselle ruudulle originista suora fysikaalinen törmäystesti Obstacle-layeriin.
    /// Jos osuu, ruutua ei lisätä. Ei kierretä kulmia eikä seiniä.
    public static HashSet<GridPosition> ComputeReach(GridPosition origin, int radiusTiles)
    {
        var lg = LevelGrid.Instance;
        var outSet = new HashSet<GridPosition>();

        if (lg == null)
        {
            Debug.LogWarning("[ExplosionSolver] LevelGrid.Instance is NULL");
            return outSet;
        }

        int r2 = radiusTiles * radiusTiles;
        var snap = DebugDraw ? new DebugSnapshot { origin = origin, radiusTiles = radiusTiles, r2 = r2 } : null;

        // Origin ruutu on aina mukana
        outSet.Add(origin);
        snap?.reached.Add(origin);
 
        var blockedDirs = new HashSet<(int,int)>();

        for (int r = 1; r <= radiusTiles; r++)
        {
            // käydään r-”kehän” reunat (Chebyshev-perimetri)
            // ylä+ala reunat
            for (int dx = -r; dx <= r; dx++)
            {
                TryOne(origin, dx, +r);
                TryOne(origin, dx, -r);
            }
            // vasen+oikea reunat (ilman kulmien duplikaatteja)
            for (int dz = -r+1; dz <= r-1; dz++)
            {
                TryOne(origin, +r, dz);
                TryOne(origin, -r, dz);
            }
        }

        void TryOne(GridPosition o, int dx, int dz)
        {
            if (dx*dx + dz*dz > r2) return;
            var lg = LevelGrid.Instance;
            var gp = new GridPosition(o.x + dx, o.z + dz, o.floor);
            if (!lg.IsValidGridPosition(gp)) return;

            var dirKey = NormDir(dx, dz);
            if (blockedDirs.Contains(dirKey)) return; 
            // jos sama suunta jo blokattu, älä testaa pidemmälle
            // (mutta vain jos osuma oli aiemmin — katso alempaa)
            // Huom: blockedDirs täydennetään VAIN osumasta!

            Vector3 aW = lg.GetWorldPosition(o)  + Vector3.up * LoSHeight;
            Vector3 bW = lg.GetWorldPosition(gp) + Vector3.up * LoSHeight;
            Vector3 dn = (bW - aW).normalized;
            float   dist = Vector3.Distance(aW, bW);

            bool hit; RaycastHit hi;
            int mask = ObstacleMaskInt;
            if (LoSRayRadius > 0f)
                hit = Physics.SphereCast(aW, LoSRayRadius, dn, out hi, dist, mask, TriggerMode);
            else
                hit = Physics.Raycast(aW, dn, out hi, dist, mask, TriggerMode);

            if (hit)
            {
                // Lisää nimenomaan ensimmäinen törmäysruutu, EI gp:tä
                var hgp = lg.GetGridPosition(hi.point);
                var hitTile = new GridPosition(hgp.x, hgp.z, o.floor);

                if (lg.IsValidGridPosition(hitTile))
                {
                    outSet.Add(hitTile);
                    snap?.stops.Add(hitTile);
                    snap?.blocks.Add((o, hitTile, $"HIT {hi.collider.name}"));
                }
                else
                {
                    snap?.stops.Add(gp);
                    snap?.blocks.Add((o, gp, $"HIT {hi.collider.name}"));
                }

                // TÄRKEÄ: blokkaa jatko TÄSSÄ suunnassa vain, jos oikeasti tuli osuma
                blockedDirs.Add(dirKey);
            }
            else
            {
                // EI osumaa → ruutu on saavutettu
                outSet.Add(gp);
                snap?.reached.Add(gp);
                snap?.steps.Add((o, gp));
                // Huom: ÄLÄ lisää dirKeytä blockedDirs:iin tässä, jotta samassa suunnassa
                // voidaan jatkaa seuraavalle kehälle.
            }
        }

        if (Verbose)
        {
            var sb = new StringBuilder();
            sb.Append($"[ExplosionSolver] DONE. Reached={outSet.Count} tiles: ");
            bool first = true;
            foreach (var gp in outSet)
            {
                if (!first) sb.Append(", ");
                sb.Append(Fmt(gp));
                first = false;
            }
            Debug.Log(sb.ToString());
        }

        if (DebugDraw) LastDebug = snap;
        return outSet;
        
    }
    
    static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return Mathf.Abs(a); }
    static (int sx,int sz) NormDir(int dx, int dz)
    {
        int g = Gcd(Mathf.Abs(dx), Mathf.Abs(dz));
        if (g == 0) return (0,0);
        return (dx / g, dz / g);
    }
}
