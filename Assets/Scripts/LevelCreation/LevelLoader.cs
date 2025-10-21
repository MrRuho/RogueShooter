/*
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;
using Mirror;

public class LevelLoader : MonoBehaviour
{
    [Tooltip("Kenttäscenet Build Settingsistä täsmälleen samoilla nimillä")]
    [SerializeField] private string[] levelScenes = { "Testing 1" };

    [Tooltip("Ladataanko tämä käynnistyksessä (jätä tyhjäksi jos et halua auto-starttia)")]
    [SerializeField] private string bootLevel = "Testing 1";

    [Tooltip("Palautetaanko Core aktiiviseksi yhden framen jälkeen (UI tms.)")]
    [SerializeField] private bool setCoreActiveAfterSpawnStart = false;

    private bool _busy;
    private string _currentLevel;

    private void Start()
    {
        // Käynnistys (valinnainen)
        if (!string.IsNullOrEmpty(bootLevel))
            StartCoroutine(LoadLevel(bootLevel));
    }

    public void LoadByName(string levelName)
    {
        if (_busy) return;
        StartCoroutine(LoadLevel(levelName));
    }

    public void LoadByIndex(int idx)
    {
        if (_busy) return;
        idx = Mathf.Clamp(idx, 0, levelScenes.Length - 1);
        StartCoroutine(LoadLevel(levelScenes[idx]));
    }

    public void LoadNext()
    {
        if (_busy || levelScenes.Length == 0) return;
        int cur = Mathf.Max(0, System.Array.IndexOf(levelScenes, _currentLevel));
        LoadByIndex((cur + 1) % levelScenes.Length);
    }

    public void Reload()
    {
        if (_busy || string.IsNullOrEmpty(_currentLevel)) return;
        StartCoroutine(LoadLevel(_currentLevel));
    }
    /*
    private IEnumerator LoadLevel(string levelName)
    {
        if (string.IsNullOrEmpty(levelName) || !Application.CanStreamedLevelBeLoaded(levelName))
        {
            Debug.LogError($"[LevelLoader] Scene '{levelName}' ei löydy Build Settingsistä.");
            yield break;
        }

        _busy = true;

        // 1) Unload edellinen level (jos on)
        if (!string.IsNullOrEmpty(_currentLevel))
        {
            var cur = SceneManager.GetSceneByName(_currentLevel);
            if (cur.IsValid() && cur.isLoaded)
                yield return SceneManager.UnloadSceneAsync(cur);
        }

        // 2) Lataa uusi level additivena
        var load = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!load.isDone) yield return null;

        // 3) TEE LEVEL AKTIIVISEKSI ENNEN SEURAAVAA FRAMEA
        var levelScene = SceneManager.GetSceneByName(levelName);
        SceneManager.SetActiveScene(levelScene);

        // 4) Anna spawnerien (Start/OnStartServer) ajaa tässä framessa → instanssit menevät Level-scenelle
        yield return null;

        // (Valinnainen) 5) Palauta Core aktiiviseksi, jos UI/entrypoint niin vaatii
        if (setCoreActiveAfterSpawnStart)
        {
            var core = SceneManager.GetSceneByName("Core");
            if (core.IsValid() && core.isLoaded)
                SceneManager.SetActiveScene(core);
        }

        _currentLevel = levelName;

        // (Valinnainen) 6) Hookkeja: esim. leivonta spawnausten jälkeen
        EdgeBaker.Instance?.BakeAllEdges();

        _busy = false;
    }
    

    public IEnumerator LoadLevel(string levelName)
    {
        if (!string.IsNullOrEmpty(_currentLevel))
        {
            if (NetworkServer.active)
            {
                DestroyAllSpawnedNetworkObjects();
            }
            
            var cur = SceneManager.GetSceneByName(_currentLevel);
            if (cur.IsValid() && cur.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_currentLevel);
        }

        var core = SceneManager.GetSceneByName("Core");
        if (core.IsValid() && core.isLoaded)
            SceneManager.SetActiveScene(core);

        var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        _currentLevel = levelName;

        EdgeBaker.Instance.BakeAllEdges();
    }

    private void DestroyAllSpawnedNetworkObjects()
    {
        var allNetObjects = FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None);
        foreach (var netObj in allNetObjects)
        {
            if (netObj.sceneId == 0)
            {
                NetworkServer.Destroy(netObj.gameObject);
            }
        }
    }

}
*/
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Mirror;

public class LevelLoader : MonoBehaviour
{
    [SerializeField] string defaultLevel = "Testing 3";
    static string _currentLevel;

    void Start()
    {
        StartCoroutine(LoadLevel(defaultLevel));
    }

    public void ReloadCurrentLevel()
    {
        if (!string.IsNullOrEmpty(_currentLevel))
            StartCoroutine(LoadLevel(_currentLevel));
    }

    public IEnumerator LoadLevel(string levelName)
    {
        if (!string.IsNullOrEmpty(_currentLevel))
        {
            if (NetworkServer.active)
            {
                DestroyAllSpawnedNetworkObjects();
            }

            var cur = SceneManager.GetSceneByName(_currentLevel);
            if (cur.IsValid() && cur.isLoaded)
            {
                var unloadOp = SceneManager.UnloadSceneAsync(_currentLevel);
                while (!unloadOp.isDone) 
                    yield return null;
            }
        }

        var core = SceneManager.GetSceneByName("Core");
        if (core.IsValid() && core.isLoaded)
            SceneManager.SetActiveScene(core);

        yield return Resources.UnloadUnusedAssets();

        var op = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();
        
        op.allowSceneActivation = true;

        while (!op.isDone)
        {
            yield return null;
        }

        _currentLevel = levelName;

        var loadedScene = SceneManager.GetSceneByName(levelName);
        if (loadedScene.IsValid())
        {
            Debug.Log($"[LevelLoader] Successfully loaded scene: {levelName}");
        }

        yield return null;

        EdgeBaker.Instance?.BakeAllEdges();
    }

    private void DestroyAllSpawnedNetworkObjects()
    {
        var allNetObjects = FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None);
        foreach (var netObj in allNetObjects)
        {
            if (netObj.sceneId == 0)
            {
                NetworkServer.Destroy(netObj.gameObject);
            }
        }
    }
}

