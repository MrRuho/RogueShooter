using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yhteinen solveri kranaattien AOE-alueen laskemiseen.
/// Tätä voidaan käyttää sekä oikeassa räjähdyksessä että
/// heiton aikaisessa "previewissä".
/// </summary>
public static class GrenadeAreaSolver
{
    /// <summary>
    /// Palauttaa ruudut, joihin kyseinen kranaatti voi vaikuttaa
    /// annetusta keskipisteestä.
    /// </summary>
    public static HashSet<GridPosition> ComputeArea(GridPosition center, GrenadeDefinition def)
    {
        var lg = LevelGrid.Instance;
        if (lg == null || def == null) return new HashSet<GridPosition>();

        float cell = lg.GetCellSize();
        if (cell <= 0.0001f) return new HashSet<GridPosition>();

        int radiusTiles = Mathf.CeilToInt(def.damageRadius / cell);

        switch (def.grenadeType)
        {
            case GrenadeType.Frag:
                // Sama “paine pysähtyy seiniin” -logiikka kuin frag-räjähdyksessä
                return ExplosionSolver.ComputeReach(center, radiusTiles);

            case GrenadeType.Flash:
                // Flash: vain korkeat LoS-seinät blokkaavat:
                return ComputeFlashArea(center, radiusTiles);

            case GrenadeType.Smoke:
            default:
                // Varasuojana pelkkä ympyrä ilman este-logiikkaa
                return ComputeDisc(center, radiusTiles);
        }
    }

    /// <summary>
    /// Flash-kranun “potentiaalinen” alue:
    /// - kaikille ruuduille, jotka ovat säteen sisällä
    /// - joihin EI ole korkeaa LoS-seinää (LoSConfig.losBlockersMask) välissä.
    /// </summary>
    private static HashSet<GridPosition> ComputeFlashArea(GridPosition center, int radiusTiles)
    {
        var los = LoSConfig.Instance;
        var lg  = LevelGrid.Instance;
        var set = new HashSet<GridPosition>();

        if (los == null || lg == null) return set;

        // Sama metriikka kuin sinun FlashGrenadeProjectile.OnExplodessa:
        int maxCost = 10 * radiusTiles; // SircleCalculator-systeemi

        for (int dz = -radiusTiles; dz <= radiusTiles; dz++)
        for (int dx = -radiusTiles; dx <= radiusTiles; dx++)
        {
            var gp = new GridPosition(center.x + dx, center.z + dz, center.floor);
            if (!lg.IsValidGridPosition(gp)) continue;

            int cost = SircleCalculator.Sircle(dx, dz);
            if (cost > maxCost) continue;

            // Korkeat seinät LoSConfigin maskilla
            bool visible =
                RaycastVisibility.HasLineOfSightRaycastHeightAware(
                    gp,                       // "silmä" ruudussa
                    center,                   // flashin ruutu
                    los.losBlockersMask,      // vain korkeat seinät
                    los.eyeHeight,
                    los.samplesPerCell,
                    los.insetWU,
                    null,                     // ei tarvetta ignoreRootille
                    RaycastVisibility.LoSAcceptance.CenterOnly
                );

            if (!visible) continue;

            set.Add(gp);
        }

        // varmuuden vuoksi keskiruutu mukaan
        set.Add(center);
        return set;
    }

    /// <summary>
    /// Yksinkertainen täysi disc (ei esteitä).
    /// </summary>
    private static HashSet<GridPosition> ComputeDisc(GridPosition center, int radiusTiles)
    {
        var lg  = LevelGrid.Instance;
        var set = new HashSet<GridPosition>();
        if (lg == null) return set;

        int r2 = radiusTiles * radiusTiles;

        for (int dz = -radiusTiles; dz <= radiusTiles; dz++)
        for (int dx = -radiusTiles; dx <= radiusTiles; dx++)
        {
            if (dx * dx + dz * dz > r2) continue;

            var gp = new GridPosition(center.x + dx, center.z + dz, center.floor);
            if (!lg.IsValidGridPosition(gp)) continue;

            set.Add(gp);
        }

        set.Add(center);
        return set;
    }
}
