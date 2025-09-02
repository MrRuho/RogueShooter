using System;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnSystemUI : MonoBehaviour
{
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TextMeshProUGUI turnNumberText;            // (valinnainen, käytä SP:ssä)
    [SerializeField] private GameObject enemyTurnVisualGameObject;      // (valinnainen, käytä SP:ssä)

    bool isCoop;
    private PlayerController localPlayerController;

    void Start()
    {
        isCoop = GameModeManager.SelectedMode == GameMode.CoOp;

        // kiinnitä handler tasan kerran
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        }

        if (isCoop)
        {
            // Co-opissa nappi on DISABLED kunnes serveri kertoo että saa toimia
            TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
            SetCanAct(false);
        }
        else
        {
            // Singleplayerissa kuuntele vuoron vaihtumista
            if (TurnSystem.Instance != null)
            {
                TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
                UpdateForSingleplayer();
            }
        }
    }

    void OnDisable()
    {
        if (!isCoop && TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
    }

    // ====== julkinen kutsu PlayerController.TargetNotifyCanAct:ista ======
    public void SetCanAct(bool canAct)
    {
        if (endTurnButton == null) return;

        endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        if (canAct) endTurnButton.onClick.AddListener(OnEndTurnClicked);

        endTurnButton.gameObject.SetActive(canAct);   // jos haluat pitää aina näkyvissä, vaihda SetActive(true)
        endTurnButton.interactable = canAct;
    }

    // ====== nappi ======
    private void OnEndTurnClicked()
    {
        // Päättele co-op -tila tilannekohtaisesti (ei SelectedMode)
        bool isCoopNow =
            CoopTurnCoordinator.Instance != null &&
            (NetworkServer.active || NetworkClient.isConnected);

        if (!isCoopNow)
        {
            Debug.Log("[UI] EndTurn clicked (SP)");
            if (TurnSystem.Instance != null)
            {
                TurnSystem.Instance.NextTurn();
            }
            else
            {
                Debug.LogWarning("[UI] TurnSystem.Instance is null");
            }
            return;
        }

        Debug.Log("[UI] EndTurn clicked (Co-op)");

        CacheLocalPlayerController();
        if (localPlayerController == null)
        {
            Debug.LogWarning("[UI] Local PlayerController not found");
            return;
        }
        // Istantly lock input
        if (UnitActionSystem.Instance != null)
        {
            UnitActionSystem.Instance.LockInput();
        }
        // Prevent double clicks
        SetCanAct(false);
        // Lähetä serverille
        localPlayerController.ClickEndTurn();
    }

    private void CacheLocalPlayerController()
    {
        if (localPlayerController == null)
        {
            var conn = NetworkClient.connection;
            if (conn != null && conn.identity != null)
            {
                localPlayerController = conn.identity.GetComponent<PlayerController>();
            }
        }
    }

    // ====== singleplayer UI (valinnainen) ======
    private void TurnSystem_OnTurnChanged(object s, EventArgs e) => UpdateForSingleplayer();

    private void UpdateForSingleplayer()
    {
        if (turnNumberText != null)
            turnNumberText.text = "Turn: " + TurnSystem.Instance.GetTurnNumber();

        if (enemyTurnVisualGameObject != null)
            enemyTurnVisualGameObject.SetActive(!TurnSystem.Instance.IsPlayerTurn());

        if (endTurnButton != null)
            endTurnButton.gameObject.SetActive(TurnSystem.Instance.IsPlayerTurn());
    }
}
