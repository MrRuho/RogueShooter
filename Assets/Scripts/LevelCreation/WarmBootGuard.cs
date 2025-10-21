using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;


// Tämä on vain fallback – jos LevelLoader toimii, tämä tekee ei mitään.
public static class WarmBootGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad()
    {
        // Koskee vain offline-starttia. Host/client-tilan latauksen hoitaa NetLevelLoader.
        if (NetworkServer.active || NetworkClient.active) return;

        // jos LevelLoaderia ei jostain syystä löydy tai se ei toimi,
        // lataa varmuuden vuoksi Level 0 additiivisesti.
        var loader = Object.FindFirstObjectByType<LevelLoader>(FindObjectsInactive.Include);
        if (loader == null)
        {
            Debug.LogWarning("[WarmBootGuard] LevelLoader not found → loading 'Level 0' additively as a fallback.");
            SceneManager.LoadSceneAsync("Level 0", LoadSceneMode.Additive);
        }
    }
}
