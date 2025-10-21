using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameModeSelectUI : MonoBehaviour
{
    // Serialized fields
    [Header("Canvas References")]
    [SerializeField] private GameObject gameModeSelectCanvas; // this (self)
    [SerializeField] private GameObject connectCanvas;        // Hiden on start
    [SerializeField] private GameObject connectCodePanel;        // Hiden on start

    [Header("Services")]
    [SerializeField] private Authentication authentication; // <-- UUSI

    [Header("Join Code UI")]
    [SerializeField] private TMP_Text joinCodeText; 

    // UI Elements
    [Header("Buttons")]
    [SerializeField] private Button coopButton;
    [SerializeField] private Button pvpButton;

    private void Awake()
    {
        // Ensure the game mode select canvas is active and connect canvas is inactive at start
        gameModeSelectCanvas.SetActive(true);
        connectCanvas.SetActive(false);
        connectCodePanel.SetActive(false);

        // Add button listeners
        coopButton.onClick.AddListener(OnClickCoOp);
        pvpButton.onClick.AddListener(OnClickPvP);
    }

    public void OnClickCoOp()
    {
        GameModeManager.SetCoOp();
        OnSelected();
    }

    public void OnClickPvP()
    {
        GameModeManager.SetVersus();
        OnSelected();
    }

    public async void OnSelected()
    {
        // 0) Varmista että Authentication löytyy (älä luota pelkkään connectCanvas-viitteeseen)
        if (!authentication)
            authentication = FindFirstObjectByType<Authentication>(FindObjectsInactive.Include);

        if (!authentication)
        {
            Debug.LogError("[GameModeSelectUI] Authentication-componenttia ei löytynyt scenestä.");
            return;
        }

        // 1) Sign-in Unity Servicesiin
        await authentication.SingInPlayerToUnityServerAsync();

        // 2) UI-flown jatko
        FieldCleaner.ClearAll();
        StartCoroutine(ResetGridNextFrame());
        if (gameModeSelectCanvas) gameModeSelectCanvas.SetActive(false);
        if (connectCanvas) connectCanvas.SetActive(true);

    }

    private System.Collections.IEnumerator ResetGridNextFrame()
    {
        yield return new WaitForEndOfFrame();
        var lg = LevelGrid.Instance;
        if (lg != null) lg.RebuildOccupancyFromScene();
    }


    public void Reset()
    {
        // Pieni “siivous” ennen reloadia on ok, mutta ei pakollinen
        FieldCleaner.ClearAll();
        /*
        if (Mirror.NetworkServer.active)
        {
            ResetService.Instance.HardResetServerAuthoritative();
        }
        else if (Mirror.NetworkClient.active)
        {
            ResetService.Instance.CmdRequestHardReset();
        }
        */
        if (Mirror.NetworkServer.active)
        {
           // ResetService.Instance.RequestReset();
        }
        else
        {
            // Yksinpeli
            GameReset.HardReloadSceneKeepMode();
        }
    }

    public void SetConnectCodePanelVisibility(bool active)
    {
        connectCodePanel.SetActive(active);
    }

    public void SetJoinCodeText(string s)
    {
        if (!joinCodeText)
        {
            Debug.LogWarning("[GameModeSelectUI] joinCodeText not assigned.");
            return;
        }

        s = (s ?? "").Trim().ToUpperInvariant();
        joinCodeText.text = $"JOIN CODE: {s}";

        // (valinnainen) kopioi koodi leikepöydälle:
        // GUIUtility.systemCopyBuffer = s;

        // (valinnainen) varmista että paneeli on näkyvissä:
        // if (connectCodePanel && !connectCodePanel.activeSelf) connectCodePanel.SetActive(true);
    }
}
