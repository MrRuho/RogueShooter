using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OfflineSceneBoot
{
    // Ajetaan aina, heti kun ensimmäinen scene on ladattu (buildissä Core)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureOfflineLevel()
    {
        if (NetMode.IsOnline) return;

        // Jos jokin muu kuin Core on jo auki (esim. online host vaihtoi scenen), älä tee mitään
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.name != (LevelLoader.Instance?.CoreSceneName ?? "Core"))
                return;
        }

        // Offline-käynnistys: ei tukeuduta Mirrorin tilaan ollenkaan
        var _ = CoroutineRunner.Run(Co_Boot());
    }

    static IEnumerator Co_Boot()
    {
        // 1) Core ladattuna ja aktiiviseksi
        string coreName = LevelLoader.Instance?.CoreSceneName ?? "Core";

        var core = SceneManager.GetSceneByName(coreName);
        if (!core.IsValid() || !core.isLoaded)
        {
            var loadCore = SceneManager.LoadSceneAsync(coreName, LoadSceneMode.Additive);
            while (loadCore != null && !loadCore.isDone) yield return null;
            core = SceneManager.GetSceneByName(coreName);
        }
        if (core.IsValid()) SceneManager.SetActiveScene(core);

        // 2) Päätä ladattava kenttä LevelLoaderista
        string requested =
            LevelLoader.Instance?.CurrentLevel ??
            LevelLoader.Instance?.DefaultLevel;

        // Fallback: vain jos “Level 0” on oikeasti Build Settingsissä
        if (string.IsNullOrEmpty(requested) || !Application.CanStreamedLevelBeLoaded(requested))
        {
            if (!string.IsNullOrEmpty(requested))
                Debug.LogWarning($"[OfflineBoot] '{requested}' ei ole Build Settingsissä. Yritetään fallbackia.");

            if (Application.CanStreamedLevelBeLoaded("Level 0"))
                requested = "Level 0";
            else
            {
                Debug.LogError("[OfflineBoot] Ei löydy ladattavaa kenttää: Current/Default puuttuu ja 'Level 0' ei ole Build Settingsissä.");
                yield break;
            }
        }

        var op = SceneManager.LoadSceneAsync(requested, LoadSceneMode.Additive);
        while (op != null && !op.isDone) yield return null;

        var map = SceneManager.GetSceneByName(requested);
        if (!map.IsValid() || !map.isLoaded)
        {
            Debug.LogError($"[OfflineBoot] Scene '{requested}' ei latautunut.");
            yield break;
        }

        // 4) Aktivoi map yhdeksi frameksi, jotta sen Start/OnEnable ehtivät
        SceneManager.SetActiveScene(map);
        yield return null;                  // 1 frame
        yield return new WaitForEndOfFrame();

        // 5) Kenttäkohtaiset hookit (esim. edge bake, miehitys)
        var edgeBaker = Object.FindFirstObjectByType<EdgeBaker>();
        edgeBaker?.BakeAllEdges();

        if (LevelGrid.Instance != null)
            LevelGrid.Instance.RebuildOccupancyFromScene();

        MousePlaneMap.Instance.Rebuild();
        // 6) Palauta Core aktiiviseksi (UI yms.) ja ilmoita, että level on valmis
        if (core.IsValid()) SceneManager.SetActiveScene(core);

        try { LevelLoader.RaiseLevelReady(map); } catch { /* ei kriittinen */ }

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
