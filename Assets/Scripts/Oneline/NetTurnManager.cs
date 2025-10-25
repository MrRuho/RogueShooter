using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
///<sumary>
/// NetTurnManager coordinates turn phases in a networked multiplayer game.
/// It tracks which players have ended their turns and advances the game phase accordingly. 
///</sumary>
public enum TurnPhase { Players, Enemy }
public class NetTurnManager : NetworkBehaviour
{
    public static NetTurnManager Instance { get; private set; }
    [SyncVar] public TurnPhase phase = TurnPhase.Players;
    [SyncVar] public int turnNumber = 1;

    // Seurannat (server)
    [SyncVar] public int endedCount = 0;
    [SyncVar] public int requiredCount = 0; // päivitetään kun pelaajia liittyy/lähtee

    public readonly HashSet<uint> endedPlayers = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // jos haluat lukita kahteen pelaajaan protoa varten:
        if (GameModeManager.SelectedMode == GameMode.CoOp) requiredCount = 2;
        StartCoroutine(DeferResetOneFrame());
    }

    [Server]
    private IEnumerator DeferResetOneFrame()
    {
        yield return null;                 // odota että SpawnObjects on valmis
        ResetTurnState();                  // nyt RpcUpdateReadyStatus on turvallinen
    }

    [Server]
    public void ResetTurnState()
    {

        phase = TurnPhase.Players;
        endedPlayers.Clear();
        endedCount = 0;
        SetPlayerStartState();
    }

    [Server]
    public void ServerPlayerEndedTurn(uint playerNetId)
    {
        // PvP: siirrä vuoro heti vastustajalle
        if (GameModeManager.SelectedMode == GameMode.Versus)
        {
            if (PvPTurnCoordinator.Instance)
                PvPTurnCoordinator.Instance.ServerHandlePlayerEndedTurn(playerNetId);
            return;
        }

        if (phase != TurnPhase.Players) return;          // ei lasketa jos ei pelaajavuoro
        if (!endedPlayers.Add(playerNetId)) return;      // älä laske tuplia

        endedCount = endedPlayers.Count;

        // Ilmoita kaikille, KUKA on valmis → UI näyttää "Player X READY" toisella pelaajalla. Käytössä vain Co-opissa
        if (GameModeManager.SelectedMode == GameMode.CoOp)
        {
            // Asettaa yksikoiden UI Näkyvyydet
            UnitUIBroadcaster.Instance.BroadcastUnitWorldUIVisibility(false);

            CoopTurnCoordinator.Instance.
            RpcUpdateReadyStatus(
            endedPlayers.Select(id => (int)id).ToArray(),
            CoopTurnCoordinator.Instance.BuildEndedLabels()
            );

            CoopTurnCoordinator.Instance.TryAdvanceIfReady();
        }
    }

    [Server]
    public void ServerUpdateRequiredCount(int playersNow)
    {
        requiredCount = Mathf.Max(1, playersNow); // Co-opissa yleensä 2
                                                  // jos yksi poistui kesken odotuksen, tarkista täyttyikö ehto nyt

        if (GameModeManager.SelectedMode == GameMode.CoOp)
        {
            CoopTurnCoordinator.Instance.TryAdvanceIfReady();
        }
    }

    public void SetPlayerStartState()
    {
        // Asettaa pelaajan tilan pelaajan vuoroksi.
        foreach (var kvp in NetworkServer.connections)
        {
            var id = kvp.Value.identity;
            if (!id) continue;
            var pc = id.GetComponent<PlayerController>();
            if (pc) pc.ServerSetHasEnded(false);  // <<< TÄRKEIN RIVI
        }
    }
    
    /// <summary>
/// Serverillä: nollaa vuorot ja aloittaa Players-vaiheen. Kutsutaan aina kun leveli latautuu (myös reloadissa).
/// </summary>
/// <param name="resetTurnNumber">Jos true, turnNumber asetetaan 1:een. Jos false, säilytetään nykyinen (tai voit itse inkrementoida muualla).</param>
    [Server]
    public void ServerResetAndBegin(bool resetTurnNumber = true)
    {
        // Co-op: laske tällä hetkellä aktiiviset pelaajat ja päivitä requiredCount
        if (GameModeManager.SelectedMode == GameMode.CoOp)
        {
            int playersNow = 0;
            foreach (var kv in NetworkServer.connections)
                if (kv.Value != null && kv.Value.identity != null) playersNow++;

            ServerUpdateRequiredCount(playersNow);
        }

        if (resetTurnNumber)
            turnNumber = 1;

        if (GameModeManager.SelectedMode == GameMode.Versus && PvPTurnCoordinator.Instance)
        {
            PvPTurnCoordinator.Instance.ServerGiveFirstTurnToHost();
        }

        ResetTurnState();     
    }
}
