using Mirror;
using UnityEngine;

/// <summary>
/// NetworkSync is a static helper class that centralizes all network-related actions.
/// 
/// Responsibilities:
/// - Provides a single entry point for spawning and synchronizing networked effects and objects.
/// - Decides whether the game is running in server/host mode, client mode, or offline mode.
/// - In online play:
///     - If running on the server/host, spawns objects directly with NetworkServer.Spawn.
///     - If running on a client, forwards the request to the local NetworkSyncAgent, which relays it to the server via Command.
/// - In offline/singleplayer mode, simply instantiates objects locally with Instantiate.
/// 
/// Usage:
/// Call the static methods from gameplay code (e.g. UnitAnimator, Actions) instead of
/// directly instantiating or spawning prefabs. This ensures consistent behavior in all game modes.
/// 
/// Example:
/// NetworkSync.SpawnBullet(bulletPrefab, shootPoint.position, targetPosition);
/// </summary>
public static class NetworkSync
{
    /// <summary>
    /// Spawns a bullet projectile in the game world.
    /// Handles both offline (local Instantiate) and online (NetworkServer.Spawn) scenarios.
    /// 
    /// In server/host:
    ///     - Instantiates and spawns the bullet directly with NetworkServer.Spawn.
    /// In client:
    ///     - Forwards the request to NetworkSyncAgent.Local, which executes a Command.
    /// In offline:
    ///     - Instantiates the bullet locally.
    /// </summary>
    /// <param name="bulletPrefab">The bullet prefab to spawn (must have NetworkIdentity if used online).</param>
    /// <param name="spawnPos">The starting position of the bullet (usually weapon muzzle).</param>
    /// <param name="targetPos">The target world position the bullet should travel towards.</param>
    public static void SpawnBullet(GameObject bulletPrefab, Vector3 spawnPos, Vector3 targetPos)
    {
        if (NetworkServer.active) // Online: server or host
        {
            var go = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            if (go.TryGetComponent<BulletProjectile>(out var bp))
                bp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z));
            NetworkServer.Spawn(go);
            return;
        }


        if (NetworkClient.active) // Online: client
        {
            if (NetworkSyncAgent.Local != null)
            {
                NetworkSyncAgent.Local.CmdSpawnBullet(spawnPos, targetPos);
            }
            else
            {
                // fallback if no local agent found (shouldn't happen in a correct setup)
                Debug.LogWarning("[NetworkSync] No Local NetworkSyncAgent found, falling back to local Instantiate.");
                var go = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
                if (go.TryGetComponent<BulletProjectile>(out var bp))
                    bp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z));
            }
        }
        else
        {
            // Offline / Singleplayer: just instantiate locally
            var go = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            if (go.TryGetComponent<BulletProjectile>(out var bp))
                bp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z));
        }
    }

    /// <summary>
    /// Apply damage to a Unit in SP/Host/Client modes.
    /// - Server/Host: call HealthSystem.Damage directly (authoritative).
    /// - Client: send a Command via NetworkSyncAgent to run on server.
    /// - Offline: call locally.
    /// </summary>
    public static void ApplyDamage(Unit target, int amount)
    {
        if (target == null) return;

        if (NetworkServer.active)
        {
            target.GetComponent<HealthSystem>()?.Damage(amount);
            return;
        }

        if (NetworkClient.active)
        {
            var ni = target.GetComponent<NetworkIdentity>();
            if (ni && NetworkSyncAgent.Local != null)
            {
                NetworkSyncAgent.Local.CmdApplyDamage(ni.netId, amount);
                return;
            }
        }

        // Offline fallback
        target.GetComponent<HealthSystem>()?.Damage(amount);
    }
}
