/*
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class NetTeamVisionSync : NetworkBehaviour
{
    private const int ServerAggregateKeyBase = -1000000000;
    private const int CHUNK_SIZE = 32;
    private const float SEND_DEBOUNCE_SEC = 0.05f;

    public static NetTeamVisionSync Instance { get; private set; }

    // debounce per team
    private readonly Dictionary<int, Coroutine> _pendingSend = new();
    // muutostunniste per team (hash) → ei lähetetä, jos sama union
    private readonly Dictionary<int, int> _lastHash = new();

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        var svc = TeamVisionService.Instance;
        if (svc != null)
            svc.OnTeamVisionChanged += HandleServerTeamVisionChanged;
    }
    public override void OnStopServer()
    {
        var svc = TeamVisionService.Instance;
        if (svc != null)
            svc.OnTeamVisionChanged -= HandleServerTeamVisionChanged;
        base.OnStopServer();
    }

    [Server]
    private void HandleServerTeamVisionChanged(int teamId)
    {
        // Co-Op ja Versus molemmat hyötyvät tästä — mutta lähetetään vain oikealle tiimille
        if (_pendingSend.TryGetValue(teamId, out var co) && co != null)
            StopCoroutine(co);

        _pendingSend[teamId] = StartCoroutine(Co_SendSnapshotAfterDelay(teamId));
    }

    [Server]
    private IEnumerator Co_SendSnapshotAfterDelay(int teamId)
    {
        yield return new WaitForSeconds(SEND_DEBOUNCE_SEC);

        var svc = TeamVisionService.Instance;
        if (svc == null) yield break;

        var snap = svc.GetVisibleTilesSnapshot(teamId);
        var packed = new List<Vector3Int>(snap?.Count ?? 0);
        if (snap != null)
            foreach (var gp in snap)
                packed.Add(new Vector3Int(gp.x, gp.floor, gp.z));

        // Hash – lähetä vain jos union on muuttunut
        int h = 17;
        for (int i = 0; i < packed.Count; i++)
        {
            unchecked
            {
                h = h * 31 + packed[i].x;
                h = h * 31 + packed[i].y;
                h = h * 31 + packed[i].z;
            }
        }
        if (_lastHash.TryGetValue(teamId, out var prev) && prev == h)
        {
            _pendingSend.Remove(teamId);
            yield break; // ei muutosta → ei lähetystä
        }
        _lastHash[teamId] = h;

        // Lähetä VAIN niille yhteyksille, joiden tiimi == teamId
        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn == null || !conn.isReady) continue;

            
            // 🔴 ÄLÄ lähetä hostin local-clientille:
            if (conn == NetworkServer.localConnection)
                continue;
            
            int connTeam = ResolveTeamForConnection(conn);
            if (connTeam != teamId) continue;

            TargetTeamVisionReset(conn, teamId);

            if (packed.Count == 0)
            {
                TargetTeamVisionFinalize(conn, teamId);
                continue;
            }

            for (int i = 0; i < packed.Count; i += CHUNK_SIZE)
            {
                int count = Mathf.Min(CHUNK_SIZE, packed.Count - i);
                var chunk = packed.GetRange(i, count);
                TargetTeamVisionChunk(conn, teamId, chunk);
            }
            TargetTeamVisionFinalize(conn, teamId);
        }

        _pendingSend.Remove(teamId);
    }

    // ------------ TEAM-RESOLVER ------------
    // Palauta, mille tiimille tämä yhteys kuuluu.
    [Server]
    private int ResolveTeamForConnection(NetworkConnectionToClient conn)
    {
        var mode = GameModeManager.SelectedMode;
        if (mode == GameMode.Versus)
        {
            // Hostin local-connection → team 0, kaikki muut clientit → team 1
            return conn == NetworkServer.localConnection ? 0 : 1;
        }
        // Co-Op / Single: kaikki pelaajat tiimi 0
        return 0;
    }

    // ------------ CLIENTIN KERUUPUSKURI ------------
    private readonly Dictionary<int, HashSet<GridPosition>> _clientBuild = new();

    [TargetRpc]
    private void TargetTeamVisionReset(NetworkConnectionToClient conn, int teamId)
    {
        _clientBuild[teamId] = new HashSet<GridPosition>();
    }

    [TargetRpc]
    private void TargetTeamVisionChunk(NetworkConnectionToClient conn, int teamId, List<Vector3Int> chunk)
    {
        if (!_clientBuild.TryGetValue(teamId, out var set))
            _clientBuild[teamId] = set = new HashSet<GridPosition>();

        for (int i = 0; i < chunk.Count; i++)
        {
            var v = chunk[i];
            set.Add(new GridPosition(v.x, v.z, v.y));
        }
    }

    [TargetRpc]
    private void TargetTeamVisionFinalize(NetworkConnectionToClient conn, int teamId)
    {
        if (!_clientBuild.TryGetValue(teamId, out var set))
            set = new HashSet<GridPosition>();

        var svc = TeamVisionService.Instance;
        if (svc != null)
        {
            int serverKey = ServerAggregateKeyBase - teamId;
            svc.ReplaceUnitVision(teamId, serverKey, set);
        }
        _clientBuild.Remove(teamId);
    }
}
*/
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class NetTeamVisionSync : NetworkBehaviour
{
    private const int ServerAggregateKeyBase = -1000000000;
    private const int CHUNK_SIZE = 32;
    private const float SEND_DEBOUNCE_SEC = 0.05f;

    public static NetTeamVisionSync Instance { get; private set; }

    private readonly Dictionary<int, Coroutine> _pendingSend = new();
    private readonly Dictionary<int, int> _lastHash = new();

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        var svc = TeamVisionService.Instance;
        if (svc != null)
        {
            svc.OnTeamVisionChanged += HandleServerTeamVisionChanged;
            Debug.Log("[NetTeamVisionSync] Server started, subscribed to TeamVisionService");
        }
        else
        {
            Debug.LogWarning("[NetTeamVisionSync] TeamVisionService.Instance is null on server start!");
        }
    }

    public override void OnStopServer()
    {
        var svc = TeamVisionService.Instance;
        if (svc != null)
            svc.OnTeamVisionChanged -= HandleServerTeamVisionChanged;
        base.OnStopServer();
    }

    [Server]
    private void HandleServerTeamVisionChanged(int teamId)
    {
        Debug.Log($"[NetTeamVisionSync] HandleServerTeamVisionChanged - Team {teamId}");
        
        if (_pendingSend.TryGetValue(teamId, out var co) && co != null)
            StopCoroutine(co);

        _pendingSend[teamId] = StartCoroutine(Co_SendSnapshotAfterDelay(teamId));
    }

    [Server]
    private IEnumerator Co_SendSnapshotAfterDelay(int teamId)
    {
        yield return new WaitForSeconds(SEND_DEBOUNCE_SEC);

        var svc = TeamVisionService.Instance;
        if (svc == null)
        {
            Debug.LogWarning("[NetTeamVisionSync] TeamVisionService is null, cannot send snapshot");
            yield break;
        }

        var snap = svc.GetVisibleTilesSnapshot(teamId);
        var packed = new List<Vector3Int>(snap?.Count ?? 0);
        if (snap != null)
            foreach (var gp in snap)
                packed.Add(new Vector3Int(gp.x, gp.floor, gp.z));

        Debug.Log($"[NetTeamVisionSync] Preparing to send Team {teamId} vision: {packed.Count} tiles");

        int h = 17;
        for (int i = 0; i < packed.Count; i++)
        {
            unchecked
            {
                h = h * 31 + packed[i].x;
                h = h * 31 + packed[i].y;
                h = h * 31 + packed[i].z;
            }
        }
        if (_lastHash.TryGetValue(teamId, out var prev) && prev == h)
        {
            Debug.Log($"[NetTeamVisionSync] Team {teamId} vision unchanged (hash {h}), skipping send");
            _pendingSend.Remove(teamId);
            yield break;
        }
        _lastHash[teamId] = h;

        int sentCount = 0;
        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn == null || !conn.isReady)
            {
                Debug.Log($"[NetTeamVisionSync] Skipping connection {kv.Key} (null or not ready)");
                continue;
            }

            if (conn == NetworkServer.localConnection)
            {
                Debug.Log($"[NetTeamVisionSync] Skipping localConnection (Host calculates vision locally)");
                continue;
            }

            int connTeam = ResolveTeamForConnection(conn);
            Debug.Log($"[NetTeamVisionSync] Connection {kv.Key}: Team {connTeam}, Target Team {teamId}");
            
            if (connTeam != teamId)
            {
                Debug.Log($"[NetTeamVisionSync] Skipping connection {kv.Key} (team {connTeam} != {teamId})");
                continue;
            }

            Debug.Log($"[NetTeamVisionSync] Sending vision to connection {kv.Key} (Team {connTeam}): {packed.Count} tiles");
            sentCount++;

            TargetTeamVisionReset(conn, teamId);

            if (packed.Count == 0)
            {
                TargetTeamVisionFinalize(conn, teamId);
                continue;
            }

            for (int i = 0; i < packed.Count; i += CHUNK_SIZE)
            {
                int count = Mathf.Min(CHUNK_SIZE, packed.Count - i);
                var chunk = packed.GetRange(i, count);
                TargetTeamVisionChunk(conn, teamId, chunk);
            }
            TargetTeamVisionFinalize(conn, teamId);
        }

        Debug.Log($"[NetTeamVisionSync] Sent Team {teamId} vision to {sentCount} connections");
        _pendingSend.Remove(teamId);
    }

    [Server]
    private int ResolveTeamForConnection(NetworkConnectionToClient conn)
    {
        var mode = GameModeManager.SelectedMode;
        if (mode == GameMode.Versus)
        {
            return conn == NetworkServer.localConnection ? 0 : 1;
        }
        return 0;
    }

    private readonly Dictionary<int, HashSet<GridPosition>> _clientBuild = new();

    [TargetRpc]
    private void TargetTeamVisionReset(NetworkConnectionToClient conn, int teamId)
    {
        Debug.Log($"[NetTeamVisionSync CLIENT] TargetTeamVisionReset - Team {teamId}");
        _clientBuild[teamId] = new HashSet<GridPosition>();
    }

    [TargetRpc]
    private void TargetTeamVisionChunk(NetworkConnectionToClient conn, int teamId, List<Vector3Int> chunk)
    {
        if (!_clientBuild.TryGetValue(teamId, out var set))
            _clientBuild[teamId] = set = new HashSet<GridPosition>();

        for (int i = 0; i < chunk.Count; i++)
        {
            var v = chunk[i];
            set.Add(new GridPosition(v.x, v.z, v.y));
        }
        Debug.Log($"[NetTeamVisionSync CLIENT] TargetTeamVisionChunk - Team {teamId}, received {chunk.Count} tiles, total {set.Count}");
    }

    [TargetRpc]
    private void TargetTeamVisionFinalize(NetworkConnectionToClient conn, int teamId)
    {
        if (!_clientBuild.TryGetValue(teamId, out var set))
            set = new HashSet<GridPosition>();

        Debug.Log($"[NetTeamVisionSync CLIENT] TargetTeamVisionFinalize - Team {teamId}, total {set.Count} tiles");

        var svc = TeamVisionService.Instance;
        if (svc != null)
        {
            int serverKey = ServerAggregateKeyBase - teamId;
            svc.ReplaceUnitVision(teamId, serverKey, set);
            Debug.Log($"[NetTeamVisionSync CLIENT] Applied Team {teamId} vision to TeamVisionService");
        }
        else
        {
            Debug.LogWarning($"[NetTeamVisionSync CLIENT] TeamVisionService is null, cannot apply vision!");
        }
        _clientBuild.Remove(teamId);
    }

    [Server]
    public void ServerPushFullVisionTo(NetworkConnectionToClient conn)
    {
        if (!isServer || conn == null || !conn.isReady) return;

        // Älä lähetä hostin local-clientille – host piirtää paikallisesti jo nyt
        if (conn == NetworkServer.localConnection) return;

        int teamId = ResolveTeamForConnection(conn);     // Co-opissa palauttaa 0
        var svc = TeamVisionService.Instance;
        if (svc == null) return;

        // Kerää union-snap (GridPosition → pakataan Vector3Int:ksi kuten muissa RPC:issä)
        var snap = svc.GetVisibleTilesSnapshot(teamId);
        var packed = new List<Vector3Int>(snap?.Count ?? 0);
        if (snap != null)
            foreach (var gp in snap)
                packed.Add(new Vector3Int(gp.x, gp.floor, gp.z));

        // Lähetä Reset → Chunkit → Finalize tälle YHDELLE clientille
        TargetTeamVisionReset(conn, teamId);

        for (int i = 0; i < packed.Count; i += CHUNK_SIZE)
        {
            int count = Mathf.Min(CHUNK_SIZE, packed.Count - i);
            var chunk = packed.GetRange(i, count);
            TargetTeamVisionChunk(conn, teamId, chunk);
        }

        TargetTeamVisionFinalize(conn, teamId);
    }

    // (valinnainen, mutta kätevä): puskeminen kaikille valmiille clienteille kerralla
    [Server]
    public void ServerPushFullVisionToAll()
    {
        foreach (var kv in NetworkServer.connections)
        {
            var c = kv.Value;
            if (c == null || !c.isReady) continue;
            if (c == NetworkServer.localConnection) continue; // ei hostin localille
            ServerPushFullVisionTo(c);
        }
    }
}
