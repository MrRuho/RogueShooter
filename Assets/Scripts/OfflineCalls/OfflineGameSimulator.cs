using UnityEngine;

public static class OfflineGameSimulator
{
    public static void SpawnBullet(GameObject bulletPrefab, Vector3 spawnPos, Vector3 targetPos)
    {
        var bullet = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        if (bullet.TryGetComponent<BulletProjectile>(out var bulletProjectile))
            bulletProjectile.Setup(targetPos);
    }

    public static void SpawnGrenade(GameObject grenadePrefab, Vector3 spawnPos, Vector3 targetPos)
    {
        var go = Object.Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
        if (go.TryGetComponent<GrenadeProjectile>(out var gp))
            gp.Setup(targetPos);
    }
}
