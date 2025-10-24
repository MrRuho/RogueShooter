using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayFromCore
{
    private const string CoreScenePath = "Assets/Scenes/Core.unity";
    private const string AutoLoadKey   = "RS_AutoLoadActiveLevel";
    private const string ReqKey        = "RS_EditorRequestedLevel";

    static PlayFromCore()
    {
        // Reagoidaan Play-tilaan siirtymiseen
        EditorApplication.playModeStateChanged += OnPlayModeChange;
    }

    [InitializeOnLoadMethod]
    private static void AutoSetStartScene()
    {
        // Aseta Core start sceneksi jos ei ole vielä asetettu
        if (EditorSceneManager.playModeStartScene == null)
        {
            var core = AssetDatabase.LoadAssetAtPath<SceneAsset>(CoreScenePath);
            if (core != null) EditorSceneManager.playModeStartScene = core;
        }
    }

    [MenuItem("RogueShooter/Play From Core/Enable")]
    public static void Enable()
    {
        var core = AssetDatabase.LoadAssetAtPath<SceneAsset>(CoreScenePath);
        if (core == null)
        {
            Debug.LogError($"[PlayFromCore] Core ei löytynyt polusta: {CoreScenePath}");
            return;
        }
        EditorSceneManager.playModeStartScene = core;
        Debug.Log("[PlayFromCore] Play Mode Start Scene = Core");
    }

    [MenuItem("RogueShooter/Play From Core/Disable")]
    public static void Disable()
    {
        EditorSceneManager.playModeStartScene = null;
        Debug.Log("[PlayFromCore] Play Mode Start Scene poistettu.");
    }

    [MenuItem("RogueShooter/Play From Core/Enable", true)]
    private static bool ValidateEnable()
    {
        Menu.SetChecked("RogueShooter/Play From Core/Enable", EditorSceneManager.playModeStartScene != null);
        return true;
    }

    // --- Auto-load Active Level toggle ---
    [MenuItem("RogueShooter/Play From Core/Auto-load Active Level/Enable")]
    public static void AutoLoadEnable()
    {
        EditorPrefs.SetBool(AutoLoadKey, true);
        Debug.Log("[PlayFromCore] Auto-load Active Level = ON");
    }

    [MenuItem("RogueShooter/Play From Core/Auto-load Active Level/Disable")]
    public static void AutoLoadDisable()
    {
        EditorPrefs.SetBool(AutoLoadKey, false);
        Debug.Log("[PlayFromCore] Auto-load Active Level = OFF");
    }

    [MenuItem("RogueShooter/Play From Core/Auto-load Active Level/Enable", true)]
    private static bool ValidateAutoLoadMenu()
    {
        bool on = EditorPrefs.GetBool(AutoLoadKey, true);
        Menu.SetChecked("RogueShooter/Play From Core/Auto-load Active Level/Enable", on);
        return true;
    }

    // --- One-shot: käynnistä Play Coresta ja lataa juuri tämä kenttä ---
    [MenuItem("RogueShooter/Play From Core/Play Core \u2192 Load This Level Once")]
    public static void PlayCoreLoadThisLevelOnce()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!ValidatePlayableScene(scene, out var sceneName)) return;

        PlayerPrefs.SetString(ReqKey, sceneName);
        PlayerPrefs.Save();
        EditorApplication.isPlaying = true;
        Debug.Log($"[PlayFromCore] One-shot: pyydetty lataamaan '{sceneName}' kun Core käynnistyy.");
    }

    private static void OnPlayModeChange(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode) return;

        // Kirjaa automaattisesti editorissa auki ollut kenttä one-shot pyynnöksi
        bool auto = EditorPrefs.GetBool(AutoLoadKey, true);
        if (!auto) return;

        var scene = EditorSceneManager.GetActiveScene();
        if (!ValidatePlayableScene(scene, out var sceneName)) return;

        PlayerPrefs.SetString(ReqKey, sceneName);
        PlayerPrefs.Save();
        Debug.Log($"[PlayFromCore] Auto: pyydetty lataamaan '{sceneName}' kun Core käynnistyy.");
    }

    private static bool ValidatePlayableScene(Scene scene, out string sceneName)
    {
        sceneName = null;

        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogWarning("[PlayFromCore] Aktiivista sceneä ei voitu lukea.");
            return false;
        }

        if (scene.path == CoreScenePath)
        {
            // Core on jo lähtöscene — ei pyydetä erillistä leveliä
            return false;
        }

        if (!IsSceneInBuild(scene.path))
        {
            Debug.LogError($"[PlayFromCore] '{scene.path}' ei ole Build Settingsissä. Lisää se ensin.");
            return false;
        }

        sceneName = Path.GetFileNameWithoutExtension(scene.path);
        return true;
    }

    private static bool IsSceneInBuild(string scenePath)
    {
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled && s.path == scenePath) return true;
        return false;
    }
}
