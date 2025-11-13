using UnityEngine;

public static class OfflineGameSimulator
{
    public static void SpawnBullet(GameObject bulletPrefab, Vector3 spawnPos, Vector3 targetPos, bool shouldHitUnits)
    {
        SpawnRouter.SpawnLocal(
            bulletPrefab, spawnPos, Quaternion.identity,
            source: null,
            sceneName: LevelLoader.Instance.CurrentLevel,
            parent: null,
            beforeReturn: go =>
            {
                if (go.TryGetComponent<BulletProjectile>(out var gp))
                    gp.Setup(targetPos, shouldHitUnits);
            });
    }

    public static void SpawnGrenade(GameObject grenadePrefab, Vector3 spawnPos, Vector3 targetPos, float maxRangeWU)
    {
        SpawnRouter.SpawnLocal(
            grenadePrefab, spawnPos, Quaternion.identity,
            source: null,
            sceneName: LevelLoader.Instance.CurrentLevel,
            parent: null,
            beforeReturn: go =>
            {
                if (go.TryGetComponent<BaseGrenadeProjectile>(out var gp))
                    gp.ownerTeamId = 0;
                    gp.Setup(targetPos, maxRangeWU);
            });
    }
    
    public static void SpawnRagdoll(GameObject prefab, Vector3 pos, Quaternion rot, uint sourceUnitNetId, Transform originalRootBone, Vector3 lastHitPosition, int overkill)
    {
        SpawnRouter.SpawnLocal(
            prefab, pos, rot,
            source: originalRootBone,
            sceneName: null,
            parent: null,
            beforeReturn: go =>
            {
                if (go.TryGetComponent<UnitRagdoll>(out var unitRagdoll))
                {
                    unitRagdoll.SetOverkill(overkill);
                    unitRagdoll.SetLastHitPosition(lastHitPosition);
                    unitRagdoll.Setup(originalRootBone);
                }
            });
    }

    public static void ApllyStunDamageToUnit(Unit unit)
    {
        if (unit == null || unit.IsDying() || unit.IsDead()) return;

        int coverAfterHit = unit.GetPersonalCover() / 2;
        unit.SetPersonalCover(coverAfterHit);
        unit.ResetReactionPoints();
        unit.ResetActionPoints();
        unit.RaisOnAnyActionPointsChanged();

        // Aseta Unittien vision coneksi
      //  var vision = unit.GetComponent<UnitVision>();
     //  vision.VisionPenaltyWhenUsingAP(0);    
     //   vision.UpdateVisionNow();

        int teamID = unit.GetTeamID();
        TeamVisionService.Instance.RebuildTeamVisionLocal(teamID, true);

        //Päivitä UI ajantasalle.
        
    }

}
