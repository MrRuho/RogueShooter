using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetLevelLoader : NetworkBehaviour
{
    public static NetLevelLoader Instance { get; private set; }

    [SyncVar(hook = nameof(OnLevelChanged))]
    private string _currentLevel;

    private int _reloadTick = 0;
    private readonly HashSet<int> _clientReadyAcks = new HashSet<int>();

    private static bool _clientIsLoading;
    private static string _clientPreparedLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("There's more than one NetLevelLoader! " + transform + " - " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (string.IsNullOrEmpty(_currentLevel))
            _currentLevel = LevelLoader.Instance?.DefaultLevel ?? "Level 0";

        Debug.Log($"[NetLevelLoader] (SERVER) OnStartServer → loading '{_currentLevel}'");
        StartCo(Co_LoadLevel(_currentLevel));
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
    }

    void OnLevelChanged(string oldValue, string newValue)
    {
        if (isServer) return;
        if (string.IsNullOrEmpty(newValue)) return;
        if (newValue.StartsWith("__RELOAD_TICK__")) return;

        if (_clientPreparedLevel == newValue)
        {
            Debug.Log($"[NetLevelLoader] (CLIENT) OnLevelChanged ignored - already prepared '{newValue}'");
            _clientPreparedLevel = null;
            return;
        }

        Debug.Log($"[NetLevelLoader] (CLIENT) OnLevelChanged triggered → '{newValue}'");
        StartCoroutine(Co_LoadLevel_Client(newValue));
    }

    [Server]
    public void ServerLoadLevel(string levelName)
    {
        StopAllCoroutines();
        StartCo(Co_ReloadLevel_All(levelName));
    }

    [Server]
    public void ServerReloadCurrentLevel()
    {
        var target = string.IsNullOrEmpty(_currentLevel)
            ? (LevelLoader.Instance?.DefaultLevel ?? "Level 0")
            : _currentLevel;

        StopAllCoroutines();
        StartCo(Co_ReloadLevel_All(target));
    }

    [Server]
    private IEnumerator Co_ReloadLevel_All(string levelName)
    {
        string coreName = LevelLoader.Instance?.CoreSceneName ?? "Core";

        _reloadTick++;
        _clientReadyAcks.Clear();

        Debug.Log($"[NetLevelLoader] (SERVER) ===== RELOAD ALL START → '{levelName}', tick={_reloadTick} =====");

        _currentLevel = $"__RELOAD_TICK__{_reloadTick}";

        RpcClientPrepareReload(coreName, levelName, _reloadTick);

        int expectedClients = ExpectedClientCount();
        Debug.Log($"[NetLevelLoader] (SERVER) Waiting for {expectedClients} clients to be ready...");

        float timeout = 15f;
        float elapsed = 0f;

        while (_clientReadyAcks.Count < expectedClients && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (elapsed >= timeout)
        {
            Debug.LogWarning($"[NetLevelLoader] (SERVER) Timeout! Got {_clientReadyAcks.Count}/{expectedClients} acks");
        }
        else
        {
            Debug.Log($"[NetLevelLoader] (SERVER) All {_clientReadyAcks.Count} clients ready!");
        }

        yield return StartCoroutine(Co_LoadLevel(levelName));
    }

    [Server]
    private IEnumerator Co_LoadLevel(string levelName)
    {
        var coreName = LevelLoader.Instance?.CoreSceneName ?? "Core";

        Debug.Log($"[NetLevelLoader] (SERVER) ===== STARTING LEVEL LOAD: '{levelName}' =====");

        LevelLoader.SetServerLevelReady(false);

        int destroyedCount = 0;
        foreach (var ni in FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None))
        {
            if (ni && ni.sceneId == 0)
            {
                NetworkServer.Destroy(ni.gameObject);
                destroyedCount++;
            }
        }
        Debug.Log($"[NetLevelLoader] (SERVER) Destroyed {destroyedCount} runtime network objects");

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            if (s.name == coreName) continue;

            Debug.Log($"[NetLevelLoader] (SERVER) Unloading scene '{s.name}'");
            yield return SceneManager.UnloadSceneAsync(s);
            i = -1;
        }

        var core = SceneManager.GetSceneByName(coreName);
        if (!core.IsValid() || !core.isLoaded)
        {
            Debug.Log($"[NetLevelLoader] (SERVER) Loading Core scene");
            var loadCore = SceneManager.LoadSceneAsync(coreName, LoadSceneMode.Additive);
            while (!loadCore.isDone) yield return null;
            core = SceneManager.GetSceneByName(coreName);
        }
        SceneManager.SetActiveScene(core);

        Debug.Log($"[NetLevelLoader] (SERVER) Loading level '{levelName}'");
        var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        var map = SceneManager.GetSceneByName(levelName);
        if (!map.IsValid() || !map.isLoaded)
        {
            Debug.LogError($"[NetLevelLoader] (SERVER) Failed to load '{levelName}'. Is it in Build Settings?");
            yield break;
        }

        SceneManager.SetActiveScene(map);
        yield return null;

        Debug.Log($"[NetLevelLoader] (SERVER) Spawning network objects");
        NetworkServer.SpawnObjects();
        yield return null;
        yield return null;

        EdgeBaker.Instance?.BakeAllEdges();

        SceneManager.SetActiveScene(core);
        _currentLevel = levelName;

        LevelLoader.SetServerLevelReady(true);
        LevelLoader.RaiseLevelReady(map);

        Debug.Log($"[NetLevelLoader] (SERVER) ===== LEVEL LOAD COMPLETE: '{levelName}' =====");
    }

    [ClientRpc]
    private void RpcClientPrepareReload(string coreName, string levelName, int tick)
    {
        if (isServer) return;

        StartCoroutine(Co_ClientPrepareAndAck(coreName, levelName, tick));
    }

    [Client]
    private IEnumerator Co_ClientPrepareAndAck(string coreName, string levelName, int tick)
    {
        Debug.Log($"[NetLevelLoader] (CLIENT) ===== PREPARE RELOAD START → '{levelName}', tick={tick} =====");

        int n = DebrisUtil.DestroyAllDebrisExceptCore(coreName);
        if (n > 0) Debug.Log($"[NetLevelLoader] (CLIENT) Cleared {n} debris objects");

        yield return Co_LoadLevel_Client_Internal(levelName);

        _clientPreparedLevel = levelName;

        Debug.Log($"[NetLevelLoader] (CLIENT) Scene ready, sending ACK for tick {tick}");
        CmdAckSceneReady(tick);
    }

    [Command(requiresAuthority = false)]
    void CmdAckSceneReady(int tick, NetworkConnectionToClient sender = null)
    {
        if (tick != _reloadTick || sender == null) return;

        Debug.Log($"[NetLevelLoader] (SERVER) Received ACK from conn {sender.connectionId} for tick {tick}");
        _clientReadyAcks.Add(sender.connectionId);
    }

    [Server]
    int ExpectedClientCount()
    {
        int c = 0;
        foreach (var kv in NetworkServer.connections)
        {
            if (kv.Value != null && kv.Value.isAuthenticated)
            {
                if (kv.Value == NetworkServer.localConnection)
                    continue;
                c++;
            }
        }
        return c;
    }

    [Client]
    private IEnumerator Co_LoadLevel_Client(string levelName)
    {
        if (_clientIsLoading)
        {
            Debug.Log($"[NetLevelLoader] (CLIENT) Already loading, skipping duplicate");
            yield break;
        }

        _clientIsLoading = true;

        try
        {
            yield return Co_LoadLevel_Client_Internal(levelName);
        }
        finally
        {
            _clientIsLoading = false;
        }
    }

    [Client]
    private IEnumerator Co_LoadLevel_Client_Internal(string levelName)
    {
        string coreName = LevelLoader.Instance?.CoreSceneName ?? "Core";

        Debug.Log($"[NetLevelLoader] (CLIENT) Start reload → '{levelName}'");

        var core = SceneManager.GetSceneByName(coreName);
        if (!core.IsValid() || !core.isLoaded)
        {
            Debug.Log($"[NetLevelLoader] (CLIENT) Loading Core scene");
            var loadCore = SceneManager.LoadSceneAsync(coreName, LoadSceneMode.Additive);
            while (!loadCore.isDone) yield return null;
            core = SceneManager.GetSceneByName(coreName);
        }
        SceneManager.SetActiveScene(core);

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded || s.name == coreName) continue;

            Debug.Log($"[NetLevelLoader] (CLIENT) Unloading scene '{s.name}'");
            yield return SceneManager.UnloadSceneAsync(s);
            i = -1;
        }

        Debug.Log($"[NetLevelLoader] (CLIENT) Loading level '{levelName}'");
        var op2 = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!op2.isDone) yield return null;

        var map = SceneManager.GetSceneByName(levelName);
        if (!map.IsValid() || !map.isLoaded)
        {
            Debug.LogError($"[NetLevelLoader] (CLIENT) Failed to load '{levelName}'. Is it in Build Settings?");
            yield break;
        }

        SceneManager.SetActiveScene(map);
        yield return null;
        SceneManager.SetActiveScene(core);

        LevelLoader.RaiseLevelReady(map);

        Debug.Log($"[NetLevelLoader] (CLIENT) Reload complete → '{levelName}'");
    }

    private void StartCo(IEnumerator r)
    {
        if (isActiveAndEnabled) StartCoroutine(r);
        else GlobalCoroutineHost.StartRoutine(r);
    }
}
