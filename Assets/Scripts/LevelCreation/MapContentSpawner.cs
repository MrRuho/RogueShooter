using System.Collections;
using Mirror;
using UnityEngine;

public class MapContentSpawner : NetworkBehaviour
{
    // (Valinnainen) ettei bakea ajeta klientillä monta kertaa
    private static bool s_clientBakedOnce;

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(SpawnThenBake());
    }

    private IEnumerator SpawnThenBake()
    {
        Debug.Log("[MapContentSpawner] Spawning map content on server...");

        // 1) Spawnaa kaikki NetworkIdentity-suojat serverillä
        var spawnPoints = FindObjectsByType<ObjectSpawnPlaceHolder>(FindObjectsSortMode.None);
        foreach (var sp in spawnPoints)
            sp.CreteObject(); // tämä kutsuu NetworkServer.Spawn(...)

        // 2) Server-bake (jos serveri käyttää edge-dataa esim. AI:hin)
        EdgeBaker.Instance.BakeAllEdges();

        // 3) Odota 1 frame → varmistaa että spawn-viestit ehtivät klienteille
        yield return null;

        // 4) Käske kaikkia klientejä bake’amaan omassa päässään
        RpcBakeAllEdgesOnClients();
    }

    [ClientRpc]
    private void RpcBakeAllEdgesOnClients()
    {
        if (s_clientBakedOnce) return; // valinnainen vartija
        EdgeBaker.Instance.BakeAllEdges();
        s_clientBakedOnce = true;
        // Jos hover-UI tarvitsee refreshin, kutsu se tässä:
        // GridSystemVisual.Instance?.RefreshAll?.Invoke();
        Debug.Log("[MapContentSpawner] Client received RPC: BakeAllEdges()");
    }

    // BONUS: myöhäisille liittyjille (late join) – kun tämä scene-objekti spawnaa klientille
    public override void OnStartClient()
    {
        base.OnStartClient();
        StartCoroutine(BakeNextFrameOnClient());
    }

    private IEnumerator BakeNextFrameOnClient()
    {
        yield return null; // odota että kaikki scene-spawnit on valmiit klientilläkin
        if (!s_clientBakedOnce)
        {
            EdgeBaker.Instance.BakeAllEdges();
            s_clientBakedOnce = true;
            Debug.Log("[MapContentSpawner] OnStartClient: BakeAllEdges()");
        }
    }
}
