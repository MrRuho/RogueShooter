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

            // UUSI: tyhjennä unit-listat ennen reloadia
            var um = FindFirstObjectByType<UnitManager>(FindObjectsInactive.Include);
            if (um) um.ClearAllUnitLists();

            NetTurnManager.Instance.ResetTurnState();
            TurnSystem.Instance.ResetTurnId();
            TurnSystem.Instance.ResetTurnNumber();

            RpcPreResetHud(); // siistii kaikkien HUDit
            NetLevelLoader.Instance.ServerReloadCurrentLevel();
            return;
        }

        if (NetworkClient.active)                 // PUHDAS CLIENT
        {
            // Serveri hoitaa reloadin ja aloituksen
            return;
        }

        // OFFLINE
        LevelLoader.Instance.ReloadOffline(LevelLoader.Instance.DefaultLevel);
    }

    /// <summary>
    /// Kevyt ja turvallinen UI-siistintä: piilota end-panelit.
    /// </summary>
    [ClientRpc]
    void RpcPreResetHud()
    {
        TurnSystem.Instance.ResetTurnId();
        var win = FindFirstObjectByType<WinBattle>(FindObjectsInactive.Include);
        if (win != null) win.HideEndPanel();

        // UUSI: myös asiakkaan UnitManager nollaan
        var um = FindFirstObjectByType<UnitManager>(FindObjectsInactive.Include);
        if (um) um.ClearAllUnitLists();
    }
}

