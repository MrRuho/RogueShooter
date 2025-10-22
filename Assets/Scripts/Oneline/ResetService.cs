using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            NetLevelLoader.Instance?.ServerReloadCurrentLevel();
            return;
        }

        if (NetworkClient.active)                 // PUHDAS CLIENT
        {
            CmdRequestResetFromServer();
            return;
        }

        // OFFLINE
        LevelLoader.Instance?.ReloadOffline(LevelLoader.Instance.DefaultLevel);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestResetFromServer()
    {
       // Server_PreResetClientCleanup();
        if (NetLevelLoader.Instance != null)
            NetLevelLoader.Instance.ServerReloadCurrentLevel();
    }

    [Server]
    void Server_PreResetClientCleanup()
    {
        // siivoa kaikilta klienteiltä paikalliset rojut (ragdoll/FX/debris yms.)
        RpcClientLocalCleanup();
    }

    [ClientRpc]
    void RpcClientLocalCleanup()
    {
        // siivoa Coresta & aktiivisesta levelistä yleisimmät “paikalliset” jäänteet
        void KillAllInScene(Scene scn)
        {
            if (!scn.IsValid() || !scn.isLoaded) return;
            foreach (var root in scn.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<UnitRagdoll>(true)) Destroy(r.gameObject);
                foreach (var b in root.GetComponentsInChildren<RagdollPoseBinder>(true)) Destroy(b.gameObject);
                // Lisää omat komponenttisi tähän jos käytät muita paikallisia jäänteitä:
                // foreach (var fx in root.GetComponentsInChildren<YourLocalFxMarker>(true)) Destroy(fx.gameObject);
            }
        }

        var core = SceneManager.GetSceneByName(LevelLoader.Instance ? LevelLoader.Instance.CoreSceneName : "Core");
        KillAllInScene(core);

        // jos nykyinen level on jo ladattu clientillä:
        for (int i = 0; i < SceneManager.sceneCount; i++)
            KillAllInScene(SceneManager.GetSceneAt(i));
    }
    
}
