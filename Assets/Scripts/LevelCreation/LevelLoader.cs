using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public class LevelLoader : MonoBehaviour
{
    
    [SerializeField] bool offlineWarmBoot = true;
    [SerializeField] float warmBootDelay = 0.25f; // sekuntia

    public static LevelLoader Instance { get; private set; }

    [SerializeField] private string coreSceneName = "Core";
    [SerializeField] private string defaultLevel  = "Level 0";

    public string CoreSceneName => coreSceneName;
    public string DefaultLevel  => defaultLevel;
    public string CurrentLevel  { get; private set; }

    [SerializeField] private bool forceDefaultOnStart = true;

    // --- NÄMÄ KAKSI UUTTA ---
    public static bool IsServerLevelReady { get; private set; }
    public static void SetServerLevelReady(bool ready) => IsServerLevelReady = ready;

    // Event pysyy LevelLoaderissa; muiden pitää kutsua RaiseLevelReady(...)
    public static event Action<Scene> LevelReady;
    public static void RaiseLevelReady(Scene scene) => LevelReady?.Invoke(scene);

    private void Awake()
    {
        
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (string.IsNullOrEmpty(CurrentLevel)) CurrentLevel = defaultLevel;
    }

    private void Start()
    {

        // ONLINE → NetLevelLoader hoitaa
        if (NetworkServer.active || NetworkClient.active) 
        {
            Debug.Log("[LevelLoader] Net mode → NetLevelLoader will load.");
            return;
        }

        // OFFLINE → tässä vasta ladataan lokaalisti
       // Debug.Log($"[LevelLoader] Offline start → '{defaultLevel}'");
       // StartCo(Co_LoadLevel_Local(defaultLevel));
        //if (offlineWarmBoot) StartCo(Co_OfflineWarmBoot());
    }
    
    private IEnumerator Co_OfflineWarmBoot()
    {
        yield return new WaitForSeconds(warmBootDelay);

        // jos ei ole ladattuna yhtään ei-Core -sceneä, lataa default
        bool anyLevelLoaded = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.name != coreSceneName) { anyLevelLoaded = true; break; }
        }

        if (!anyLevelLoaded)
        {
            Debug.Log($"[LevelLoader] WarmBoot: no level yet → loading '{defaultLevel}'");
            StartCo(Co_LoadLevel_Local(defaultLevel));
        }
    }

    private void StartCo(IEnumerator r)
    {
        if (isActiveAndEnabled) StartCoroutine(r);
        else GlobalCoroutineHost.StartRoutine(r);
    }

    public void StartLocalReload(string levelName = null)
    {
        var target = string.IsNullOrWhiteSpace(levelName) ? CurrentLevel ?? defaultLevel : levelName;
        StopAllCoroutines();
        StartCo(Co_LoadLevel_Local(target));
    }

    private bool _localLoading;

    private IEnumerator Co_LoadLevel_Local(string levelName)
    {
        if (_localLoading) yield break;
        _localLoading = true;
        try
        {
            // ... NYKYINEN sisältö ...
            var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
            while (!op.isDone) yield return null;

            var map = SceneManager.GetSceneByName(levelName);
            if (!map.IsValid() || !map.isLoaded)
            {
                Debug.LogError($"[LevelLoader] '{levelName}' is not loaded (Build Settings?) aborting.");
                yield break;
            }

            SceneManager.SetActiveScene(map);
            yield return null;

            EdgeBaker.Instance?.BakeAllEdges();
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(coreSceneName));

            CurrentLevel = levelName;
            RaiseLevelReady(map);
        }
        finally { _localLoading = false; }
    }

}
