/*
using System.Collections;
using Mirror;
using UnityEngine;

public class MapContentSpawner : NetworkBehaviour
{
    private bool _clientBakedThisScene;
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(SpawnThenBake());
    }

    private IEnumerator SpawnThenBake()
    {
        // 1) Spawnaa kaikki NetworkIdentity-suojat serverillä
        var spawnPoints = FindObjectsByType<ObjectSpawnPlaceHolder>(FindObjectsSortMode.None);
        foreach (var sp in spawnPoints)
            sp.CreteObject(); // NetworkServer.Spawn(...)

        // 2) Odota, että riippuvuudet ovat olemassa
        yield return new WaitUntil(() =>
            EdgeBaker.Instance != null &&
            LevelGrid.Instance  != null &&
            PathFinding.Instance != null
        );

        // 3) Odota 1 frame, että uusien objektien Start() ehtii
        yield return null;

        // 4) Server-bake
        EdgeBaker.Instance.BakeAllEdges();

        // 5) Pyydä clienttejä bakeamaan guardattuna
        RpcBakeAllEdgesOnClientsGuarded();
    }

    [ClientRpc]
    private void RpcBakeAllEdgesOnClientsGuarded()
    {
        StartCoroutine(ClientBakeGuarded());
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // varmistus myöhässä liittyville
        StartCoroutine(ClientBakeGuarded());
    }

    private IEnumerator ClientBakeGuarded()
    {
        if (_clientBakedThisScene) yield break;
        // Odota että kaikki on varmasti paikalla myös klientillä
        yield return new WaitUntil(() =>
            EdgeBaker.Instance != null &&
            LevelGrid.Instance != null &&
            PathFinding.Instance != null
        );
        
        yield return null; // Startit ehtii

        EdgeBaker.Instance.BakeAllEdges();
        _clientBakedThisScene = true;
    }
}
*/
/*
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapContentSpawner : NetworkBehaviour
{
    
    private bool _clientBakedThisScene;
    
    public override void OnStartServer()
    {
        if(!GameModeManager.Instance.LevelIsLoaded()) return;
        base.OnStartServer();
        StartCoroutine(SpawnThenBake());
    }

    private IEnumerator SpawnThenBake()
    {
        Scene mapScene = gameObject.scene;
        Debug.Log($"[MapContentSpawner] Spawning objects in scene: {mapScene.name}");

        var spawnPoints = FindObjectsByType<ObjectSpawnPlaceHolder>(FindObjectsSortMode.None);
        
        int spawnedCount = 0;
        foreach (var sp in spawnPoints)
        {
            if (sp.gameObject.scene == mapScene)
            {
                var spawnedObject = sp.CreteObject();
                if (spawnedObject != null)
                {
                    spawnedCount++;
                }
            }
        }
        
        Debug.Log($"[MapContentSpawner] Spawned {spawnedCount} objects in {mapScene.name}");

        yield return new WaitUntil(() =>
            EdgeBaker.Instance != null &&
            LevelGrid.Instance  != null &&
            PathFinding.Instance != null
        );

        yield return null;

        EdgeBaker.Instance.BakeAllEdges();
        RpcBakeAllEdgesOnClientsGuarded();
    }

    [ClientRpc]
    private void RpcBakeAllEdgesOnClientsGuarded()
    {
        StartCoroutine(ClientBakeGuarded());
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        StartCoroutine(ClientBakeGuarded());
    }

    private IEnumerator ClientBakeGuarded()
    {
        if (_clientBakedThisScene) yield break;
        
        yield return new WaitUntil(() =>
            EdgeBaker.Instance != null &&
            LevelGrid.Instance != null &&
            PathFinding.Instance != null
        );
        
        yield return null;

        EdgeBaker.Instance.BakeAllEdges();
        _clientBakedThisScene = true;
    }
}
*/
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapContentSpawner : NetworkBehaviour
{
    private bool _clientBakedThisScene;
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        if (GameModeManager.Instance != null && !GameModeManager.Instance.LevelIsLoaded())
        {
            Debug.LogWarning("[MapContentSpawner] Level not loaded yet, waiting...");
            StartCoroutine(WaitForLevelThenSpawn());
            return;
        }
        
        StartCoroutine(SpawnThenBake());
    }

    private IEnumerator WaitForLevelThenSpawn()
    {
        yield return new WaitUntil(() => GameModeManager.Instance.LevelIsLoaded());
        yield return SpawnThenBake();
    }

    private IEnumerator SpawnThenBake()
    {
        Scene mapScene = gameObject.scene;
        Debug.Log($"[MapContentSpawner] Spawning objects in scene: {mapScene.name}");

        var spawnPoints = FindObjectsByType<ObjectSpawnPlaceHolder>(FindObjectsSortMode.None);
        
        int spawnedCount = 0;
        foreach (var sp in spawnPoints)
        {
            if (sp.gameObject.scene == mapScene)
            {
                var spawnedObject = sp.CreteObject();
                if (spawnedObject != null)
                {
                    spawnedCount++;
                }
            }
        }
        
        Debug.Log($"[MapContentSpawner] Spawned {spawnedCount} objects in {mapScene.name}");

        yield return new WaitUntil(() =>
            EdgeBaker.Instance != null &&
            LevelGrid.Instance  != null &&
            PathFinding.Instance != null
        );

        yield return null;

        EdgeBaker.Instance.BakeAllEdges();
        RpcBakeAllEdgesOnClientsGuarded();
    }

    [ClientRpc]
    private void RpcBakeAllEdgesOnClientsGuarded()
    {
        StartCoroutine(ClientBakeGuarded());
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        StartCoroutine(ClientBakeGuarded());
    }

    private IEnumerator ClientBakeGuarded()
    {
        if (_clientBakedThisScene) yield break;
        
        yield return new WaitUntil(() =>
            EdgeBaker.Instance != null &&
            LevelGrid.Instance != null &&
            PathFinding.Instance != null
        );
        
        yield return null;

        EdgeBaker.Instance.BakeAllEdges();
        _clientBakedThisScene = true;
    }
}

