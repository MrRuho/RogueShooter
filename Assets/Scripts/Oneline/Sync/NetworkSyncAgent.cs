using System.Collections;
using System.Collections.Generic;
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
                    gp.ownerTeamId = teamId;
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

    [Server]
    public void ServerBroadcastOverwatchShot(uint watcherNetId, int targetX, int targetZ, int targetFloor)
    {
        RpcExecuteOverwatchShot(watcherNetId, targetX, targetZ, targetFloor);
    }

    [ClientRpc]
    void RpcExecuteOverwatchShot(uint watcherNetId, int targetX, int targetZ, int targetFloor)
    {

        if (!NetworkClient.spawned.TryGetValue(watcherNetId, out var watcherNi) || watcherNi == null)
        {
            Debug.LogWarning($"[OW-Client] Watcher netId {watcherNetId} not found in spawned objects!");
            return;
        }

        var watcher = watcherNi.GetComponent<Unit>();
        if (watcher == null)
        {
            Debug.LogWarning($"[OW-Client] Watcher has no Unit component!");
            return;
        }

        if (watcher.IsDead() || watcher.IsDying())
        {
            Debug.LogWarning($"[OW-Client] Watcher {watcher.name} is dead or dying!");
            return;
        }

        var shoot = watcher.GetComponent<ShootAction>();
        if (shoot == null)
        {
            Debug.LogWarning($"[OW-Client] Watcher {watcher.name} has no ShootAction!");
            return;
        }

        GridPosition targetGridPos = new GridPosition(targetX, targetZ, targetFloor);
        shoot.MarkAsOverwatchShot(true);
        shoot.TakeAction(targetGridPos, () => { });
    }

    [Command(requiresAuthority = false)]
    public void CmdCheckOverwatchStep(uint unitNetId, int gridX, int gridZ, int gridFloor)
    {
        if (!NetworkServer.active) return;

        if (!NetworkServer.spawned.TryGetValue(unitNetId, out var unitNi) || unitNi == null) return;

        var unit = unitNi.GetComponent<Unit>();
        if (unit == null || unit.IsDead() || unit.IsDying()) return;

        GridPosition gridPos = new GridPosition(gridX, gridZ, gridFloor);
        StatusCoordinator.Instance.CheckOverwatchStep(unit, gridPos);
    }


    [ClientRpc]
    public void RpcRebuildTeamVision(uint[] unitNetIds, bool endPhase)
    {
        foreach (var id in unitNetIds)
        {
            if (!Mirror.NetworkClient.spawned.TryGetValue(id, out var ni) || ni == null) continue;

            var u = ni.GetComponent<Unit>();
            var v = u ? u.GetComponent<UnitVision>() : null;
            if (u == null || v == null || !v.IsInitialized) continue;

            // 1) aina tuore 360° cache
            v.UpdateVisionNow();

            Vector3 facing = u.transform.forward;
            facing.y = 0f;
            float angle = 360f;

            // 2) ENSIN OverwatchPayload (serveriltä päivitetty)
            if (u.TryGetComponent<UnitStatusController>(out var status) &&
                status.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var payload))
            {
                facing = (payload.facingWorld.sqrMagnitude > 1e-6f) ? payload.facingWorld : facing;
                angle = payload.coneAngleDeg;            // esim. 80f
            }
            // 3) Muuten jos endPhase ja OW päällä -> johda suunnasta
            else if (endPhase && u.TryGetComponent<OverwatchAction>(out var ow) && ow.IsOverwatch())
            {
                var dir = ow.TargetWorld - u.transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f) facing = dir.normalized;
                angle = 80f;
            }
            // 4) Muuten fallback AP-logiikkaan
            else
            {
                angle = v.GetDynamicConeAngle(u.GetActionPoints(), 80f);
            }

            // 5) julkaisu
            v.ApplyAndPublishDirectionalVision(facing, angle);
        }
    }
    
    [ClientRpc]
    public void RpcUpdateSingleUnitVision(uint unitNetId, Vector3 facing, float coneAngle)
    {
        if (!Mirror.NetworkClient.spawned.TryGetValue(unitNetId, out var ni) || ni == null) return;

        var u = ni.GetComponent<Unit>();
        var v = u ? u.GetComponent<UnitVision>() : null;
        if (u == null || v == null || !v.IsInitialized) return;

        // Päivitä tämän unitin 360° cache
        v.UpdateVisionNow();
        // Julkaise uusi kartio
        v.ApplyAndPublishDirectionalVision(facing, coneAngle);
    }

    [Server]
    public void ServerPushUnitVision(uint unitNetId, float fx, float fz, float coneDeg)
    {
        // hostille (serverin oma näkymä) päivitys heti paikallisesti:
        if (Mirror.NetworkServer.active)
            RebuildUnitVisionLocal(unitNetId, fx, fz, coneDeg);

        // lähetä myös clienteille
        RpcRebuildUnitVision(unitNetId, fx, fz, coneDeg);
    }

    [ClientRpc]
    private void RpcRebuildUnitVision(uint unitNetId, float fx, float fz, float coneDeg)
    {
        RebuildUnitVisionLocal(unitNetId, fx, fz, coneDeg);
    }

    // Sama koodi serverille ja clientille: EI kutsuta UpdateVisionNow()
    // → ei mitään 360° välikuvia. Julkaistaan suoraan kartio.
    private static void RebuildUnitVisionLocal(uint unitNetId, float fx, float fz, float coneDeg)
    {
        if (!Mirror.NetworkClient.spawned.TryGetValue(unitNetId, out var ni) || ni == null) return;
        var u = ni.GetComponent<Unit>();
        var v = ni.GetComponent<UnitVision>();
        if (u == null || v == null || !v.IsInitialized) return;

        var facing = new Vector3(fx, 0f, fz);
        // EI kutsuta v.UpdateVisionNow() — käytetään olemassa olevaa cachea
        v.ApplyAndPublishDirectionalVision(facing, coneDeg);

        // halutessasi päivitä myös OW-overlay
        v.ShowUnitOverWachVision(facing, coneDeg);
    }

    [Server]
    public void ServerPushTeamVision(int teamId, bool endPhase)
    {
        var ids = CollectTeamUnitIds(teamId);
        RpcRebuildTeamVision(ids, endPhase);
    }

    [Server]
    private static uint[] CollectTeamUnitIds(int teamId)
    {
        var um = UnitManager.Instance;
        var list = um != null ? um.GetAllUnitList() : null;
        var ids = new List<uint>();
        if (list == null) return ids.ToArray();

        foreach (var unit in list)
        {
            if (!unit) continue;

            int t = unit.GetTeamID();
            if (t != teamId) continue;
            if (unit.IsDying() || unit.IsDead()) continue;

            var ni = unit.GetComponent<NetworkIdentity>();
            if (ni != null) ids.Add(ni.netId);
        }
        return ids.ToArray();
    }

    [Command(requiresAuthority = false)]
    public void CmdSetOverwatch(uint unitNetId, bool value, float fx, float fz)
    {
        if (!Mirror.NetworkServer.spawned.TryGetValue(unitNetId, out var ni) || ni == null) return;

        var u  = ni.GetComponent<Unit>();
        var ow = ni.GetComponent<OverwatchAction>();
        if (!u || !ow) return;

        // ⬇️ ÄLÄ koske ow.TargetWorld:iin (setter private) — käytä serverimetodia
        ow.ServerApplyOverwatch(value, new Vector2(fx, fz));
    }

}
