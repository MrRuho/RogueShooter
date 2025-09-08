using Mirror;
using UnityEngine;

public static class NetworkSync
{
    public static void SpawnBullet(GameObject bulletPrefab, Vector3 spawnPos, Vector3 targetPos)
    {
        if (NetworkServer.active)
        {
            var go = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            if (go.TryGetComponent<BulletProjectile>(out var bp))
                bp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z));
            NetworkServer.Spawn(go);
            return;
        }

        // Client-puoli → Command serverille oman agentin kautta
        if (NetworkClient.active)
        {
            if (NetworkSyncAgent.Local != null)
            {
                NetworkSyncAgent.Local.CmdSpawnBullet(spawnPos, targetPos);
            }
            else
            {
                // fallback (esim. offline SP)
                var go = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
                if (go.TryGetComponent<BulletProjectile>(out var bp))
                    bp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z));
            }
        }
        else
        {
            // täysin offline
            var go = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            if (go.TryGetComponent<BulletProjectile>(out var bp))
                bp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z));
        }
    }
}
