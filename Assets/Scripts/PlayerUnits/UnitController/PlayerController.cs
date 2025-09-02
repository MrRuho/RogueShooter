using System;
using Mirror;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SyncVar] public bool hasEndedThisTurn;

    // UI-nappi kutsuu tätä (vain local player)
    public void ClickEndTurn()
    {
        if (!isLocalPlayer) return;
        if (hasEndedThisTurn) return;             // ei tuplia
        if (CoopTurnCoordinator.Instance && CoopTurnCoordinator.Instance.phase != TurnPhase.Players) return;
        Debug.Log("[PC] ClickEndTurn → CmdEndTurn()");
        CmdEndTurn();
    }

    [Command(requiresAuthority = true)]
    void CmdEndTurn()
    {
        Debug.Log($"[PC][SERVER] CmdEndTurn called by player {netId}");
        if (hasEndedThisTurn) return;
        hasEndedThisTurn = true;
        Debug.Log("[PC][SERVER] CmdEndTurn received");

        // Estä kaikki toiminnot clientillä
        TargetNotifyCanAct(connectionToClient, false);

        // Varmista myös että koordinaattori löytyy serveripuolelta:
        if (CoopTurnCoordinator.Instance == null)
        {
            Debug.LogWarning("[PC][SERVER] CoopTurnCoordinator.Instance is NULL on server!");
            return;
        }
        CoopTurnCoordinator.Instance.ServerPlayerEndedTurn(netIdentity.netId);
    }

    

    // Server kutsuu tämän kierroksen alussa nollatakseen tilan
    [Server]
    public void ServerSetHasEnded(bool v)
    {
        hasEndedThisTurn = v;
        Debug.Log($"[PC][SERVER] ServerSetHasEnded({v}) for player {netId}");
        TargetNotifyCanAct(connectionToClient, !v);
    }

    [TargetRpc]
    void TargetNotifyCanAct(NetworkConnectionToClient __, bool canAct)
    {
        Debug.Log($"[PC][CLIENT] TargetNotifyCanAct({canAct})");
        // Update End Turn Button
        var ui = FindFirstObjectByType<TurnSystemUI>();
        if (ui != null)
            ui.SetCanAct(canAct);

        // Lock/Unlock UnitActionSystem input
        if (UnitActionSystem.Instance != null)
        {
            if (canAct) UnitActionSystem.Instance.UnlockInput();
            else        UnitActionSystem.Instance.LockInput();
        }
    }
}
