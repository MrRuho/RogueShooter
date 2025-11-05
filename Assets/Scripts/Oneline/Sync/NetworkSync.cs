using Mirror;
using Unity.Networking.Transport.Error;
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
    // --- Perus rooliliput (yhdessä paikassa) ---
    public static bool IsServer           => NetworkServer.active;
    public static bool IsClient           => NetworkClient.active;
    public static bool IsHost             => NetworkServer.active && NetworkClient.active;
    public static bool IsClientOnly       => !NetworkServer.active && NetworkClient.active;
    public static bool IsDedicatedServer  => NetworkServer.active && !NetworkClient.active;
    public static bool IsOffline => !NetworkServer.active && !NetworkClient.active;

    
    /// <summary>
    /// Hae NetworkClient netId:llä (toimii sekä clientillä että serverillä).
    /// Palauttaa null jos ei löydy (esim. ei vielä spawnattu tällä framella).
    /// </summary>
    public static NetworkIdentity FindIdentity(uint actorId)
    {
        if (actorId == 0) return null;
        NetworkClient.spawned.TryGetValue(actorId, out var ni);
        return ni;
    }
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
    public static void SpawnBullet(GameObject bulletPrefab, Vector3 spawnPos, Vector3 targetPos, uint actorNetId)
    {
        if (NetworkServer.active) // Online: server or host
        {
            Transform src = null;
            if (NetworkServer.spawned.TryGetValue(actorNetId, out var srcNI) && srcNI != null)
                src = srcNI.transform;

            SpawnRouter.SpawnNetworkServer(
                bulletPrefab, spawnPos, Quaternion.identity,
                source: src,            // löydetty actorNetId:llä
                sceneName: null,
                parent: null,
                owner: null,
                beforeSpawn: go =>
                {
                    if (go.TryGetComponent<BulletProjectile>(out var gp))
                    {
                        gp.actorUnitNetId = actorNetId;
                        gp.Setup(targetPos);
                    }
                });

            return;

        }

        if (NetworkClient.active && NetworkSyncAgent.Local != null) // Online: client
        {
            NetworkSyncAgent.Local.CmdSpawnBullet(actorNetId, targetPos);
        } 
    }

    // HUOM: käytä tätä myös AE:stä (UnitAnimatorista)
    public static void SpawnGrenade(GameObject grenadePrefab, Vector3 spawnPos, Vector3 targetPos, float maxRangeWU, uint actorNetId)
    {

        if (NetworkServer.active) // Online: server tai host
        {
            Transform src = null;
            if (NetworkServer.spawned.TryGetValue(actorNetId, out var srcNI) && srcNI != null)
                src = srcNI.transform;

            SpawnRouter.SpawnNetworkServer(
                grenadePrefab, spawnPos, Quaternion.identity,
                source: src,            // löydetty actorNetId:llä
                sceneName: null,
                parent: null,
                owner: null,
                beforeSpawn: go =>
                {

                    var unit = src ? src.GetComponent<Unit>() : null;
                    int teamId = unit ? unit.GetTeamID() : -1;

                    if (go.TryGetComponent<GrenadeProjectile>(out var gp)) {
                        gp.actorUnitNetId = actorNetId;
                        gp.ownerTeamId = teamId;
                        gp.Setup(targetPos, maxRangeWU);
                    }
                });

            return;

        }

        if (NetworkClient.active && NetworkSyncAgent.Local != null) // Online: client
        {      
            NetworkSyncAgent.Local.CmdSpawnGrenade(actorNetId, targetPos);         
        }
    }

    /// <summary>
    /// Apply damage to a Unit in SP/Host/Client modes.
    /// - Server/Host: call HealthSystem.Damage directly (authoritative).
    /// - Client: send a Command via NetworkSyncAgent to run on server.
    /// - Offline: call locally.
    /// </summary>
    public static void ApplyDamageToUnit(Unit unit, int amount, Vector3 hitPosition, uint actorNetId)
    {

        if (unit == null) return;

        if (NetworkServer.active) // Online: server or host
        {
            var healthSystem = unit.GetComponent<HealthSystem>();
            if (healthSystem == null) return;

            healthSystem.Damage(amount, hitPosition);
            UpdateHealthBarUI(healthSystem, unit);
            return;
        }

        if (NetworkClient.active) // Online: client
        {
            var ni = unit.GetComponent<NetworkIdentity>();
            if (ni && NetworkSyncAgent.Local != null)
            {
                if (unit == null || unit.IsDying() || unit.IsDead()) return;
                NetworkSyncAgent.Local.CmdApplyDamage(actorNetId, ni.netId, amount, hitPosition);
                return;
            }
        }

        unit.GetComponent<HealthSystem>().Damage(amount, hitPosition);        
    }

    public static void ApplyDamageToObject(DestructibleObject target, int amount, Vector3 hitPosition, uint actorNetId)
    {
        if (target == null) return;

        if (NetworkServer.active) // Online: server or host
        {
            target.Damage(amount, hitPosition);
            return;
        }

        if (NetworkClient.active) // Online: client
        {
            var ni = target.GetComponent<NetworkIdentity>();
            if (ni && NetworkSyncAgent.Local != null)
            {
                NetworkSyncAgent.Local.CmdApplyDamageToObject(actorNetId,ni.netId, amount, hitPosition);
                return;
            }
        }

        // Offline fallback
        target.Damage(amount, hitPosition);
    }

    private static void UpdateHealthBarUI(HealthSystem healthSystem, Unit unit)
    {

        if (unit == null || healthSystem == null) return;
        // → ilmoita kaikille clienteille, jotta UnitWorldUI saa eventin
        if (NetworkSyncAgent.Local == null)
        {
            // haetaan mikä tahansa agentti serveriltä (voi olla erillinen manageri)
            var agent = Object.FindFirstObjectByType<NetworkSyncAgent>();
            if (agent != null)
                agent.ServerBroadcastHp(unit, healthSystem.GetHealth(), healthSystem.GetHealthMax());
            return;
        }

        if (NetworkClient.active && NetworkSyncAgent.Local != null)
        {
            if (unit == null || unit.IsDying() || unit.IsDead()) return;
            var ni = unit.GetComponent<NetworkIdentity>();
            if (ni != null)
                NetworkSyncAgent.Local.CmdRequestHpRefresh(ni.netId);
        }
    }

    public static void UpdateCoverUI(Unit unit)
    {
        if (unit == null || unit.IsDying() || unit.IsDead()) return;

        if (NetworkServer.active)
        {
            var agent = UnityEngine.Object.FindFirstObjectByType<NetworkSyncAgent>();
            if (agent != null)
                agent.ServerBroadcastCover(unit, unit.GetPersonalCover(), unit.GetPersonalCoverMax());
            return;
        }

        if (NetworkClient.active && NetworkSyncAgent.Local != null)
        {
            var ni = unit.GetComponent<NetworkIdentity>();
            if (ni != null)
                NetworkSyncAgent.Local.CmdRequestCoverRefresh(ni.netId);
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
        if (unit == null || unit.IsDying() || unit.IsDead()) return;

        if (NetworkServer.active)
        {
            var agent = Object.FindFirstObjectByType<NetworkSyncAgent>();
            if (agent != null)
                agent.ServerBroadcastAp(unit, apValue);
            return;
        }

        if (NetworkClient.active && NetworkSyncAgent.Local != null)
        {
            var ni = unit.GetComponent<NetworkIdentity>();
            if (ni) NetworkSyncAgent.Local.CmdMirrorAp(ni.netId, apValue);
        }
    }

    public static void SpawnRagdoll(GameObject prefab, Vector3 pos, Quaternion rot, uint sourceUnitNetId, Vector3 lastHitPosition, int overkill)
    {

        if (NetworkServer.active)
        {
            // 1) Hae kaatuneen unitin Transform serveriltä netId:llä
            Transform src = null;
            if (NetworkServer.spawned.TryGetValue(sourceUnitNetId, out var srcNI) && srcNI != null)
                src = srcNI.transform;

            // 2) Spawnaa verkossa: siirto oikeaan level-sceneen hoituu SpawnRouterissa
            SpawnRouter.SpawnNetworkServer(
                prefab, pos, rot,
                source: src,          // → sama scene kuin kaatuneella unitilla
                sceneName: null,
                parent: null,
                owner: null,
                beforeSpawn: go =>
                {
                    if (go.TryGetComponent<UnitRagdoll>(out var rg))
                    {
                        rg.SetOverkill(overkill);
                        rg.SetLastHitPosition(lastHitPosition);
                    }
                    if (go.TryGetComponent<RagdollPoseBinder>(out var binder))
                    {
                        binder.sourceUnitNetId = sourceUnitNetId;
                        binder.lastHitPos = lastHitPosition;
                        binder.overkill = overkill;
                    }
                    else
                    {
                        Debug.LogWarning("[Ragdoll] Ragdoll prefab lacks RagdollPoseBinder.");
                    }
                });

            return;
        }
    }

    public static bool IsOwnerHost(uint ownerId)
    {
        if (!NetworkServer.active) return false; // varmin tieto vain serverillä
        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn?.identity && conn.identity.netId == ownerId)
                return conn.connectionId == 0; // 0 = host
        }
        return false;
    }

    /// <summary>Onko tämä NetworkIdentity omistettu tällä koneella?</summary>
    public static bool IsOwnedHere(NetworkIdentity ni)
        => ni != null && (ni.isOwned || ni.isLocalPlayer);

    /// <summary>
    /// Paikallisen pelaajan teamId (piirtäjät, HUD, yms. käyttävät tätä).
    /// - Offline: 0
    /// - Versus: host=0, puhdas client=1
    /// - SP/Coop online: 0
    /// </summary>
    public static int GetLocalPlayerTeamId(GameMode mode)
    {
        if (IsOffline) return 0;

        if (mode == GameMode.Versus) return IsServer ? 0 : 1;
        return 0;
    }
    
}
