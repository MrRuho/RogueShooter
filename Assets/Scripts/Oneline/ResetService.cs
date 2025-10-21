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
        // Host/serveri: resetoi suoraan
        if (NetworkServer.active)
        {
            if (NetLevelLoader.Instance != null)
                NetLevelLoader.Instance.ServerReloadCurrentLevel();
            return;
        }

        // Puhtaasti client: pyydä serveriä resetoimaan
        if (NetworkClient.active)
        {
            CmdRequestResetFromServer();
            return;
        }

        // Offline: lataa kenttä uudelleen paikallisesti
        if (LevelLoader.Instance != null)
            LevelLoader.Instance.StartLocalReload();
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestResetFromServer()
    {
        if (NetLevelLoader.Instance != null)
            NetLevelLoader.Instance.ServerReloadCurrentLevel();
    }
    
}