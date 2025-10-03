using System;
using Mirror;
using UnityEngine;
/// <summary>
/// NetworkSyncAgent is a helper NetworkBehaviour to relay Commands from clients to the server.
/// Each client should have exactly one instance of this script in the scene, usually attached to the PlayerController GameObject.
/// 
/// Responsibilities:
/// - Receives local calls from NetworkSync (static helper).
/// - Sends Commands to the server when the local player performs an action (e.g. shooting).
/// - On the server, instantiates and spawns networked objects (like projectiles).
/// </summary>
public class NetworkSyncAgent : NetworkBehaviour
{
    public static NetworkSyncAgent Local;   // Easy access for NetworkSync static helper
    [SerializeField] private GameObject bulletPrefab; // Prefab for the bullet projectile
    [SerializeField] private GameObject grenadePrefab;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Local = this;
    }

    /// <summary>
    /// Command from client → server.
    /// The client requests the server to spawn a bullet at the given position.
    /// The server instantiates the prefab, sets it up, and spawns it to all connected clients.
    /// </summary>
    /// <param name="spawnPos">World position where the bullet starts (usually weapon muzzle).</param>
    /// <param name="targetPos">World position the bullet is travelling towards.</param>
    [Command(requiresAuthority = true)]
    public void CmdSpawnBullet(Vector3 spawnPos, Vector3 targetPos)
    {
        if (bulletPrefab == null) { Debug.LogWarning("[NetSync] bulletPrefab missing"); return; }

        // Instantiate on the server
        var go = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        // Setup target on the projectile
        if (go.TryGetComponent<BulletProjectile>(out var bp))
        {
            bp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z));
        }

        // Spawn across the network
        NetworkServer.Spawn(go);
    }
 
    [Command(requiresAuthority = true)]
    public void CmdSpawnGrenade(Vector3 spawnPos, Vector3 targetPos)
    {
        if (grenadePrefab == null) { Debug.LogWarning("[NetSync] grenadePrefab missing"); return; }

        var go = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
        if (go.TryGetComponent<GrenadeProjectile>(out var gp))
            gp.Setup(targetPos); // tärkeää: ennen Spawnia

        NetworkServer.Spawn(go);
    }

    /// <summary>
    /// Client → Server: resolve target by netId and apply damage on server.
    /// then broadcast the new HP to all clients for UI.
    /// </summary>
    [Command(requiresAuthority = true)]
    public void CmdApplyDamage(uint targetNetId, int amount, Vector3 hitPosition)
    {
        if (!NetworkServer.spawned.TryGetValue(targetNetId, out var targetNi) || targetNi == null)
            return;

        var unit = targetNi.GetComponent<Unit>();
        var hs = targetNi.GetComponent<HealthSystem>();
        if (unit == null || hs == null)
            return;

        // 1) Server tekee damagen (kuten ennenkin)
        hs.Damage(amount, hitPosition);

        // 2) Heti perään broadcast → kaikki clientit päivittävät oman UI:nsa
        //    (ServerBroadcastHp kutsuu RpcNotifyHpChanged → hs.ApplyNetworkHealth(..) clientillä)
        ServerBroadcastHp(unit, hs.GetHealth(), hs.GetHealthMax());
    }

    [Command(requiresAuthority = true)]
    public void CmdApplyDamageToObject(uint targetNetId, int amount, Vector3 hitPosition)
    {
        if (!NetworkServer.spawned.TryGetValue(targetNetId, out var targetNi) || targetNi == null)
            return;

        var obj = targetNi.GetComponent<DestructibleObject>();
        if (obj == null)
            return;

        obj.Damage(amount, hitPosition);
    }

    // ---- SERVER-puolen helperit: kutsu näitä palvelimelta
    [Server]
    public void ServerBroadcastHp(Unit unit, int current, int max)
    {
        var ni = unit.GetComponent<NetworkIdentity>();
        if (ni) RpcNotifyHpChanged(ni.netId, current, max);
    }

    [Server]
    public void ServerBroadcastAp(Unit unit, int ap)
    {
        var ni = unit.GetComponent<NetworkIdentity>();
        if (ni) RpcNotifyApChanged(ni.netId, ap);
    }

    // ---- SERVER → ALL CLIENTS: HP-muutos ilmoitus
    [ClientRpc]
    void RpcNotifyHpChanged(uint unitNetId, int current, int max)
    {
        if (!NetworkClient.spawned.TryGetValue(unitNetId, out var id) || id == null) return;

        var hs = id.GetComponent<HealthSystem>();
        if (hs == null) return;

        hs.ApplyNetworkHealth(current, max);
    }

    // ---- SERVER → ALL CLIENTS: AP-muutos ilmoitus
    [ClientRpc]
    void RpcNotifyApChanged(uint unitNetId, int ap)
    {
        ApplyApClient(unitNetId, ap);
    }

    [Command]
    public void CmdMirrorAp(uint unitNetId, int ap)
    {
        RpcNotifyApChanged(unitNetId, ap);
    }

    void ApplyApClient(uint unitNetId, int ap)
    {
        if (!NetworkClient.spawned.TryGetValue(unitNetId, out var id) || id == null) return;
        var unit = id.GetComponent<Unit>();
        if (!unit) return;

        unit.ApplyNetworkActionPoints(ap); // päivittää arvon + triggaa eventin
    }
}
