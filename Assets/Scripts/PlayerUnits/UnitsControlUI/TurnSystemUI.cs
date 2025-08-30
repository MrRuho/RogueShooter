using System;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnSystemUI : MonoBehaviour
{
    [SerializeField] private Button endTurnButton;
    [SerializeField] private TextMeshProUGUI turnNumberText;
    [SerializeField] private GameObject enemyTurnVisualGameObject;

    private bool isCoop;

    void Start()
    {
        isCoop = GameModeManager.SelectedMode == GameMode.CoOp;

        // aina yksi reitti nappiin
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        }

        if (isCoop)
        {
            // päivitä heti ja sitten säännöllisesti
            UpdateForCoop();
            InvokeRepeating(nameof(UpdateForCoop), 0.1f, 0.2f);
        }
        else
        {
            // singleplayer: käytä vanhaa eventtiä
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

        if (IsInvoking(nameof(UpdateForCoop)))
            CancelInvoke(nameof(UpdateForCoop));
    }

    // ---------- Nappilogiikka: yksi reitti kaikille moodeille ----------
    private void OnEndTurnClicked()
    {
        if (GameModeManager.SelectedMode == GameMode.CoOp)
        {
            Debug.Log("[UI] EndTurn clicked (Co-op)");

            var conn = Mirror.NetworkClient.connection;
            if (conn == null)
            {
                Debug.LogWarning("[UI] NetworkClient.connection is null");
                return;
            }

            var id = conn.identity;
            if (id == null)
            {
                Debug.LogWarning("[UI] Local NetworkIdentity not ready yet");
                return;
            }

            PlayerController me;
            if (!id.TryGetComponent<PlayerController>(out me))
            {
                Debug.LogWarning("[UI] PlayerController missing on local player");
                return;
            }

            me.ClickEndTurn();   // -> CmdEndTurn -> server koordinoi
            UpdateForCoop();     // päivitä nappi/teksti heti
        }
        else
        {
            Debug.Log("[UI] EndTurn clicked (SP)");
            if (TurnSystem.Instance != null)
                TurnSystem.Instance.NextTurn();
        }
    }

    // ---------- SINGLEPLAYER UI ----------
    private void TurnSystem_OnTurnChanged(object sender, EventArgs e) => UpdateForSingleplayer();

    private void UpdateForSingleplayer()
    {
        if (turnNumberText != null)
            turnNumberText.text = "Turn: " + TurnSystem.Instance.GetTurnNumber();

        if (enemyTurnVisualGameObject != null)
            enemyTurnVisualGameObject.SetActive(!TurnSystem.Instance.IsPlayerTurn());

        if (endTurnButton != null)
            endTurnButton.gameObject.SetActive(TurnSystem.Instance.IsPlayerTurn());
    }

    // ---------- CO-OP UI ----------
    private void UpdateForCoop()
    {
        var coord = CoopTurnCoordinator.Instance;

        // Turn-numero + (X/Y) odotus
        if (turnNumberText != null)
        {
            if (coord != null)
            {
                string extra = (coord.phase == TurnPhase.Players &&
                                coord.endedCount < Math.Max(1, coord.requiredCount))
                             ? $"  ({coord.endedCount}/{coord.requiredCount})"
                             : "";
                turnNumberText.text = $"Turn: {Mathf.Max(1, coord.turnNumber)}{extra}";
            }
            else
            {
                turnNumberText.text = "Turn: -";
            }
        }

        // Enemy overlay
        if (enemyTurnVisualGameObject != null)
            enemyTurnVisualGameObject.SetActive(coord != null && coord.phase == TurnPhase.Enemy);

        // EndTurn-napin näkyvyys / interaktio
        if (endTurnButton != null)
        {
            bool canShow = coord != null && coord.phase == TurnPhase.Players;

            bool canPress = false;
            var conn = NetworkClient.connection;
            if (conn != null && conn.identity != null)
            {
                var me = conn.identity.GetComponent<PlayerController>();
                if (me != null)
                    canPress = canShow && !me.hasEndedThisTurn;
            }

            endTurnButton.gameObject.SetActive(canShow);
            endTurnButton.interactable = canPress;
        }
    }
}
