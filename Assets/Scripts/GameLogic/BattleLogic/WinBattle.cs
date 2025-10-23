using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public class WinBattle : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;           // koko voitto-UI:n root (piilossa aluksi)
    [SerializeField] private TextMeshProUGUI titleText;  // "Players Win!" / "Enemies Win!"
    [SerializeField] private Button playAgainButton;     // käynnistää resetin

    private bool gameEnded;

    private void Awake()
    {
        if (panel) panel.SetActive(false);
    }

    private void OnEnable()
    {
        Unit.OnAnyUnitDead    += Unit_OnAnyUnitDead;

    }

    private void OnDisable()
    {
        Unit.OnAnyUnitDead    -= Unit_OnAnyUnitDead;
    }

    private void Start()
    {
        if (panel) panel.SetActive(false);
        if (playAgainButton)
        {
            playAgainButton.onClick.RemoveAllListeners();
            playAgainButton.onClick.AddListener(OnClickPlayAgain);
        }

        // Jos aloitetaan tilasta, jossa toista puolta ei ole
       // EvaluateWin();
    }

    private void Unit_OnAnyUnitDead(object sender, System.EventArgs e)
    {
        if (GameModeManager.SelectedMode == GameMode.Versus)
        {
            if (NetMode.IsOnline) EvaluateWin_Server(); // vain server päättää
            return;
        }

        // Offline/SP
        EvaluateWin_Local();
    }

    // ---- UUSI: vain server ----
    [Server]
    private void EvaluateWin_Server()
    {
        if (gameEnded) return;
        var um = UnitManager.Instance; if (um == null) return;

        int friendCount = um.GetFriendlyUnitList().Count;
        int enemyCount  = um.GetEnemyUnitList().Count;

        bool hostWins  = enemyCount  <= 0;
        bool hostLoses = friendCount <= 0;
        if (!(hostWins || hostLoses)) return;

        gameEnded = true; // gate, kunnes ResetService nollaa

        // Lähetä tulos jokaiselle pelaajalle henkilökohtaisesti
        foreach (var kvp in NetworkServer.connections)
        {
            var conn = kvp.Value;
            if (conn?.identity == null) continue;

            var pc = conn.identity.GetComponent<PlayerController>();
            if (!pc) continue;

            bool isHost = conn.connectionId == 0; // hostin connectionId on 0
            bool youWon = (hostWins && isHost) || (hostLoses && !isHost);
            pc.TargetShowEnd(conn, youWon); // näyttää WinBattle-paneelin clientillä
        }
    }

    // ---- Vanhasta EvaluateWinistä jää SinglePlayer-haara tähän ----
    private void EvaluateWin_Local()
    {
        if (gameEnded) return;
        var um = UnitManager.Instance; if (um == null) return;

        int friendCount = um.GetFriendlyUnitList().Count;
        int enemyCount  = um.GetEnemyUnitList().Count;

        if (enemyCount <= 0) ShowEnd("Players Win!");
        else if (friendCount <= 0) ShowEnd("Enemies Win!");
    }
    
    public void ShowEnd(string title)
    {
        gameEnded = true;

        if (titleText) titleText.text = title;
        if (panel) panel.SetActive(true);

    }

    private void OnClickPlayAgain()
    {
        // Yksi reitti kaikkeen: ResetService → LevelLoader
        if (NetMode.IsOnline)
        {
            ResetService.Instance.RequestReset();
            return;
        }
        gameEnded = false;
        if (panel) panel.SetActive(false);
        // OFFLINE → suoraan LevelLoaderin kautta
        LevelLoader.Instance.ReloadOffline(LevelLoader.Instance.DefaultLevel);
    }

    public void HideEndPanel()
    {
        gameEnded = false;
        if (panel) panel.SetActive(false);
    }
}
