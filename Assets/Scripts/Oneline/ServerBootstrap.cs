using System.Collections;
using Mirror;
using UnityEngine;
using Utp;

/// <summary>
/// This ensures that the server starts correctly and in the correct order.
/// </summary>

[DefaultExecutionOrder(10000)]               // aja myöhään
[DisallowMultipleComponent]
public class ServerBootstrap : NetworkBehaviour
{
    public override void OnStartServer()
    {
        // varmistaa että tämä ei ajaudu clientillä
        StartCoroutine(Bootstrap());
    }

    private IEnumerator Bootstrap()
    {
        // 1) Odota että Mirror on spawnannut scene-identiteetit
        //    (2 frameä riittää, mutta odotetaan lisäksi koordinaattorit)
        yield return null;
        yield return null;

        // Odota kunnes koordinaattori(t) ovat varmasti olemassa ja spawned
        yield return new WaitUntil(() =>
            CoopTurnCoordinator.Instance &&
            CoopTurnCoordinator.Instance.netIdentity &&
            CoopTurnCoordinator.Instance.netIdentity.netId != 0
        );

        // 2) Nollaa vuorologiikka vain serverillä
        NetTurnManager.Instance.ResetTurnState();   // EI UI-RPC:itä täällä

        // 3) Spawnaa viholliset vain Co-opissa ja vain jos tarvitaan
        if (GameModeManager.SelectedMode == GameMode.CoOp &&
            !SpawnUnitsCoordinator.Instance.AreEnemiesSpawned())
        {
            GameNetworkManager.Instance.SetEnemies();
        }

        // 4) Rakenna occupancy nykyisestä scenestä (unitit/esteet)
        LevelGrid.Instance.RebuildOccupancyFromScene();
        // 4b) Varmista että edge/cover-data on synkassa occupancy/geometryn kanssa
        EdgeBaker.Instance.BakeAllEdges();

        // 5) Pakota aloitus Players turniin ja turnNumber = 1
        NetTurnManager.Instance.turnNumber = 1;
        NetTurnManager.Instance.phase = TurnPhase.Players;
        TurnSystem.Instance.ForcePhase(isPlayerTurn: true, incrementTurnNumber: false);

        // 6) Nyt on turvallista lähettää UI/RPC:t kaikille
        var endedIds = System.Array.Empty<int>();
        var endedLabels = CoopTurnCoordinator.Instance.BuildEndedLabels();

        CoopTurnCoordinator.Instance.RpcUpdateReadyStatus(endedIds, endedLabels);
        CoopTurnCoordinator.Instance.RpcTurnPhaseChanged(
            NetTurnManager.Instance.phase,
            NetTurnManager.Instance.turnNumber,
            true // isPlayersPhase
        );

        // (valinnainen) piilota enemy-WorldUI tms. alussa
        UnitUIBroadcaster.Instance.BroadcastUnitWorldUIVisibility(false);

        // (valinnainen) client-init, jos sinulla on tällainen
        ResetService.Instance.RpcPostResetClientInit(NetTurnManager.Instance.turnNumber);

        NetTurnManager.Instance.SetPlayerStartState();
    }
}
