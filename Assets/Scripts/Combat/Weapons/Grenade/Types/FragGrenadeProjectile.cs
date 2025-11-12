using UnityEngine;

public class FragGrenadeProjectile : BaseGrenadeProjectile
{
    protected override void OnExplode(GridPosition center)
    {
        if (grenadeDefinition == null) return;
        
        float damageRadiusWU = grenadeDefinition.damageRadius;
        int damage = grenadeDefinition.baseDamage;
        
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
        
        var objects = new System.Collections.Generic.HashSet<DestructibleObject>();
        foreach (var gp in tiles)
        {
            var list = LevelGrid.Instance.GetDestructibleListAtGridPosition(gp);
            if (list == null || list.Count == 0) continue;
            for (int i = 0; i < list.Count; i++)
            {
                var @object = list[i];
                if (@object) objects.Add(@object);
            }
        }
        
        foreach (var u in targets)
        {
            NetworkSync.ApplyDamageToUnit(u, damage, targetPosition, this.GetActorId());
        }
        
        foreach (var o in objects)
        {
            NetworkSync.ApplyDamageToObject(o, damage, targetPosition, this.GetActorId());
        }
    }
}
