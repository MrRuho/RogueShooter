using System;
using System.Collections.Generic;
using UnityEngine;


public interface IDynamicLoSBlocker
{
    /// <summary>
    /// Palauta true jos tämä objekti blokkaa LoS:n silmäkorkeudella (worldY).
    /// </summary>
    bool IsBlockingAtHeight(float heightWorldY);
}


public static class RaycastVisibility
{
    private const float EPS = 0.01f;
    private static readonly RaycastHit[] _hitBuf1 = new RaycastHit[1];

    public enum LoSAcceptance
    {
        Any,                // nykyinen “yksi riittää” (ei suositella)
        CenterOnly,         // vain keskisäde ratkaisee (reilu & tiukka)
        CenterAndAny,       // keskisäde + väh. yksi muu
        Majority,           // väh. 3/5 selvä (offsetit “pehmentää”)
        All                 // kaikki 5 läpi (tosi tiukka)
    }

    /// <summary>
    /// Yksinkertainen LoS: vain losBlockersMask (korkeat seinät tms.) blokkaa.
    /// samplesPerCell: 1 = nopea (keskipiste), 5 = “risti” (kulmasuoja pienenee).
    /// insetWU siirtää kohdepisteitä hieman ruudun sisään, jotta reunaräpsyt vähenee.
    /// </summary>
    public static bool HasLineOfSightRaycast(
        GridPosition from, GridPosition to,
        LayerMask losBlockersMask,
        float eyeHeight = 1.6f,
        int samplesPerCell = 1,
        float insetWU = 0.30f)
    {
        if (from.floor != to.floor) return false;

        var lg = LevelGrid.Instance;
        if (lg == null) return false;

        Vector3 a = lg.GetWorldPosition(from) + Vector3.up * eyeHeight;
        Vector3 center = lg.GetWorldPosition(to) + Vector3.up * eyeHeight;

        if (samplesPerCell <= 1)
            return RayClear(a, center, losBlockersMask);

        // 5-pisteen “risti”: center ±right ±forward
        Vector3 dir = (center - a).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;

        Vector3[] targets = new[]
        {
            center,
            center + right   * insetWU,
            center - right   * insetWU,
            center + forward * insetWU,
            center - forward * insetWU,
        };

        // Yksi “onnistunut” säde riittää näyttämään ruudun näkyvänä.
        // Jos haluat “täysin näkyvä ruutu” -säännön, vaadi että kaikki läpäisee.
        foreach (var t in targets)
            if (RayClear(a, t, losBlockersMask)) return true;

        return false;
    }

    public static HashSet<GridPosition> ComputeVisibleTilesRaycast(
        GridPosition origin,
        int maxRange,
        LayerMask losBlockersMask,
        float eyeHeight = 1.6f,
        int samplesPerCell = 1,
        float insetWU = 0.30f)
    {
        var set = new HashSet<GridPosition>();
        var lg = LevelGrid.Instance;
        if (lg == null) return set;

        for (int dx = -maxRange; dx <= maxRange; dx++)
            for (int dz = -maxRange; dz <= maxRange; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                var gp = origin + new GridPosition(dx, dz, 0);
                if (!lg.IsValidGridPosition(gp)) continue;

                int cost = SircleCalculator.Sircle(dx, dz);
                if (cost > 10 * maxRange) continue;

                if (HasLineOfSightRaycast(origin, gp, losBlockersMask, eyeHeight, samplesPerCell, insetWU))
                    set.Add(gp);
            }
        return set;
    }

    private static bool RayClear(Vector3 a, Vector3 b, LayerMask mask)
    {
        Vector3 d = b - a;
        float L = d.magnitude;
        if (L <= EPS) return true;
        int hits = Physics.RaycastNonAlloc(a, d / L, _hitBuf1, L - EPS, mask, QueryTriggerInteraction.Ignore);
        return hits == 0;
    }

    public static bool HasLineOfSightRaycastHeightAware(
        GridPosition from, GridPosition to,
        LayerMask losMask,
        float eyeHeight = 1.6f,
        int samplesPerCell = 1,
        float insetWU = 0.30f,
        Transform ignoreRoot = null,
        LoSAcceptance acceptance = LoSAcceptance.CenterOnly
    )
    {
        if (from.floor != to.floor) return false;

        var lg = LevelGrid.Instance;
        if (lg == null) return false;

        Vector3 a = lg.GetWorldPosition(from) + Vector3.up * eyeHeight;
        Vector3 center = lg.GetWorldPosition(to) + Vector3.up * eyeHeight;

        if (samplesPerCell <= 1)
            return RayClearHeightAware(a, center, losMask, ignoreRoot);

        Vector3 dir = (center - a).normalized;
        Vector3 right   = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 forward = Vector3.Cross(right,   Vector3.up).normalized;

        Vector3[] targets = new[]
        {
            center,
            center + right   * insetWU,
            center - right   * insetWU,
            center + forward * insetWU,
            center - forward * insetWU,
        };

        int clearCount = 0;
        bool centerClear = false;

        for (int i = 0; i < targets.Length; i++)
        {
            bool ok = RayClearHeightAware(a, targets[i], losMask, ignoreRoot);
            if (ok) { clearCount++; if (i == 0) centerClear = true; }
        }

        switch (acceptance)
        {
            case LoSAcceptance.CenterOnly:   return centerClear;
            case LoSAcceptance.CenterAndAny: return centerClear && clearCount >= 2;
            case LoSAcceptance.Majority:     return clearCount >= 3;     // 3/5
            case LoSAcceptance.All:          return clearCount == targets.Length;
            default:                         return clearCount > 0;      // Any
        }
    }

    private static bool RayClearHeightAware(Vector3 a, Vector3 b, LayerMask mask, Transform ignoreRoot)
    {
        Vector3 d = b - a;
        float L = d.magnitude;
        if (L <= EPS) return true;

        // Kerää kaikki osumat ja käy läpi lähimmästä kaukaisimpaan
        var hits = Physics.RaycastAll(a, d / L, L - EPS, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return true;

        Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

        float eyeY = a.y;

        foreach (var h in hits)
        {
            if (ignoreRoot != null && h.transform != null && h.transform.IsChildOf(ignoreRoot))
                continue;

            // Jos objektissa on oma “älykäs” looginen tarkistin, käytä sitä
            var dyn = h.collider.GetComponentInParent<IDynamicLoSBlocker>();
            if (dyn != null)
            {
                if (dyn.IsBlockingAtHeight(eyeY))
                    return false; // blokkaa
                else
                    continue;     // ei blokkaa → jatka seuraavaan osumaan
            }

            // Muuten: käytä colliderin ylärajaa
            var topY = h.collider.bounds.max.y;
            if (topY >= eyeY - 0.01f)
                return false; // tarpeeksi korkea → blokkaa
            // Muuten matala → ei blokkaa, jatka
        }

        // Yksikään osuma ei ollut “tarpeeksi korkea”
        return true;
    }
    
    public static HashSet<GridPosition> ComputeVisibleTilesRaycastHeightAware(
        GridPosition origin,
        int maxRange,
        LayerMask losMask,
        float eyeHeight = 1.6f,
        int samplesPerCell = 1,
        float insetWU = 0.30f,
        Transform ignoreRoot = null)
    {
        var set = new HashSet<GridPosition>();
        var lg  = LevelGrid.Instance;
        if (lg == null) return set;

        for (int dx = -maxRange; dx <= maxRange; dx++)
        for (int dz = -maxRange; dz <= maxRange; dz++)
        {
            if (dx == 0 && dz == 0) continue; // ei lisätä omaa ruutua
            var gp = origin + new GridPosition(dx, dz, 0);
            if (!lg.IsValidGridPosition(gp)) continue;

            // Sama rengas-metriikka kuin ampumarangeissa (10 * range)
            int cost = SircleCalculator.Sircle(dx, dz);
            if (cost > 10 * maxRange) continue;

            if (HasLineOfSightRaycastHeightAware(origin, gp, losMask, eyeHeight, samplesPerCell, insetWU, ignoreRoot))
                set.Add(gp);
        }
        return set;
    }

}
