using UnityEngine;

public static class OfflineGameSimulator
{
    public static void SpawnBullet(GameObject bulletPrefab, Vector3 spawnPos, Vector3 targetPos)
    {       
        SpawnRouter.SpawnLocal(
            bulletPrefab, spawnPos, Quaternion.identity,
            source: null,
            sceneName: LevelLoader.Instance.CurrentLevel,
            parent: null,
            beforeReturn: go =>
            {
                if (go.TryGetComponent<BulletProjectile>(out var gp))
                gp.Setup(targetPos);          
            });
    }

    public static void SpawnGrenade(GameObject grenadePrefab, Vector3 spawnPos, Vector3 targetPos)
    {
        SpawnRouter.SpawnLocal(
            grenadePrefab, spawnPos, Quaternion.identity,
            source: null,
            sceneName: LevelLoader.Instance.CurrentLevel,
            parent: null,
            beforeReturn: go =>
            {
                if (go.TryGetComponent<GrenadeProjectile>(out var gp))
                    gp.Setup(targetPos);
            });
    }
    
    public static void SpawnRagdoll(GameObject prefab, Vector3 pos, Quaternion rot, uint sourceUnitNetId, Transform originalRootBone, Vector3 lastHitPosition, int overkill)
    {

        // OFFLINE: paikallinen spawn, ohjaa samaan sceneen kuin originalRootBone
        SpawnRouter.SpawnLocal(
            prefab, pos, rot,
            source: originalRootBone, // → sama scene kuin ruumiilla/luurangolla (level)
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
}
