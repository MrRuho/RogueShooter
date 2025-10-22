using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public class SpawnUnitsCoordinator : MonoBehaviour
{
    public static SpawnUnitsCoordinator Instance { get; private set; }
    private bool enemiesSpawned;

    [Header("Co-op squad prefabs")]
    public GameObject unitHostPrefab;
    public GameObject unitClientPrefab;

    [Header("Enemy spawn (Co-op)")]
    public GameObject enemyPrefab;

    [Header("Spawn positions (world coords on your grid)")]
    public Vector3[] hostSpawnPositions = {
        new Vector3(0, 0, 0),
        new Vector3(2, 0, 0),
    };
    public Vector3[] clientSpawnPositions = {
        new Vector3(0, 0, 6),
        new Vector3(2, 0, 6),
    };
    public Vector3[] enemySpawnPositions = {
        new Vector3(4, 0, 8),
        new Vector3(6, 0, 8),
    };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            var myScene = gameObject.scene;
            var oldScene = Instance.gameObject.scene;

            // Jos edellinen on eri scenessä (jäänne purkamatta), tuhoa se ja ota tämä käyttöön
            if (oldScene != myScene)
            {
                Debug.LogWarning($"[SpawnUnitsCoordinator] Replacing leftover instance from scene '{oldScene.name}' with current '{myScene.name}'.");
                Destroy(Instance.gameObject);
                Instance = this;
                return;
            }

            // Sama scene → tämä on tupla oikeasti: tuhoa tämä
            Debug.LogError($"There's more than one SpawnUnitsCoordinator! {Instance} - {this}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public GameObject[] SpawnPlayersForNetwork(NetworkConnectionToClient conn, bool isHost)
    {
        GameObject unitPrefab = GetUnitPrefabForPlayer(isHost);
        Vector3[] spawnPoints = GetSpawnPositionsForPlayer(isHost);

        if (unitPrefab == null)
        {
            Debug.LogError($"[SpawnUnitsCoordinator] {(isHost ? "unitHostPrefab" : "unitClientPrefab")} puuttuu!");
            return null;
        }
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"[SpawnUnitsCoordinator] {(isHost ? "hostSpawnPositions" : "clientSpawnPositions")} ei ole asetettu!");
            return null;
        }

        var spawnedPlayersUnit = new GameObject[spawnPoints.Length];
        
        // Hae Level-scene (tämä SpawnUnitsCoordinator on Level-scenessä)
        Scene levelScene = gameObject.scene;
        Debug.Log($"[SpawnUnitsCoordinator] Spawning {spawnPoints.Length} units for {(isHost ? "HOST" : "CLIENT")} to scene '{levelScene.name}'");
        
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            // Käytä SpawnRouteria → varmistaa että unitit menevät Level-sceneen
            var playerUnit = SpawnRouter.SpawnNetworkServer(
                prefab: unitPrefab,
                pos: spawnPoints[i],
                rot: Quaternion.identity,
                source: transform,  // Käytä tämän objektin sceneä
                sceneName: levelScene.name,
                parent: null,
                owner: conn,
                beforeSpawn: (go) => 
                {
                    if (go.TryGetComponent<Unit>(out var u) && conn.identity != null)
                        u.OwnerId = conn.identity.netId;
                }
            );
            
            Debug.Log($"[SpawnUnitsCoordinator] Spawned unit '{playerUnit.name}' at {spawnPoints[i]} in scene '{playerUnit.scene.name}'");
            spawnedPlayersUnit[i] = playerUnit;
        }

        return spawnedPlayersUnit;
    }

    public GameObject GetUnitPrefabForPlayer(bool isHost)
    {
        if (unitHostPrefab == null || unitClientPrefab == null)
        {
            Debug.LogError("Unit prefab references not set in SpawnUnitsCoordinator!");
            return null;
        }

        return isHost ? unitHostPrefab : unitClientPrefab;
    }

    public Vector3[] GetSpawnPositionsForPlayer(bool isHost)
    {
        if (hostSpawnPositions.Length == 0 || clientSpawnPositions.Length == 0)
        {
            Debug.LogError("Spawn position arrays not set in SpawnUnitsCoordinator!");
            return new Vector3[0];
        }

        return isHost ? hostSpawnPositions : clientSpawnPositions;
    }

    public GameObject[] SpawnEnemies()
    {
        var spawnedEnemies = new GameObject[enemySpawnPositions.Length];
        Scene levelScene = gameObject.scene;
        
        Debug.Log($"[SpawnUnitsCoordinator] Spawning {enemySpawnPositions.Length} enemies to scene '{levelScene.name}'");

        for (int i = 0; i < enemySpawnPositions.Length; i++)
        {
            // Käytä SpawnRouteria verkkopelaamisessa
            if (NetworkServer.active)
            {
                var enemy = SpawnRouter.SpawnNetworkServer(
                    prefab: GetEnemyPrefab(),
                    pos: enemySpawnPositions[i],
                    rot: Quaternion.identity,
                    source: transform,
                    sceneName: levelScene.name,
                    parent: null,
                    owner: null
                );
                spawnedEnemies[i] = enemy;
                Debug.Log($"[SpawnUnitsCoordinator] Network spawned enemy '{enemy.name}' in scene '{enemy.scene.name}'");
            }
            else
            {
                // Offline-spawni
                var enemy = SpawnRouter.SpawnLocal(
                    prefab: GetEnemyPrefab(),
                    pos: enemySpawnPositions[i],
                    rot: Quaternion.identity,
                    source: transform,
                    sceneName: levelScene.name
                );
                spawnedEnemies[i] = enemy;
                Debug.Log($"[SpawnUnitsCoordinator] Local spawned enemy '{enemy.name}' in scene '{enemy.scene.name}'");
            }
        }

        SetEnemiesSpawned(true);
        return spawnedEnemies;
    }

    public Vector3[] GetEnemySpawnPositions()
    {
        if (enemySpawnPositions.Length == 0)
        {
            Debug.LogError("Enemy spawn position array not set in SpawnUnitsCoordinator!");
            return new Vector3[0];
        }

        return enemySpawnPositions;
    }

    public void SetEnemiesSpawned(bool value)
    {
        enemiesSpawned = value;
    }
    
    public bool AreEnemiesSpawned()
    {
        return enemiesSpawned;
    }

    public GameObject GetEnemyPrefab()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy prefab reference not set in SpawnUnitsCoordinator!");
            return null;
        }
        return enemyPrefab;
    }

    public void SpwanSinglePlayerUnits()
    {
        SpawnPlayer1UnitsOffline();
        SpawnEnemyUnitsOffline();
    }

    private void SpawnPlayer1UnitsOffline()
    {
        Scene targetScene = gameObject.scene;
        
        Debug.Log($"[SpawnUnitsCoordinator] Spawning offline player units to '{targetScene.name}'");
        
        for (int i = 0; i < Mathf.Min(6, hostSpawnPositions.Length); i++)
        {
            var unit = SpawnRouter.SpawnLocal(
                prefab: unitHostPrefab,
                pos: hostSpawnPositions[i],
                rot: Quaternion.identity,
                source: transform,
                sceneName: targetScene.name
            );
            Debug.Log($"[SpawnUnitsCoordinator] Offline player unit spawned in '{unit.scene.name}'");
        }
    }

    private void SpawnEnemyUnitsOffline()
    {
        Scene targetScene = gameObject.scene;

        Debug.Log($"[SpawnUnitsCoordinator] Spawning offline enemy units to '{targetScene.name}'");

        for (int i = 0; i < Mathf.Min(2, enemySpawnPositions.Length); i++)
        {
            var enemy = SpawnRouter.SpawnLocal(
                prefab: enemyPrefab,
                pos: enemySpawnPositions[i],
                rot: Quaternion.identity,
                source: transform,
                sceneName: targetScene.name
            );
            Debug.Log($"[SpawnUnitsCoordinator] Offline enemy spawned in '{enemy.scene.name}'");
        }
    }
}
