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
            Debug.LogError("There's more than one SpawnUnitsCoordinator! " + transform + " - " + Instance);
            Destroy(gameObject); return;
        }
        Instance = this;
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
        
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var playerUnit = Instantiate(unitPrefab, spawnPoints[i], Quaternion.identity);
            
            if (playerUnit.TryGetComponent<Unit>(out var u) && conn.identity != null)
                u.OwnerId = conn.identity.netId;
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

        for (int i = 0; i < enemySpawnPositions.Length; i++)
        {
            var enemy = Instantiate(GetEnemyPrefab(), enemySpawnPositions[i], Quaternion.identity);
            spawnedEnemies[i] = enemy;
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
        
        var unit1 = Instantiate(unitHostPrefab, hostSpawnPositions[0], Quaternion.identity);
        SceneManager.MoveGameObjectToScene(unit1, targetScene);
        
        var unit2 = Instantiate(unitHostPrefab, hostSpawnPositions[1], Quaternion.identity);
        SceneManager.MoveGameObjectToScene(unit2, targetScene);
        
        var unit3 = Instantiate(unitHostPrefab, hostSpawnPositions[2], Quaternion.identity);
        SceneManager.MoveGameObjectToScene(unit3, targetScene);
        
        var unit4 = Instantiate(unitHostPrefab, hostSpawnPositions[3], Quaternion.identity);
        SceneManager.MoveGameObjectToScene(unit4, targetScene);
        
        var unit5 = Instantiate(unitHostPrefab, hostSpawnPositions[4], Quaternion.identity);
        SceneManager.MoveGameObjectToScene(unit5, targetScene);
        
        var unit6 = Instantiate(unitHostPrefab, hostSpawnPositions[5], Quaternion.identity);
        SceneManager.MoveGameObjectToScene(unit6, targetScene);
    }
    
    private void SpawnEnemyUnitsOffline()
    {
        Scene targetScene = gameObject.scene;
        
        var enemy1 = Instantiate(enemyPrefab, enemySpawnPositions[0], Quaternion.identity);
        SceneManager.MoveGameObjectToScene(enemy1, targetScene);
        
        var enemy2 = Instantiate(enemyPrefab, enemySpawnPositions[1], Quaternion.identity);
        SceneManager.MoveGameObjectToScene(enemy2, targetScene);
    }
}