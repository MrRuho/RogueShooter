using UnityEngine;
using UnityEngine.UI;

public class GameModeSelectUI : MonoBehaviour
{
    // Serialized fields
    [Header("Canvas References")]
    [SerializeField] private GameObject gameModeSelectCanvas; // this (self)
    [SerializeField] private GameObject connectCanvas;        // Hiden on start

    // UI Elements
    [Header("Buttons")]
    [SerializeField] private Button coopButton;
    [SerializeField] private Button pvpButton;

    private void Awake()
    {
        // Ensure the game mode select canvas is active and connect canvas is inactive at start
        gameModeSelectCanvas.SetActive(true);
        connectCanvas.SetActive(false);

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

    public void OnSelected()
    {
        FieldCleaner.ClearAll();
        StartCoroutine(ResetGridNextFrame());
        gameModeSelectCanvas.SetActive(false);
        connectCanvas.SetActive(true);
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

        if (Mirror.NetworkServer.active)
        {
            ResetService.Instance.HardResetServerAuthoritative();
        }
        else if (Mirror.NetworkClient.active)
        {
            ResetService.Instance.CmdRequestHardReset();
        }
        else
        {
            // Yksinpeli
            GameReset.HardReloadSceneKeepMode();
        }
    }
}
