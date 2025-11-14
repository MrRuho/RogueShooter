using System.Collections.Generic;
using UnityEngine;

public class FlashGrenadeProjectile : BaseGrenadeProjectile
{
    protected override void OnExplode(GridPosition center)
    {
        if (grenadeDefinition == null) return;

        var levelGrid   = LevelGrid.Instance;
        var unitManager = UnitManager.Instance;
        var losConfig   = LoSConfig.Instance;

        if (levelGrid == null || unitManager == null || losConfig == null) return;

        // Sama säteen lasku kuin ennenkin
        float damageRadiusWU = grenadeDefinition.damageRadius;
        int   radiusTiles    = Mathf.CeilToInt(damageRadiusWU / levelGrid.GetCellSize());
        int   maxCost        = 10 * radiusTiles; // SircleCalculator-metriikka

        var targets = new HashSet<Unit>();

        foreach (var u in unitManager.GetAllUnitList())
        {
            if (!u) continue;

            var ugp = u.GetGridPosition();
            if (ugp.floor != center.floor) continue;

            // 1) Oletko flash-säteen sisällä (tile-metriikalla)?
            int dx   = ugp.x - center.x;
            int dz   = ugp.z - center.z;
            int cost = SircleCalculator.Sircle(dx, dz);
            if (cost > maxCost) continue;

            // 2) Onko unitilla näköyhteys flash-ruutuun
            if (!HasLineOfSightToFlash(u, ugp, center, losConfig))
                continue;

            targets.Add(u);
        }

        // 3) Apply stun
        foreach (var u in targets)
        {
            if (NetMode.IsOnline)
            {
                NetworkSync.ApplyStunDamageToUnit(u, this.GetActorId());
            }
            else
            {
                OfflineGameSimulator.ApplyStunDamageToUnit(u);
            }
        }
    }

    private static bool HasLineOfSightToFlash(
        Unit unit,
        GridPosition from,
        GridPosition flashCenter,
        LoSConfig losConfig)
    {
        // 1) Yritä käyttää UnitVisionin cachea (ei uusia raycasteja)
        var vision = unit.GetComponent<UnitVision>();
        if (vision != null && vision.IsInitialized)
        {
            var visible = vision.GetVision360Tiles();
            if (visible != null && visible.Contains(flashCenter))
                return true;   // näkyy → flash osuu

            return false;
        }

        Debug.LogWarning("[FlashGrenadeProjectile] Fallback. Can't get UnitVision, so using Raycast insted");
        // 2) Fallback: korkeus-tietoinen LoS raycast
        //    losBlockersMask = vain korkeat seinät (LoSConfigissa)
        return RaycastVisibility.HasLineOfSightRaycastHeightAware(
            from,
            flashCenter,
            losConfig.losBlockersMask,
            losConfig.eyeHeight,
            losConfig.samplesPerCell,
            losConfig.insetWU,
            unit.transform,                          // älä blokkaa itseä
            RaycastVisibility.LoSAcceptance.CenterOnly
        );
    }

}
