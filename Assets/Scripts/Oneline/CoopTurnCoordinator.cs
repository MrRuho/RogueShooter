using System.Collections;
using System.Linq;
using Mirror;
using UnityEngine;

public class CoopTurnCoordinator : NetworkBehaviour
{
    public static CoopTurnCoordinator Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }


    [Server]
    public void TryAdvanceIfReady()
    {
        if (NetTurnManager.Instance.phase == TurnPhase.Players && NetTurnManager.Instance.endedPlayers.Count >= Mathf.Max(1, NetTurnManager.Instance.requiredCount))
        {
            StartCoroutine(ServerEnemyTurnThenNextPlayers());
        }
    }

    [Server]
    private IEnumerator ServerEnemyTurnThenNextPlayers()
    {
        // Asettaa vihollisen WordUI: (Action Points) näkyviin.
        UnitUIBroadcaster.Instance.BroadcastUnitWorldUIVisibility(true);

        // 1) Vihollisvuoro alkaa
        RpcTurnPhaseChanged(NetTurnManager.Instance.phase = TurnPhase.Enemy, NetTurnManager.Instance.turnNumber, false);

        // Silta unit/AP-logiikalle (sama kuin nyt)
        if (TurnSystem.Instance != null)
        {
            TurnSystem.Instance.ForcePhase(isPlayerTurn: false, incrementTurnNumber: false);
        }

        // Aja AI
        yield return RunEnemyAI();

        // 2) Paluu pelaajille + turn-numero + resetit
        NetTurnManager.Instance.turnNumber++;
        NetTurnManager.Instance.ResetTurnState();

        if (TurnSystem.Instance != null)
        {
            TurnSystem.Instance.ForcePhase(isPlayerTurn: true, incrementTurnNumber: false);
        }

        // 3) Lähetä *kaikille* (host + clientit) HUD-päivitys SP-logiikan kautta
        RpcTurnPhaseChanged(NetTurnManager.Instance.phase = TurnPhase.Players, NetTurnManager.Instance.turnNumber, true);
        
        // Asettaa pelaajien WordUI: (Action Points) näkyviin.
        UnitUIBroadcaster.Instance.BroadcastUnitWorldUIVisibility(false);
    }

    [Server]
    IEnumerator RunEnemyAI()
    {
        if (EnemyAI.Instance != null)
            yield return EnemyAI.Instance.RunEnemyTurnCoroutine();
        else
            yield return null; // fallback, ettei ketju katkea
    }

    // ---- Client-notifikaatiot UI:lle ----
    [ClientRpc]
    public void RpcTurnPhaseChanged(TurnPhase newPhase, int newTurnNumber, bool isPlayersPhase)
    {
        // Päivitä paikallinen SP-UI-luuppi (ei Mirror-kutsuja)
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.SetHudFromNetwork(newTurnNumber, isPlayersPhase);

        // Vaihe vaihtui → varmuuden vuoksi piilota mahdollinen "READY" -teksti
        var ui = FindFirstObjectByType<TurnSystemUI>();
        if (ui != null) ui.SetTeammateReady(false, null);
    }


    // Näyttää toiselle pelaajalle "Player X READY"
    [ClientRpc]
    public void RpcUpdateReadyStatus(int[] whoEndedIds, string[] whoEndedLabels)
    {
        var ui = FindFirstObjectByType<TurnSystemUI>();
        if (ui == null) return;

        // Selvitä oma netId
        uint localId = 0;
        if (NetworkClient.connection != null && NetworkClient.connection.identity)
            localId = NetworkClient.connection.identity.netId;

        bool show = false;
        string label = null;

        // Jos joku muu kuin minä on valmis → näytä hänen labelinsa
        for (int i = 0; i < whoEndedIds.Length; i++)
        {
            if ((uint)whoEndedIds[i] != localId)
            {
                show = true;
                label = (i < whoEndedLabels.Length) ? whoEndedLabels[i] : "Teammate";
                break;
            }
        }

        ui.SetTeammateReady(show, label);
    }

    // ---- Server-apurit ----
    [Server] string GetLabelByNetId(uint id)
    {
        foreach (var kvp in NetworkServer.connections)
        {
            var conn = kvp.Value;
            if (conn != null && conn.identity && conn.identity.netId == id)
                return conn.connectionId == 0 ? "Player 1" : "Player 2";
        }
        return "Teammate";
    }

    [Server]
    public string[] BuildEndedLabels()
    {
        // HashSetin järjestys ei ole merkityksellinen, näytetään mikä tahansa toinen
        return NetTurnManager.Instance.endedPlayers.Select(id => GetLabelByNetId(id)).ToArray();
    }
}
