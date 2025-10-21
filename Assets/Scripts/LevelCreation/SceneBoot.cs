using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[DisallowMultipleComponent]
public class SceneBoot : MonoBehaviour
{
    [SerializeField] private string[] levelScenes;      // jätä tyhjäksi ja syötä Inspectorissa
    [SerializeField] private int defaultLevelIndex = 0; // valitse Inspectorissa

    private void Awake()
    {
        if (!Application.isPlaying) return; // <-- ÄLÄ tee mitään editor-tilassa

        var core = SceneManager.GetSceneByName("Core");
        if (core.IsValid() && core.isLoaded)
            SceneManager.SetActiveScene(core);
    }

    private IEnumerator Start()
    {
        if (!Application.isPlaying) yield break; // <-- ÄLÄ tee mitään editor-tilassa

        string target = ResolveTargetLevel();
        if (string.IsNullOrEmpty(target))
        {
            Debug.LogError("[SceneBoot] Ei kelvollista kenttää.");
            yield break;
        }

        var s = SceneManager.GetSceneByName(target);
        if (!s.isLoaded)
        {
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogError($"[SceneBoot] '{target}' puuttuu Build Settingsistä.");
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
        }
    }

    private string ResolveTargetLevel()
    {
        if (levelScenes == null || levelScenes.Length == 0) return null;

        // Komentorivi-yliajo: -level=LevelName (ok myös editorissa, mutta ei tee mitään kun Start guardaa)
        foreach (var arg in System.Environment.GetCommandLineArgs())
            if (arg.StartsWith("-level="))
                return arg.Substring("-level=".Length);

        int i = Mathf.Clamp(defaultLevelIndex, 0, levelScenes.Length - 1);
        return levelScenes[i];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ei SceneManager/Resources tms. Vain kenttien rajaus.
        if (levelScenes != null && levelScenes.Length > 0)
            defaultLevelIndex = Mathf.Clamp(defaultLevelIndex, 0, levelScenes.Length - 1);
        else
            defaultLevelIndex = 0;
    }
#endif
}
