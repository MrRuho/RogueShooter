using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public static LevelLoader Instance { get; private set; }

    [SerializeField] private string coreSceneName = "Core";
    [SerializeField] private string defaultLevel = "Level 0";

    public string CoreSceneName => coreSceneName;
    public string DefaultLevel => defaultLevel;
    public string CurrentLevel { get; private set; }

    // [SerializeField] private bool forceDefaultOnStart = true;

    // --- NÄMÄ KAKSI UUTTA ---
    public static bool IsServerLevelReady { get; private set; }
    public static void SetServerLevelReady(bool ready) => IsServerLevelReady = ready;

    // Event pysyy LevelLoaderissa; muiden pitää kutsua RaiseLevelReady(...)
    public static event Action<Scene> LevelReady;
    public static void RaiseLevelReady(Scene scene) => LevelReady?.Invoke(scene);

    [SerializeField] private LevelCatalog catalog;
    [SerializeField] private int currentIndex;

#if UNITY_EDITOR
    private const string EDITOR_REQ_KEY = "RS_EditorRequestedLevel";
#endif

    private void Awake()
    {

        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

#if UNITY_EDITOR

        // 1) Lue editorin one-shot-pyyntö
        string req = PlayerPrefs.GetString(EDITOR_REQ_KEY, string.Empty);
        if (!string.IsNullOrEmpty(req))
        {
            // 2) Siivoa avain heti (one-shot)
            PlayerPrefs.DeleteKey(EDITOR_REQ_KEY);

            // 3) Varmista että kenttä on ladattavissa (Build Settingsissä)
            if (Application.CanStreamedLevelBeLoaded(req))
            {
                // 4) Ohjaa sekä CurrentLevel että DefaultLevel tähän
                CurrentLevel = req;
                defaultLevel = req;
            }
            else
            {
                Debug.LogWarning($"[LevelLoader] Pyydetty '{req}', mutta sitä ei löydy Build Settingsistä.");
            }
        }
#endif
       // if (string.IsNullOrEmpty(CurrentLevel)) CurrentLevel = defaultLevel;
    }

    public void LoadByIndex(int index)
    {
        StartCoroutine(Co_LoadLocal(index));
    }
    
    public void Reload() => LoadByIndex(currentIndex);

    private IEnumerator Co_LoadLocal(int index)
    {
        // Unload edellinen, load uusi, set active...
        // Sama pattern kuin NetLevelLoaderissa, mutta ilman RPC:itä
        currentIndex = index;
        yield break;
    }

    public void StartLocalReload(string levelName = null)
    {
        var target = string.IsNullOrWhiteSpace(levelName) ? CurrentLevel ?? defaultLevel : levelName;
        StopAllCoroutines();
    }

    // Kutsu tämä Play Again -napista OFFLINE-tilassa
    public void ReloadOffline(string levelName = null)
    {
        var target = string.IsNullOrWhiteSpace(levelName) ? (CurrentLevel ?? DefaultLevel) : levelName;
        StopAllCoroutines();
        StartCoroutine(Co_ReloadOffline(target));
    }

    private IEnumerator Co_ReloadOffline(string levelName)
    {
        string coreName = CoreSceneName ?? "Core";

        // 0) Lista ennen (debug)
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
        }

        // 1) Pura kaikki ei-Core -scenet
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded || s.name == coreName) continue;

            var op = SceneManager.UnloadSceneAsync(s);
            if (op != null) while (!op.isDone) yield return null;

            // koska sceneCount muuttuu, aloita alusta
            i = -1;
        }

        // 2) Varmista Core ladattu + aktiivinen
        var core = SceneManager.GetSceneByName(coreName);
        if (!core.IsValid() || !core.isLoaded)
        {
            var loadCore = SceneManager.LoadSceneAsync(coreName, LoadSceneMode.Additive);
            while (!loadCore.isDone) yield return null;
            core = SceneManager.GetSceneByName(coreName);
        }
        SceneManager.SetActiveScene(core);

        // (siivoa roskat – vapauttaa tuhotun scenen assetteja)
        yield return Resources.UnloadUnusedAssets();
        yield return null;

        // 3) Lataa uusi taso additiivisesti
        var op2 = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!op2.isDone) yield return null;

        var map = SceneManager.GetSceneByName(levelName);
        if (!map.IsValid() || !map.isLoaded)
        {
            Debug.LogError($"[LevelLoader] Failed to load '{levelName}'. Is it in Build Settings?");
            yield break;
        }

        // 4) Aseta map aktiiviseksi yhdeksi frameksi (Start/Awake/OnEnable → placeholderit spawn)
        SceneManager.SetActiveScene(map);
        yield return null;                  // 1 frame
        yield return new WaitForEndOfFrame(); // varmistaa että scene-Startit ehtii

        // 5) Hookit (kuten OfflineBootissa)
        var edgeBaker = UnityEngine.Object.FindFirstObjectByType<EdgeBaker>();
        if (edgeBaker != null) edgeBaker.BakeAllEdges();

        // (valinn.) jos käytössä: miehitys uudelleen sceneen spawneista
        if (LevelGrid.Instance != null) LevelGrid.Instance.RebuildOccupancyFromScene();

        // 6) Core takaisin aktiiviseksi, ilmoita että valmis
        SceneManager.SetActiveScene(core);
        CurrentLevel = levelName;

        try { RaiseLevelReady(map); } catch { }



        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            MousePlaneMap.Instance.Rebuild();
        }
    }
}
