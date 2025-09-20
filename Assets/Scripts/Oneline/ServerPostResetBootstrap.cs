// ServerPostResetBootstrap.cs
using System.Collections;
using Mirror;
using UnityEngine;
using Utp;

public class ServerPostResetBootstrap : NetworkBehaviour
{
    private IEnumerator Start()
    {
        // Ajetaan vain serverillä ja vain resetin jälkeen
        if (!isServer) yield break;
        if (!ResetService.PendingHardReset) yield break;
        ResetService.PendingHardReset = false;

        // Odota 1 frame että kaikki singletons/OnEnable ehtivät alustua
        yield return null;
        yield return null; 

        // 1) Nollaa vuorologiikka ja pysäytä vanhat AI-koroutiinat varmuuden vuoksi
        EnemyAI.Instance?.StopAllCoroutines();
        NetTurnManager.Instance.ResetTurnState();

        // 2) Spawnaa viholliset serveriltä
        GameNetworkManager.Instance.SetEnemies();

        // 3) Rakenna occupancy
        LevelGrid.Instance?.RebuildOccupancyFromScene();

        // 4) Pakota UI Player-vuoroon ja synkkaa kaikille
        NetTurnManager.Instance.turnNumber = 1;
        NetTurnManager.Instance.phase = TurnPhase.Players;

        // SP/UI-puolelle
        TurnSystem.Instance?.ForcePhase(isPlayerTurn: true, incrementTurnNumber: false);

        // Co-op: tyhjennä ready/ended ja kerro vaiheesta
        var ended = System.Array.Empty<int>();
        CoopTurnCoordinator.Instance?.RpcUpdateReadyStatus(
            ended,
            CoopTurnCoordinator.Instance.BuildEndedLabels()
        );

        CoopTurnCoordinator.Instance?.RpcTurnPhaseChanged(
            NetTurnManager.Instance.phase,
            NetTurnManager.Instance.turnNumber,
            false // ei enemy
        );

        // Vihollisten world-UI piiloon 
        UnitUIBroadcaster.Instance?.BroadcastUnitWorldUIVisibility(false);
        // Kerro kaikille klienteille, että nyt saa toimia + päivitä HUD ***
        ResetService.Instance.RpcPostResetClientInit(NetTurnManager.Instance.turnNumber);
    }
}
