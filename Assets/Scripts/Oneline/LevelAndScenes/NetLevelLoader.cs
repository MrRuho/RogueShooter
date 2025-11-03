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

    [SerializeField] private string _fallbackDefaultLevelName; // valinnainen: aseta Inspectorissa jos haluat
    

    private int _reloadTick = 0;
    private readonly HashSet<int> _clientReadyAcks = new HashSet<int>();

    private static bool _clientIsLoading;
    private static string _clientPreparedLevel;

    [Header("Catalog")]
    [SerializeField] private LevelCatalog catalog;
    [SerializeField] private int currentIndex = -1;  // tällä hetkellä ladattu kartta (katalogin indeksi)
    
    public int CurrentIndex => currentIndex;

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

        int idx = ResolveDefaultIndex();
        var sceneName = catalog.Get(idx).sceneName;
        _currentLevel = sceneName;
        currentIndex = idx;
        StartCo(Co_LoadLevel(idx));
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
            _clientPreparedLevel = null;
            return;
        }
        StartCoroutine(Co_LoadLevel_Client(newValue));
    }

    [Server]
    public void ServerLoadLevel(string levelName)
    {    
        if (string.IsNullOrEmpty(levelName))
        {
            Debug.LogError("[NetLevelLoader] ServerLoadLevel sai tyhjän scenenimen.");
            return;
        }

        StopAllCoroutines();
        StartCo(Co_ReloadLevel_All(levelName));
    }

    [Server]
    private IEnumerator Co_ReloadLevel_All(string levelName)
    {
        string coreName = LevelLoader.Instance?.CoreSceneName ?? "Core";

        _reloadTick++;
        _clientReadyAcks.Clear();

        _currentLevel = $"__RELOAD_TICK__{_reloadTick}";
        RpcClientPrepareReload(coreName, levelName, _reloadTick);

        int expectedClients = ExpectedClientCount();
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

        int idx = IndexOfScene(levelName);
        if (idx < 0) idx = ResolveDefaultIndex();

        _currentLevel = levelName; 
        yield return StartCoroutine(Co_LoadLevel(idx)); 
    }

    
    private IEnumerator Co_LoadLevel(int index) 
    {
        // 0) Siivous + unload edellinen (jo teillä koodissa)
        UnitManager.Instance?.ClearAllUnitLists();

        var current = CurrentSceneName;
        if (!string.IsNullOrEmpty(current)) {
            var s = SceneManager.GetSceneByName(current);
            if (s.isLoaded) {
                var opUnload = SceneManager.UnloadSceneAsync(s);
                while (!opUnload.isDone) yield return null;
            }
        }

        // 1) Lataa uusi additiivisesti (jo teillä)
        var entry = catalog.Get(index);
        var sceneName = entry.sceneName;
        _currentLevel = sceneName;

        var opLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (!opLoad.isDone) yield return null;

        // 2) Aseta map aktiiviseksi ja anna 1–2 framea heräämiseen
        Scene newScene = SceneManager.GetSceneByName(sceneName);
        SceneManager.SetActiveScene(newScene);
        yield return null;

        NetworkServer.SpawnObjects();       // pakollinen additiivisen scenen scene-objekteille
        yield return null;
        EdgeBaker.Instance?.BakeAllEdges();
        MousePlaneMap.Instance.Rebuild();

        // 3) Päivitä indeksi vasta onnistumisen jälkeen (jo teillä)
        currentIndex = index;

        // 4) Palauta Core aktiiviseksi jos haluat samaan tapaan kuin toisessa polussa
        var coreName = LevelLoader.Instance?.CoreSceneName ?? "Core";
        var core = SceneManager.GetSceneByName(coreName);
        if (core.IsValid() && core.isLoaded)
            SceneManager.SetActiveScene(core);

        // 5) Ilmoita että servupuoli on valmis → käynnistää OnLevelReady_Server-ketjun
        LevelLoader.SetServerLevelReady(true);
        LevelLoader.RaiseLevelReady(newScene);   // 🔔 tämä käynnistää GameNetworkManagerin spawnit

        // 6) (valinn.) UI-siivo RPC: kuten teillä jo on
        RpcOnLevelLoaded(sceneName, currentIndex);
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

        DebrisUtil.DestroyAllDebrisExceptCore(coreName);

        yield return Co_LoadLevel_Client_Internal(levelName);

        _clientPreparedLevel = levelName;

        CmdAckSceneReady(tick);
    }

    [Command(requiresAuthority = false)]
    void CmdAckSceneReady(int tick, NetworkConnectionToClient sender = null)
    {
        if (tick != _reloadTick || sender == null) return;
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

        var core = SceneManager.GetSceneByName(coreName);
        if (!core.IsValid() || !core.isLoaded)
        {
            var loadCore = SceneManager.LoadSceneAsync(coreName, LoadSceneMode.Additive);
            while (!loadCore.isDone) yield return null;
            core = SceneManager.GetSceneByName(coreName);
        }
        SceneManager.SetActiveScene(core);

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded || s.name == coreName) continue;

            yield return SceneManager.UnloadSceneAsync(s);
            i = -1;
        }

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
    }

    private void StartCo(IEnumerator r)
    {
        if (isActiveAndEnabled) StartCoroutine(r);
    }

    [Server]
    public void ServerReloadCurrentLevel()
    {
        if (currentIndex < 0) currentIndex = ResolveDefaultIndex();
        
        var entry = catalog.Get(currentIndex);
        if (entry == null)
        {
            Debug.LogError($"[NetLevelLoader] Cannot reload - invalid index {currentIndex}");
            return;
        }
        
        string sceneName = entry.sceneName;
  
        StopAllCoroutines();
        StartCo(Co_ReloadLevel_All(sceneName));
    }

    public string CurrentSceneName =>
        (catalog != null && catalog.Count > 0) ? catalog.Get(currentIndex)?.sceneName : null;



    // Julkinen entry point: lataa nimen perusteella
    [Server]
    public void ServerLoadLevelByName(string sceneName) {
        if (catalog == null) { Debug.LogError("[NetLevelLoader] Catalog puuttuu"); return; }
        int i = catalog.IndexOfScene(sceneName);
        if (i < 0) { Debug.LogError($"[NetLevelLoader] Scene '{sceneName}' ei löydy catalogista"); return; }
        ServerLoadLevelByIndex(i);
    }

    // Julkinen entry point: lataa indeksillä
    [Server]
    public void ServerLoadLevelByIndex(int index) {
        if (catalog == null || catalog.Count == 0) { Debug.LogError("[NetLevelLoader] Catalog tyhjä"); return; }
        if (index < 0 || index >= catalog.Count) { Debug.LogError("[NetLevelLoader] Index out of range"); return; }

        StartCoroutine(Co_LoadLevel(index));
    }

    [Server]
    public void ServerLoadNextLevelLoop() {
        if (catalog == null || catalog.Count == 0) return;
        int next = (currentIndex + 1) % catalog.Count;
        ServerLoadLevelByIndex(next);
    }

    [ClientRpc]
    void RpcOnLevelLoaded(string sceneName, int index)
    {
        // Client-päässä: UI/HUD siivous, WinPanel piiloon jne.
        var win = FindFirstObjectByType<WinBattle>(FindObjectsInactive.Include);
        if (win) win.HideEndPanel();
    }

    public string ResolveDefaultLevelName()
    {
        // 1) Jos _currentLevel on jo asetettu (esim. edellisestä pelistä / valikosta)
        if (!string.IsNullOrEmpty(_currentLevel)) return _currentLevel;

        // 2) Jos LevelLoaderissa on määritelty oletus
        if (LevelLoader.Instance && !string.IsNullOrEmpty(LevelLoader.Instance.DefaultLevel))
            return LevelLoader.Instance.DefaultLevel;
        
        // 3) Lopuksi oma (inspectorista asetettava) fallback
        if (!string.IsNullOrEmpty(_fallbackDefaultLevelName))
            return _fallbackDefaultLevelName;
        
        // 4) Ei keksitty mitään
        Debug.LogError("[NetLevelLoader] ResolveDefaultLevelName() epäonnistui: ei current/default/fallback-nimeä.");
        return null;
    }

    public int IndexOfScene(string sceneName)
    {
        if (catalog == null) return -1;
        return catalog.IndexOfScene(sceneName); // LevelCatalogissa on tämä valmiina
    }

    // 2) Oletusindeksin ratkaisu ilman defaultIndex-kenttää
    public int ResolveDefaultIndex()
    {
        // Jos katalogi puuttuu/tyhjä → 0
        if (catalog == null || catalog.Count == 0) return 0;

        // a) Yritä LevelLoaderin oletusnimeä
        if (LevelLoader.Instance && !string.IsNullOrEmpty(LevelLoader.Instance.DefaultLevel))
        {
            int idx = catalog.IndexOfScene(LevelLoader.Instance.DefaultLevel);
            if (idx >= 0) return idx;
        }

        // b) Jos _currentLevel on asetettu (esim. aiemmasta pelistä)
        if (!string.IsNullOrEmpty(_currentLevel))
        {
            int idx = catalog.IndexOfScene(_currentLevel);
            if (idx >= 0) return idx;
        }

        // c) Fallback: katalogin ensimmäinen
        return 0;
    }
}
