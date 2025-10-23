using Mirror;
using UnityEngine;

public class ResetService : NetworkBehaviour
{
    public static ResetService Instance;
    void Awake() => Instance = this;

    /// <summary>
    /// Kutsu tätä Play Again -napista. Hoitaa online/offline-haarat.
    /// </summary>
    public void RequestReset()
    {
        if (NetworkServer.active)                 // HOST / DEDISERVER
        {
            var win = FindFirstObjectByType<WinBattle>(FindObjectsInactive.Include);
            if (win != null) win.HideEndPanel();
            NetTurnManager.Instance.ResetTurnState();
            TurnSystem.Instance.ResetTurnId();
            // Kevyt client pre-cleanup (ei mitään gameplay/HUD-lukituksia!)
            RpcPreResetHud();
            // GameNetworkManager hoitaa levelin uudelleenlatauksen ja aloituksen
            NetLevelLoader.Instance.ServerReloadCurrentLevel();
            return;
        }

        if (NetworkClient.active)                 // PUHDAS CLIENT
        {
            // Ei tehdä mitään lokaalisti — serveri vaihtaa levelin ja käynnistää matsin
            return;
        }

        // OFFLINE
        LevelLoader.Instance.ReloadOffline(LevelLoader.Instance.DefaultLevel);
    }

    /// <summary>
    /// Kevyt ja 100% turvallinen UI-siistintä: piilota end-panelit ja join overlay.
    /// EI kosketa TurnSystemUI/UnitActionSystem/WorldUI/TurnGate!
    /// </summary>
    [ClientRpc]
    void RpcPreResetHud()
    {
        // Piilota end-paneeli (WinBattle on Core-scenessä)
        var win = FindFirstObjectByType<WinBattle>(FindObjectsInactive.Include);
        if (win != null) win.HideEndPanel();

    }
}
