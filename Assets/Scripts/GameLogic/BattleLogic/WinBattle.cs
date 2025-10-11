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
        Debug.Log("Evaluate win. Player units:" + friendCount + "Enemy units:" + enemyCount);

        // Jos kumpikaan ei ole vielä spawnannut, älä tee mitään
        /*
        if (friendCount <= 0 && enemyCount <= 0)
        {
            return;
        }
        */
        
        if (enemyCount <= 0)
        {
            ShowEnd("Players Win!");
        }
        else if (friendCount <= 0)
        {
            ShowEnd("Enemies Win!");
        }
    }

    private void ShowEnd(string title)
    {
        gameEnded = true;

        if (titleText) titleText.text = title;
        if (panel) panel.SetActive(true);

        // (valinnainen) lukitse input
        // UnitActionSystem.Instance?.LockInput();
    }

    private void OnClickPlayAgain()
    {
        // Sama malli kuin GameModeSelectUI.Reset
        if (NetworkServer.active)
        {
            ResetService.Instance.HardResetServerAuthoritative();
        }
        else if (NetworkClient.active)
        {
            ResetService.Instance.CmdRequestHardReset();
        }
        else
        {
            GameReset.HardReloadSceneKeepMode();
        }
    }
}
