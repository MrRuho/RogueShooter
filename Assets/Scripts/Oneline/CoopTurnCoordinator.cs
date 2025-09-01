using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public enum TurnPhase { Players, Enemy }

public class CoopTurnCoordinator : NetworkBehaviour
{
    public static CoopTurnCoordinator Instance { get; private set; }

    [SyncVar] public TurnPhase phase = TurnPhase.Players;
    [SyncVar] public int turnNumber = 1;
    [SyncVar] public int endedCount = 0;
    [SyncVar] public int requiredCount = 0; // päivitetään kun pelaajia liittyy/lähtee

    // Server only: ketkä ovat jo painaneet End Turn tässä kierrossa
    private readonly HashSet<uint> endedPlayers = new HashSet<uint>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResetTurnState();
        // jos haluat lukita kahteen pelaajaan protoa varten:
        if (GameModeManager.SelectedMode == GameMode.CoOp) requiredCount = 2;
        Debug.Log($"[TURN][SERVER] Start, requiredCount={requiredCount}");
        
    }

    [Server]
    public void ServerUpdateRequiredCount(int playersNow)
    {
        requiredCount = Mathf.Max(1, playersNow); // Co-opissa yleensä 2
        // jos yksi poistui kesken odotuksen, tarkista täyttyikö ehto nyt
        TryAdvanceIfReady();
    }

    [Server]
    public void ServerPlayerEndedTurn(uint playerNetId)
    {
        if (phase != TurnPhase.Players) return;          // ei lasketa jos ei pelaajavuoro
        if (!endedPlayers.Add(playerNetId)) return;      // älä laske tuplia

        endedCount = endedPlayers.Count;
        Debug.Log($"[TURN][SERVER] Player {playerNetId} ended. {endedCount}/{requiredCount}");
        RpcUpdateWaiting(endedCount, requiredCount);     // UI:lle "odotetaan X/Y"

        TryAdvanceIfReady();
    }

    [Server]
    void TryAdvanceIfReady()
    {
        if (phase == TurnPhase.Players && endedPlayers.Count >= Mathf.Max(1, requiredCount))
        {
            Debug.Log("[TURN][SERVER] All players ready → enemy turn");
            StartCoroutine(ServerEnemyTurnThenNextPlayers());
        }
    }

    [Server]
    private IEnumerator ServerEnemyTurnThenNextPlayers()
    {
        phase = TurnPhase.Enemy;
        RpcTurnPhaseChanged(phase, turnNumber);

        // Silta Unit-luokalle (AP-logiikka jne.)
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.ForcePhase(isPlayerTurn: false, incrementTurnNumber: false);

        // Aja SP-AI uudelleenkäyttönä
        yield return RunEnemyAI();

        // Takaisin pelaajille + uusi turn-numero
        turnNumber++;
        ResetTurnState();
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.ForcePhase(isPlayerTurn: true, incrementTurnNumber: false);

        RpcTurnPhaseChanged(phase, turnNumber);
    }

    [Server]
    IEnumerator RunEnemyAI()
    {
        if (EnemyAI.Instance != null)
            yield return EnemyAI.Instance.RunEnemyTurnCoroutine();
        else
            yield return new WaitForSeconds(2f); // fallback, ettei ketju katkea
    }

    [Server]
    void ResetTurnState()
    {
        Debug.Log("[TURN][SERVER] ResetTurnState");
        phase = TurnPhase.Players;
        endedPlayers.Clear();
        endedCount = 0;

        // nollaa kaikilta pelaajilta ‘hasEndedThisTurn’
        foreach (var kvp in NetworkServer.connections)
        {
            var id = kvp.Value.identity;
            if (!id) continue;
            var pc = id.GetComponent<PlayerController>();
            if (pc) pc.ServerSetHasEnded(false);  // <<< TÄRKEIN RIVI
        }

        RpcUpdateWaiting(endedCount, requiredCount);
    }

    // ---- Client-notifikaatiot UI:lle ----
    [ClientRpc]
    void RpcTurnPhaseChanged(TurnPhase newPhase, int turnNo)
    {
        // Päivitä UI (esim. "Enemy Turn", "Players Turn #X")
        // Voit myös täältä disabloida/enabloida ohjausta jos haluat
    }

    [ClientRpc]
    void RpcUpdateWaiting(int have, int need)
    {
        // UI: "Waiting for teammate: have/need"
    }
}