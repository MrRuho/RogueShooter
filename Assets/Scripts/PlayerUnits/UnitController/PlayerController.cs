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

    [Command]
    void CmdEndTurn()
    {
        if (hasEndedThisTurn) return;
        hasEndedThisTurn = true;
        Debug.Log("[PC][SERVER] CmdEndTurn received");
        // Ilmoita koordinaattorille (serverissä)
        CoopTurnCoordinator.Instance.ServerPlayerEndedTurn(netIdentity.netId);
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
        // Täällä voit disabloida ohjauksen/EndTurn-napin kun v==true
        // esim. UIEndTurnButton.interactable = canAct;
    }
}
