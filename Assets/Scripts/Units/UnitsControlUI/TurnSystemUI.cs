using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Utp;

///<sumary>
/// TurnSystemUI manages the turn system user interface.
/// It handles both singleplayer and multiplayer modes.
/// In multiplayer, it interacts with PlayerController to manage turn ending.
/// It also updates UI elements based on the current turn state.
///</sumary>
public class TurnSystemUI : MonoBehaviour
{
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TextMeshProUGUI turnNumberText;            // (valinnainen, käytä SP:ssä)
    [SerializeField] private GameObject enemyTurnVisualGameObject;      // (valinnainen, käytä SP:ssä)
    [SerializeField] private TextMeshProUGUI playerReadyText;          // (Online)

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

        if (playerReadyText) playerReadyText.gameObject.SetActive(false);
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
        bool isOnline =
            NetTurnManager.Instance != null &&
            (GameNetworkManager.Instance.GetNetWorkServerActive() || GameNetworkManager.Instance.GetNetWorkClientConnected());
        if (!isOnline)
        {
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

        //Päivitä player ready hud
    }

    private void CacheLocalPlayerController()
    {
        if (localPlayerController != null) return;

        // 1) Varmista helpoimman kautta
        if (PlayerController.Local != null)
        {
            localPlayerController = PlayerController.Local;
            return;
        }

        // 2) Fallback: Mirrorin client-yhteyden identity
        var conn = GameNetworkManager.Instance != null
            ? GameNetworkManager.Instance.NetWorkClientConnection()
            : null;
        if (conn != null && conn.identity != null)
        {
            localPlayerController = conn.identity.GetComponent<PlayerController>();
            if (localPlayerController != null) return;
        }

        // 3) Viimeinen oljenkorsi: etsi skenestä local-pelaaja
        var pcs = FindObjectsByType<PlayerController>(FindObjectsSortMode.InstanceID);
        foreach (var pc in pcs)
        {
            if (pc.isLocalPlayer) { localPlayerController = pc; break; }
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

    // Kutsutaan verkosta
    public void SetTeammateReady(bool visible, string whoLabel = null)
    {
        if (!playerReadyText) return;
        if (visible)
        {
            playerReadyText.text = $"{whoLabel} READY";
            playerReadyText.gameObject.SetActive(true);
        }
        else
        {
            playerReadyText.gameObject.SetActive(false);
        }
    }
}
