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
        EvaluateWin();
    }

    private void EvaluateWin()
    {

        if (gameEnded) return;
        var um = UnitManager.Instance;
        if (um == null) return;

        int friendCount = um.GetFriendlyUnitList().Count;
        int enemyCount = um.GetEnemyUnitList().Count;

        if (GameModeManager.SelectedMode == GameMode.Versus)
        {
            bool hostWins = enemyCount <= 0;
            bool hostLoses = friendCount <= 0;

            if (hostWins || hostLoses)
            {
                bool isLocalHost = IsLocalHost();
                bool localWins = (hostWins && isLocalHost) || (hostLoses && !isLocalHost);
                ShowEnd(localWins ? "You win!" : "You lost");
            }
        }
        else // SinglePlayer
        {
            if (enemyCount <= 0) ShowEnd("Players Win!");
            else if (friendCount <= 0) ShowEnd("Enemies Win!");
        }
    }
    
    // Host-koneella (server+client samassa) tämä palauttaa true. Etäklientillä false.
    private bool IsLocalHost()
    {
        // Varmistetaan, että ollaan host-clientissä: sekä server että client aktiiviset,
        // ja “paikallinen serveriyhteys” on sama kuin clientin oma yhteys.
        return NetworkServer.active && NetworkClient.active;
    }

    public void ShowEnd(string title)
    {
        gameEnded = true;

        if (titleText) titleText.text = title;
        if (panel) panel.SetActive(true);

    }

    private void OnClickPlayAgain()
    {
        gameEnded = false;
        if (panel) panel.SetActive(false);
        // Yksi reitti kaikkeen: ResetService → LevelLoader
         ResetService.Instance.RequestReset();
    }
}
