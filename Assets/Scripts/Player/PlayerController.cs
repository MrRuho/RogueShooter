using System;
using Mirror;
using UnityEngine;

///<sumary>
/// PLayerController handles per-player state in a networked game.
/// Each connected player has one PlayerController instance attached to PlayerController GameObject prefab
/// It tracks whether the player has ended their turn and communicates with the UI.
///</sumary>
public class PlayerController : NetworkBehaviour
{

    [SyncVar] public bool hasEndedThisTurn;

    public static PlayerController Local; // helppo viittaus UI:lle

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Local = this;
    }

    // UI-nappi kutsuu tätä (vain local player)
    public void ClickEndTurn()
    {
        if (!isLocalPlayer) return;
        if (hasEndedThisTurn) return;
        if (NetTurnManager.Instance && NetTurnManager.Instance.phase != TurnPhase.Players) return;
        CmdEndTurn();
    }

    [Command(requiresAuthority = true)]
    void CmdEndTurn()
    {
        if (hasEndedThisTurn) return;
        hasEndedThisTurn = true;

        // Estä kaikki toiminnot clientillä
        TargetNotifyCanAct(connectionToClient, false);

        // Varmista myös että koordinaattori löytyy serveripuolelta:
        if (NetTurnManager.Instance == null)
        {
            Debug.LogWarning("[PC][SERVER] NetTurnManager.Instance is NULL on server!");
            return;
        }

        NetTurnManager.Instance.ServerPlayerEndedTurn(netIdentity.netId);
    }



    // Server kutsuu tämän kierroksen alussa nollatakseen tilan
    [Server]
    public void ServerSetHasEnded(bool v)
    {
        hasEndedThisTurn = v;
        TargetNotifyCanAct(connectionToClient, !v);
    }

    [TargetRpc]
    void TargetNotifyCanAct(NetworkConnectionToClient __, bool canAct)
    {
        
        // Update End Turn Button
        var ui = FindFirstObjectByType<TurnSystemUI>();
        if (ui != null)
            ui.SetCanAct(canAct);
        if (!canAct) ui.SetTeammateReady(false, null);

        // Lock/Unlock UnitActionSystem input
        if (UnitActionSystem.Instance != null)
        {
            if (canAct) UnitActionSystem.Instance.UnlockInput();
            else UnitActionSystem.Instance.LockInput();
        }

        PlayerLocalTurnGate.Set(canAct);
    }

}
