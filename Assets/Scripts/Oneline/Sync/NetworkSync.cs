using Mirror;
using Mirror.Examples.CharacterSelection;
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

    public static void SpawnGrenade(GameObject grenadePrefab, Vector3 spawnPos, Vector3 targetPos)
    {
        if (NetworkServer.active) // Online: server/host
        {
            var go = Object.Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
            if (go.TryGetComponent<GrenadeProjectile>(out var gp))
                gp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z)); // ks. kohta 4

            NetworkServer.Spawn(go);
            return;
        }

        if (NetworkClient.active) // Online: client
        {
            if (NetworkSyncAgent.Local != null)
            {
                NetworkSyncAgent.Local.CmdSpawnGrenade(spawnPos, targetPos);
            }
            else
            {
                Debug.LogWarning("[NetworkSync] No Local NetworkSyncAgent found, fallback to local Instantiate.");
                var go = Object.Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
                if (go.TryGetComponent<GrenadeProjectile>(out var gp))
                    gp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z));
            }
        }
        else
        {
            
            // Offline
            var go = Object.Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
            if (go.TryGetComponent<GrenadeProjectile>(out var gp))
                gp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z));
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

        if (NetworkServer.active) // Online: server or host
        {
            var healthSystem = target.GetComponent<HealthSystem>();
            if (healthSystem == null) return;

            healthSystem.Damage(amount);
            UpdateHealthBarUI(healthSystem, target);
            return;
        }

        if (NetworkClient.active) // Online: client
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

    private static void UpdateHealthBarUI(HealthSystem healthSystem, Unit target)
    {
        // → ilmoita kaikille clienteille, jotta UnitWorldUI saa eventin
        if (NetworkSyncAgent.Local == null)
        {
            // haetaan mikä tahansa agentti serveriltä (voi olla erillinen manageri)
            var agent = Object.FindFirstObjectByType<NetworkSyncAgent>();
            if (agent != null)
                agent.ServerBroadcastHp(target, healthSystem.GetHealth(), healthSystem.GetHealthMax());
        }
        else
        {
            NetworkSyncAgent.Local.ServerBroadcastHp(target, healthSystem.GetHealth(), healthSystem.GetHealthMax());
        }
    }

    /// <summary>
    /// Server: Control when Pleyers can see own and others Unit stats, 
    /// Like only active player AP(Action Points) are visible.
    /// When is Enemy turn only Enemy Units Action points are visible.
    /// Solo and Versus mode handle this localy becouse there is no need syncronisation.
    /// </summary>
    public static void BroadcastActionPoints(Unit unit, int apValue)
    {
        if (unit == null) return;

        if (NetworkServer.active)
        {
            var agent = Object.FindFirstObjectByType<NetworkSyncAgent>();
            if (agent != null)
                agent.ServerBroadcastAp(unit, apValue);
            return;
        }

        // CLIENT-haara: lähetä peilauspyyntö serverille
        if (NetworkClient.active && NetworkSyncAgent.Local != null)
        {
            var ni = unit.GetComponent<NetworkIdentity>();
            if (ni) NetworkSyncAgent.Local.CmdMirrorAp(ni.netId, apValue);
        }
    }
    
    public static void SpawnRagdoll(GameObject prefab, Vector3 pos, Quaternion rot, uint sourceUnitNetId, Transform originalRootBone)
    {

        if (NetworkServer.active)
        {
            var go = Object.Instantiate(prefab, pos, rot);

            if (go.TryGetComponent<RagdollPoseBinder>(out var ragdollPoseBinder))
            {
                ragdollPoseBinder.sourceUnitNetId = sourceUnitNetId;
            }
            else
            {
                Debug.LogWarning("[Ragdoll] Ragdoll prefab lacks RagdollPoseBinder component.");
            }

            NetworkServer.Spawn(go);
            return;
        }

        // offline fallback
        var off = Object.Instantiate(prefab, pos, rot);
        if (off.TryGetComponent<UnitRagdoll>(out var unitRagdoll))
            unitRagdoll.Setup(originalRootBone);
    }

}
