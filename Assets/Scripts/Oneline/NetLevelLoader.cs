using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public class NetLevelLoader : NetworkBehaviour
{
    public static NetLevelLoader Instance { get; private set; }

    [SyncVar] private string _currentLevel;

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
        
        Debug.Log($"[NetLevelLoader] Starting to load '{levelName}'");
        LevelLoader.SetServerLevelReady(false);

        // 1) Tuhoa runtime-verkko-instanssit (sceneId == 0)
        foreach (var ni in FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None))
            if (ni && ni.sceneId == 0) 
            {
                Debug.Log($"[NetLevelLoader] Destroying runtime object: {ni.name}");
                NetworkServer.Destroy(ni.gameObject);
            }

        // 2) Unloadaa kaikki ei-Core -scenet
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            if (s.name == coreName) continue;
            
            Debug.Log($"[NetLevelLoader] Unloading scene: {s.name}");
            yield return SceneManager.UnloadSceneAsync(s);
            i = -1;
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

        // 4) Lataa level additiivisesti
        Debug.Log($"[NetLevelLoader] Loading level scene '{levelName}'");
        var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        var map = SceneManager.GetSceneByName(levelName);
        if (!map.IsValid() || !map.isLoaded)
        {
            Debug.LogError($"[NetLevelLoader] Failed to load '{levelName}'. Is it in Build Settings?");
            yield break;
        }

        // 5) Aseta map aktiiviseksi hetkeksi
        SceneManager.SetActiveScene(map);
        yield return null;

        // 6) TÄRKEÄ: Spawn scene-objektit TÄSSÄ
        Debug.Log($"[NetLevelLoader] Spawning scene objects in '{levelName}'");
        NetworkServer.SpawnObjects();
        
        // Odota että spawnatut objektit ehtivät käynnistyä
        yield return null;
        yield return null;

        Debug.Log($"[NetLevelLoader] Scene objects spawned, MapContentSpawner should now work");

        // 7) Hookit
        EdgeBaker.Instance?.BakeAllEdges();

        // 8) Core takaisin aktiiviseksi
        SceneManager.SetActiveScene(core);
        _currentLevel = levelName;
        
        LevelLoader.SetServerLevelReady(true);
        LevelLoader.RaiseLevelReady(map);
        
        Debug.Log($"[NetLevelLoader] Level '{levelName}' fully loaded and ready");
    }
}
