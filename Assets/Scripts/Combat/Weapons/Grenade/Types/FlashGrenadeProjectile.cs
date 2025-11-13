using UnityEngine;

public class FlashGrenadeProjectile : BaseGrenadeProjectile
{
    protected override void OnExplode(GridPosition center)
    {
        if (grenadeDefinition == null) return;

        float damageRadiusWU = grenadeDefinition.damageRadius;

        int rTiles = Mathf.CeilToInt(damageRadiusWU / LevelGrid.Instance.GetCellSize());
        var tiles = ExplosionSolver.ComputeReach(center, rTiles);

        var targets = new System.Collections.Generic.HashSet<Unit>();
        foreach (var gp in tiles)
        {
            var list = LevelGrid.Instance.GetUnitListAtGridPosition(gp);
            if (list == null || list.Count == 0) continue;
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                if (u) targets.Add(u);
            }
        }

        foreach (var u in targets)
        {   
            //jos netti yhteys niin tämä
            if (NetMode.IsOnline)
            {
                NetworkSync.ApplyStunDamageToUnit(u, this.GetActorId());
            } else
            {
                OfflineGameSimulator.ApllyStunDamageToUnit(u);
            }

        }
    }

}
