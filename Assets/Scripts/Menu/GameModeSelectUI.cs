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
       // coopButton.onClick.AddListener(() => OnModeSelected("Co-op"));
       // pvpButton.onClick.AddListener(() => OnModeSelected("PvP"));
        coopButton.onClick.AddListener(OnClickCoOp);
        pvpButton.onClick.AddListener(OnClickPvP);
    }

    private void OnModeSelected(string mode)
    {
        // Clear the field of existing units
        FieldCleaner.ClearAll();
        // UnitActionSystem.Instance?.SetSelectedUnit(null);
        StartCoroutine(ResetGridNextFrame());

        Debug.Log($"{mode} mode selected.");
        // Hide the game mode select canvas and show the connect canvas
        gameModeSelectCanvas.SetActive(false);
        connectCanvas.SetActive(true);
        // Additional logic for handling mode selection can be added here

        // Set the selected game mode in GameModeManager
        if (mode == "Co-op")
        {
            GameModeManager.SetCoOp();
        }
        else
        {
            GameModeManager.SetVersus();
        } 

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
    
}
