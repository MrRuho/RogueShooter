using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public class NetLevelLoader : NetworkBehaviour
{

    public static NetLevelLoader Instance { get; private set; }

    [SyncVar(hook = nameof(OnLevelChanged))]
    private string _currentLevel;

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

        Debug.Log($"[NetLevelLoader] OnStartServer → loading '{_currentLevel}'");
        StartCo(Co_LoadLevel(_currentLevel));
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isServer && !string.IsNullOrEmpty(_currentLevel))
            StartCoroutine(Co_LoadLevel_Client(_currentLevel));
    }

    void OnLevelChanged(string oldValue, string newValue)
    {
        /*
        if (isServer) return;
        if (!string.IsNullOrEmpty(newValue))
            StartCoroutine(Co_LoadLevel_Client(newValue));
        */
        if (isServer) return;
        if (string.IsNullOrEmpty(newValue) || newValue.StartsWith("__RELOAD_TICK__")) return;
         StartCoroutine(Co_LoadLevel_Client(newValue));
    }

    [Client]
    private IEnumerator Co_LoadLevel_Client(string levelName)
    {
        string coreName = LevelLoader.Instance?.CoreSceneName ?? "Core";

        // varmista Core ladattuna + aktiivinen
        var core = SceneManager.GetSceneByName(coreName);
        if (!core.IsValid() || !core.isLoaded)
        {
            var loadCore = SceneManager.LoadSceneAsync(coreName, LoadSceneMode.Additive);
            while (!loadCore.isDone) yield return null;
            core = SceneManager.GetSceneByName(coreName);
        }
        SceneManager.SetActiveScene(core);

        // unload kaikki ei-Core
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.name != coreName)
            {
                var op = SceneManager.UnloadSceneAsync(s);
                if (op != null) while (!op.isDone) yield return null;
                i = -1;
            }
        }

        // lataa level additiivisesti
        var op2 = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!op2.isDone) yield return null;

        var map = SceneManager.GetSceneByName(levelName);
        if (!map.IsValid() || !map.isLoaded)
        {
            Debug.LogError($"[NetLevelLoader] Client failed to load '{levelName}' (Build Settings?).");
            yield break;
        }

        // hetkeksi aktiivinen, sitten Core takaisin
        SceneManager.SetActiveScene(map);
        yield return null;
        SceneManager.SetActiveScene(core);

        // ilmoita että clientin level on valmis (jos teillä on tällainen event)
        LevelLoader.RaiseLevelReady(map);
    }

    private void StartCo(IEnumerator r)
    {
        if (isActiveAndEnabled) StartCoroutine(r);
        else GlobalCoroutineHost.StartRoutine(r);
    }

    [Server]
    public void ServerReloadCurrentLevel()
    {
        
        var target = string.IsNullOrEmpty(_currentLevel)
            ? (LevelLoader.Instance?.DefaultLevel ?? "Level 0")
            : _currentLevel;
        StopAllCoroutines();
        StartCo(Co_LoadLevel(target));
        
       //StopAllCoroutines();
       // StartCoroutine(Co_ReloadSame());
    }

    [Server]
    IEnumerator Co_ReloadSame()
    {
        // Pakota hook käyntiin myös “samaan” kenttään
        var lvl = string.IsNullOrEmpty(_currentLevel) ? (LevelLoader.Instance?.DefaultLevel ?? "Level 0") : _currentLevel;

        // 1) kerro klienteille “väliarvo” → ei ladata tyhjää (hookissa ohita null/tyhjä)
        _currentLevel = "__RELOAD_TICK__" + Time.frameCount; 
        yield return null;

        // 2) normaali server-lataus
        yield return Co_LoadLevel(lvl);

        // 3) aseta oikea nimi (hook → client Co_LoadLevel_Client)
        _currentLevel = lvl;
    }

    [Server]
    public void ServerLoadLevel(string levelName)
    {
        StopAllCoroutines();
        StartCo(Co_LoadLevel(levelName));
    }

    [Server]
    private IEnumerator Co_LoadLevel(string levelName)
    {
        var coreName = LevelLoader.Instance?.CoreSceneName ?? "Core";

        Debug.Log($"[NetLevelLoader] ===== STARTING LEVEL LOAD: '{levelName}' =====");

        // Lista kaikki ladatut scenet ENNEN unloadausta
        Debug.Log($"[NetLevelLoader] Currently loaded scenes BEFORE unload:");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            Debug.Log($"[NetLevelLoader]   Scene {i}: '{s.name}' (isLoaded: {s.isLoaded}, path: {s.path})");
        }

        LevelLoader.SetServerLevelReady(false);

        // 1) Tuhoa runtime-verkko-instanssit
        int destroyedCount = 0;
        foreach (var ni in FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None))
            if (ni && ni.sceneId == 0)
            {
                Debug.Log($"[NetLevelLoader] Destroying runtime object: {ni.name}");
                NetworkServer.Destroy(ni.gameObject);
                destroyedCount++;
            }
        Debug.Log($"[NetLevelLoader] Destroyed {destroyedCount} runtime network objects");

        // 2) Unloadaa kaikki ei-Core -scenet
        Debug.Log($"[NetLevelLoader] Starting scene unload loop...");
        int unloadedCount = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            Debug.Log($"[NetLevelLoader] Checking scene {i}: '{s.name}', isLoaded={s.isLoaded}, isCore={s.name == coreName}");

            if (!s.isLoaded)
            {
                Debug.Log($"[NetLevelLoader] Skipping '{s.name}' - not loaded");
                continue;
            }
            if (s.name == coreName)
            {
                Debug.Log($"[NetLevelLoader] Skipping '{s.name}' - is Core scene");
                continue;
            }

            Debug.Log($"[NetLevelLoader] UNLOADING scene: '{s.name}'");
            yield return SceneManager.UnloadSceneAsync(s);
            unloadedCount++;
            Debug.Log($"[NetLevelLoader] Unloaded '{s.name}', restarting loop");
            i = -1;
        }
        Debug.Log($"[NetLevelLoader] Unloaded {unloadedCount} scenes");

        // Lista scenet JÄLKEEN unloadauksen
        Debug.Log($"[NetLevelLoader] Currently loaded scenes AFTER unload:");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            Debug.Log($"[NetLevelLoader]   Scene {i}: '{s.name}' (isLoaded: {s.isLoaded})");
        }

        // 3) Varmista Core aktiiviseksi
        var core = SceneManager.GetSceneByName(coreName);
        if (!core.IsValid() || !core.isLoaded)
        {
            Debug.Log($"[NetLevelLoader] Loading Core scene");
            var loadCore = SceneManager.LoadSceneAsync(coreName, LoadSceneMode.Additive);
            while (!loadCore.isDone) yield return null;
            core = SceneManager.GetSceneByName(coreName);
        }
        SceneManager.SetActiveScene(core);
        Debug.Log($"[NetLevelLoader] Core scene is active");

        // 4) Lataa level additiivisesti
        Debug.Log($"[NetLevelLoader] Loading NEW level scene '{levelName}'");
        var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        var map = SceneManager.GetSceneByName(levelName);
        if (!map.IsValid() || !map.isLoaded)
        {
            Debug.LogError($"[NetLevelLoader] Failed to load '{levelName}'. Is it in Build Settings?");
            yield break;
        }

        Debug.Log($"[NetLevelLoader] Loaded '{levelName}', setting as active scene temporarily");

        // 5) Aseta map aktiiviseksi hetkeksi
        SceneManager.SetActiveScene(map);
        yield return null;

        // 6) Spawn scene-objektit
        Debug.Log($"[NetLevelLoader] Calling NetworkServer.SpawnObjects()");
        NetworkServer.SpawnObjects();

        yield return null;
        yield return null;

        Debug.Log($"[NetLevelLoader] Scene objects spawned");

        // 7) Hookit
        EdgeBaker.Instance?.BakeAllEdges();

        // 8) Core takaisin aktiiviseksi
        SceneManager.SetActiveScene(core);
        _currentLevel = levelName;

        LevelLoader.SetServerLevelReady(true);
        LevelLoader.RaiseLevelReady(map);

        Debug.Log($"[NetLevelLoader] ===== LEVEL LOAD COMPLETE: '{levelName}' =====");

        // Lista lopulliset scenet
        Debug.Log($"[NetLevelLoader] Final loaded scenes:");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            Debug.Log($"[NetLevelLoader]   Scene {i}: '{s.name}' (isLoaded: {s.isLoaded})");
        }
    }
}
