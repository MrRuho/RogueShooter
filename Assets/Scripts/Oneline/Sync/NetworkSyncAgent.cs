using System.Collections;
using Mirror;
using UnityEngine;
/// <summary>
/// Attached to the PlayerController GameObject.
/// NetworkSyncAgent is a helper NetworkBehaviour to relay Commands from clients to the server.
/// Each client should have exactly one instance of this script in the scene.
/// 
/// Responsibilities:
/// - Receives local calls from NetworkSync (static helper).
/// - Sends Commands to the server when the local player performs an action (e.g. shooting).
/// - On the server, instantiates and spawns networked objects (like projectiles).
/// </summary>
public class NetworkSyncAgent : NetworkBehaviour
{
    public static NetworkSyncAgent Local;   // Easy access for NetworkSync static helper
    public override void OnStartAuthority() { Local = this; }
    public override void OnStopAuthority() { if (Local == this) Local = null; }

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
    /// <param name="clientSuggestedTarget">World position the bullet is travelling towards.</param>
    
    /*
    [Command(requiresAuthority = true)]
    public void CmdSpawnBullet(uint actorNetId, Vector3 clientSuggestedTarget)
    {
        if (!NetworkServer.active) return;
        if (bulletPrefab == null) { Debug.LogWarning("[NetSyncAgent] bulletPrefab missing"); return; }
        if (actorNetId == 0 || !RightOwner(actorNetId)) return;

        if (!NetworkServer.spawned.TryGetValue(actorNetId, out var actorNi) || actorNi == null) return;

        var ua = actorNi.GetComponent<UnitAnimator>();
        Vector3 origin = (ua && ua.ShootPoint) ? ua.ShootPoint.position : actorNi.transform.position;
        Vector3 target = clientSuggestedTarget;

        // tärkeää: käytä SpawnRouteria ja anna source = actor
        SpawnRouter.SpawnNetworkServer(
            bulletPrefab, origin, Quaternion.identity,
            source: actorNi.transform,
            sceneName: null,
            parent: null,
            owner: connectionToClient,         // omistajuus halutessa
            beforeSpawn: go =>
            {
                if (go.TryGetComponent<BulletProjectile>(out var bp))
                {
                    bp.actorUnitNetId = actorNetId;
                    bp.Setup(target);
                }
            });
    }
    */
    
    [Command(requiresAuthority = true)]
    public void CmdSpawnBullet(uint actorNetId, Vector3 clientSuggestedTarget, bool shouldHitUnits)
    {
        if (!NetworkServer.active) return;
        if (bulletPrefab == null) { Debug.LogWarning("[NetSyncAgent] bulletPrefab missing"); return; }
        if (actorNetId == 0 || !RightOwner(actorNetId)) return;

        if (!NetworkServer.spawned.TryGetValue(actorNetId, out var actorNi) || actorNi == null) return;

        var ua = actorNi.GetComponent<UnitAnimator>();
        Vector3 origin = (ua && ua.ShootPoint) ? ua.ShootPoint.position : actorNi.transform.position;
        Vector3 target = clientSuggestedTarget;

        SpawnRouter.SpawnNetworkServer(
            bulletPrefab, origin, Quaternion.identity,
            source: actorNi.transform,
            sceneName: null,
            parent: null,
            owner: connectionToClient,
            beforeSpawn: go =>
            {
                if (go.TryGetComponent<BulletProjectile>(out var bp))
                {
                    bp.actorUnitNetId = actorNetId;
                    bp.Setup(target, shouldHitUnits);
                }
            });
    }



    [Command(requiresAuthority = true)]
    public void CmdSpawnGrenade(uint actorNetId, Vector3 clientSuggestedTarget)
    {
        if (!NetworkServer.active) return;
        if (grenadePrefab == null) { Debug.LogWarning("[NetSyncAgent] GrenadePrefab missing"); return; }
        if (actorNetId == 0 || !RightOwner(actorNetId)) return;

        if (!NetworkServer.spawned.TryGetValue(actorNetId, out var actorNi) || actorNi == null) return;

        var ua = actorNi.GetComponent<UnitAnimator>();
        if (!ua || !ua.ThrowPoint) return;

        Vector3 origin = ua.ThrowPoint.position;
        Vector3 target = clientSuggestedTarget;

        SpawnRouter.SpawnNetworkServer(
            grenadePrefab, origin, Quaternion.identity,
            source: actorNi.transform,
            sceneName: null,
            parent: null,
            owner: connectionToClient,
            beforeSpawn: go =>
            {
                var unit = actorNi.GetComponent<Unit>();
                int teamId = unit ? unit.GetTeamID() : -1;

                if (go.TryGetComponent<GrenadeProjectile>(out var gp))
                {
                    gp.actorUnitNetId = actorNetId;
                    gp.ownerTeamId    = teamId;
                    gp.Setup(target);
                }
            });
    }

    private bool RightOwner(uint actorNetId)
    {
        // Varmista että soittaja omistaa kyseisen actor-yksikön
        if (!NetworkServer.spawned.TryGetValue(actorNetId, out var actorIdentity) || actorIdentity == null) return false;

        // actorin todellinen omistaja. 
        var actorUnit = actorIdentity.GetComponent<Unit>();

        // Client joka lähetti comennon.
        var callerOwnerId = connectionToClient.identity?.netId ?? 0;

        // Unitissa on OwnerId, jonka asetus tehdään spawnaaessa (SpawnUnitsCoordinator) → käytä sitä checkissä
        // OwnerId = PlayerController-objektin netId
        if (actorUnit == null || actorUnit.OwnerId != callerOwnerId) return false;

        return true;
    }

    /// <summary>
    /// Client → Server: resolve target by netId and apply damage on server.
    /// then broadcast the new HP to all clients for UI.
    /// </summary>
    [Command(requiresAuthority = true)]
    public void CmdApplyDamage(uint actorNetId, uint targetNetId, int amount, Vector3 hitPosition)
    {
        
        if (!NetworkServer.spawned.TryGetValue(targetNetId, out var targetNi) || targetNi == null) return;
        if (!RightOwner(actorNetId)) return;

        var unit = targetNi.GetComponent<Unit>();
        var hs = targetNi.GetComponent<HealthSystem>();
        if (unit == null || hs == null)
            return;

        // --- NEW: server-side sanity cap ---
        int maxAllowed = 0;
        if (NetworkServer.spawned.TryGetValue(actorNetId, out var attackerNi) && attackerNi != null)
        {
            var attacker = attackerNi.GetComponent<Unit>();
            var w = attacker != null ? attacker.GetCurrentWeapon() : null;

            // Aseesta johdettu maksimi: Miss/Graze/Hit/Crit → enintään base + critBonus
            if (w != null)
                maxAllowed = Mathf.Max(maxAllowed, w.baseDamage + w.critBonusDamage); // esim. 10 + 8, jne. 

            // Lähitaistelulle varmuuskatto (sinulla MeleeAction.damage = 100 → ota vähintään tämä)
            // Vältetään riippuvuus MeleeActionin yksityiseen kenttään ottamalla varovainen fallback:
            maxAllowed = Mathf.Max(maxAllowed, 100); // MeleeActionissa serialize'd damage=100. :contentReference[oaicite:2]{index=2}
        }

        int safe = Mathf.Clamp(amount, 0, maxAllowed);
        if (safe != amount)
            Debug.LogWarning($"[Server] Clamped damage from {amount} to {safe} (actor {actorNetId} → target {targetNetId}).");

        // 1) Server tekee damagen
        hs.Damage(amount, hitPosition);

        // 2) Broadcast UI (kuten ennenkin)
        ServerBroadcastHp(unit, hs.GetHealth(), hs.GetHealthMax());
    }

    [Command(requiresAuthority = true)]
    public void CmdApplyDamageToObject(uint actorNetId, uint targetNetId, int amount, Vector3 hitPosition)
    {
        if (!NetworkServer.spawned.TryGetValue(targetNetId, out var targetNi) || targetNi == null) return;
        if (!RightOwner(actorNetId)) return;

        var obj = targetNi.GetComponent<DestructibleObject>();
        if (obj == null) return;

        obj.Damage(amount, hitPosition);
    }

    [Command(requiresAuthority = false)]
    public void CmdRequestHpRefresh(uint unitNetId)
    {
        if (!NetworkServer.active) return;
        if (!NetworkServer.spawned.TryGetValue(unitNetId, out var id)) return;

        var u = id.GetComponent<Unit>();
        var hs = u ? u.GetComponent<HealthSystem>() : null;
        if (u == null || hs == null) return;

        ServerBroadcastHp(u, hs.GetHealth(), hs.GetHealthMax()); // server lukee
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

    [Server]
    public void ServerBroadcastCover(Unit unit, int current, int max)
    {
        var ni = unit.GetComponent<NetworkIdentity>();
        if (ni) RpcNotifyCoverChanged(ni.netId, current, max);
    }

    // ---- SERVER → ALL CLIENTS: Cover-muutos ilmoitus
    [ClientRpc]
    void RpcNotifyCoverChanged(uint unitNetId, int current, int max)
    {
        if (!NetworkClient.spawned.TryGetValue(unitNetId, out var id) || id == null) return;

        var unit = id.GetComponent<Unit>();
        if (unit == null) return;

        unit.ApplyNetworkCover(current, max);
    }

    [Command(requiresAuthority = false)]
    public void CmdRequestCoverRefresh(uint unitNetId)
    {
        if (!NetworkServer.spawned.TryGetValue(unitNetId, out var id) || id == null) return;
        var unit = id.GetComponent<Unit>();
        if (unit == null) return;

        // Server lukee arvot ja broadcastaa
        ServerBroadcastCover(unit, unit.GetPersonalCover(), unit.GetPersonalCoverMax());
    }

    [Command(requiresAuthority = false)]
    public void CmdSetUnitCover(uint unitNetId, int value)
    {
        if (!NetworkServer.spawned.TryGetValue(unitNetId, out var id) || id == null) return;
        var unit = id.GetComponent<Unit>();
        if (!unit) return;

        unit.SetPersonalCover(Mathf.Clamp(value, 0, unit.GetPersonalCoverMax()));
    }

    [Command(requiresAuthority = false)]
    public void CmdSetUnderFire(uint unitNetId, bool value)
    {
        
        if (!NetworkServer.spawned.TryGetValue(unitNetId, out var id) || id == null) return;
        var unit = id.GetComponent<Unit>();
        if (!unit) return;

        unit.SetUnderFireServer(value);
    }

    [Server]
    public void ServerBroadcastUnderFire(Unit unit, bool value)
    {
        var ni = unit.GetComponent<NetworkIdentity>();
        if (ni) RpcNotifyUnderFireChanged(ni.netId, value);
    }

    [ClientRpc]
    void RpcNotifyUnderFireChanged(uint unitNetId, bool value)
    {
        if (!NetworkClient.spawned.TryGetValue(unitNetId, out var id) || id == null) return;
        var unit = id.GetComponent<Unit>();
        if (!unit) return;

        unit.ApplyNetworkUnderFire(value);
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

    [Command]
    public void CmdRegenCoverOnMove(uint unitNetId, int distance)
    {
        if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity ni)) return;
        var cs = ni.GetComponent<CoverSkill>();
        if (cs != null) cs.ServerRegenCoverOnMove(distance);
    }
    [Command]
    public void CmdResetCurrentCoverBonus(uint unitNetId)
    {
        if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity ni)) return;
        var cs = ni.GetComponent<CoverSkill>();
        if (cs != null) cs.ServerResetCurrentCoverBonus();
    }

    [Command(requiresAuthority = false)]
    public void CmdApplyCoverBonus(uint unitNetId)
    {

        if (!NetworkServer.spawned.TryGetValue(unitNetId, out NetworkIdentity ni)) return;
        var cs = ni.GetComponent<CoverSkill>(); // tai GetComponentInChildren<CoverSkill>()
        if (cs != null) cs.ServerApplyCoverBonus();
    }

}
