using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
// using Mirror;
public static class OfflineSceneBoot
{   

    // Nimeä nämä Build Settings -listan mukaan 1:1
    const string CORE = "Core";
    const string LEVEL0 = "Level 0";   // vaihda halutuksi “oletuskentäksi”

    // Ajetaan aina, heti kun ensimmäinen scene on ladattu (buildissä Core)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureOfflineLevel()
    {
        // if (NetworkServer.active || NetworkClient.active) return;
        if (NetMode.IsOnline) return;
        // Jos jokin muu kuin Core on jo auki (esim. online host muuttaa scenen),
        // älä tee mitään.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.name != CORE)
                return;
        }

        // Offline-käynnistys: ei tukeuduta Mirrorin tilaan ollenkaan
        var _ = CoroutineRunner.Run(Co_Boot());
    }

    static IEnumerator Co_Boot()
    {
        // 1) Varmista että Core on ladattu ja aktiivinen
        var core = SceneManager.GetSceneByName(CORE);
        if (!core.IsValid() || !core.isLoaded)
        {
            var loadCore = SceneManager.LoadSceneAsync(CORE, LoadSceneMode.Additive);
            while (!loadCore.isDone) yield return null;
            core = SceneManager.GetSceneByName(CORE);
        }
        SceneManager.SetActiveScene(core);

        // 2) Lataa oletuskenttä additiivisesti
        if (!Application.CanStreamedLevelBeLoaded(LEVEL0))
        {
            Debug.LogError($"[OfflineBoot] Scene '{LEVEL0}' ei ole Build Settings -listassa.");
            yield break;
        }

        var op = SceneManager.LoadSceneAsync(LEVEL0, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        var map = SceneManager.GetSceneByName(LEVEL0);

        // 3) Aktivoi map yhdeksi frameksi, jotta sen Start/OnEnable/OnAwake ehtivät
        SceneManager.SetActiveScene(map);
        yield return null;

        // 4) (valinn.) tee kenttäkohtaiset hookit, esim. edge bake
        var edgeBaker = Object.FindFirstObjectByType<EdgeBaker>();
        edgeBaker?.BakeAllEdges();

        // 5) Palauta Core aktiiviseksi (UI jne.)
        SceneManager.SetActiveScene(core);

        // 6) Jos käytät LevelLoaderin LevelReady-eventtiä, nosta se (turvallisesti)
        try
        {
            // projektissasi LevelLoaderilla on public static RaiseLevelReady(Scene)
            LevelLoader.RaiseLevelReady(map);
        }
        catch { }

        Debug.Log($"[OfflineBoot] Core+'{LEVEL0}' valmiina.");
    }


    // Minimaalinen “global coroutine host” ilman mitään GameObjectia
    private sealed class CoroutineRunner : MonoBehaviour
    {
        static CoroutineRunner _inst;
        public static Coroutine Run(IEnumerator e)
        {
            if (_inst == null)
            {
                var go = new GameObject("~OfflineBoot");
                Object.DontDestroyOnLoad(go);
                _inst = go.AddComponent<CoroutineRunner>();
            }
            return _inst.StartCoroutine(e);
        }
    } 
}
