using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Nollaa HUD/valinnat/world-UI turvallisesti heti uuden scenen latauduttua.
/// Laita tämä Coreen (DontDestroyOnLoad tai pysyvä GameObject).
/// </summary>
public class UiResetOnLevelReady : MonoBehaviour
{
    /*
    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("[UIResetOnLevelReady] Starting UI reset");
        // Odota 1 frame, että uudet instanssit ehtivät Start():iin
        StartCoroutine(Co_ResetHudNextFrame());
    }

    private IEnumerator Co_ResetHudNextFrame()
    {
        yield return null;
        Debug.Log("[UIResetOnLevelReady] Resetting HUD and UI elements");
        // 1) HUD (nappi pois / READY pois)
        var hud = FindFirstObjectByType<TurnSystemUI>(FindObjectsInactive.Include);
        if (hud != null)
        {
            hud.SetCanAct(false);
            hud.SetTeammateReady(false, null);
        }

        // 2) Valinnat & input-lukko
        if (UnitActionSystem.Instance != null)
        {
            UnitActionSystem.Instance.ResetSelectedAction();
            UnitActionSystem.Instance.ResetSelectedUnit();
            UnitActionSystem.Instance.LockInput();
        }

        // 3) World-space UI piiloon varalta – serveri jakaa oikeat näkyvyydet heti aloituksessa
        var worldUIs = FindObjectsByType<UnitWorldUI>(FindObjectsSortMode.None);
        foreach (var ui in worldUIs)
            ui.SetVisible(false);

    }
    */
}