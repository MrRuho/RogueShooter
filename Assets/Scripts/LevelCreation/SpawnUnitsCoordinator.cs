
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using System.Collections.Generic;
using System.Linq;

public class SpawnUnitsCoordinator : MonoBehaviour
{
    public static SpawnUnitsCoordinator Instance { get; private set; }

    // ...luokan sisälle:
    [Header("Use placeholders instead of arrays")]
    public bool usePlaceholders = true;

    [Tooltip("Jos true, koordinaattori disabloi/tuhouttaa käytetyt placeholderit serverillä heti spawnin jälkeen.")]
    public bool consumePlaceholdersOnServer = true;

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
                    if (go.TryGetComponent<Unit>(out var u))
                    {
                        if (conn.identity != null) u.OwnerId = conn.identity.netId;

                        // 1) Vision-komponentti (mieluiten valmiiksi prefabissa):
                        if (go.TryGetComponent<UnitVision>(out var uv))
                        {
                            InitUnitVision(go, teamId: (GameModeManager.SelectedMode == GameMode.Versus)
                            ? (NetworkSync.IsOwnerHost(u.OwnerId) ? 0 : 1)
                            : 0);
                        }
                    }
                }
            );
            
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

    // Get Spawn positions from placeholders in the scene
    private Vector3[] GetSpawnPositionsFromPlaceholders(UnitSpawnPlaceholder.Side side)
    {
        var scene = gameObject.scene;
        var all = FindObjectsByType<UnitSpawnPlaceholder>(FindObjectsSortMode.None);
        var mine = new List<UnitSpawnPlaceholder>(all.Length);

        foreach (var ph in all)
        {
            if (!ph) continue;
            if (ph.gameObject.scene != scene) continue; // vain tämän Level-skenen placeholderit
            if (ph.side != side) continue;
            mine.Add(ph);
        }

        if (mine.Count == 0) return System.Array.Empty<Vector3>();

        // deterministinen järjestys order-kentän mukaan, sitten nimi/instanceID fallback
        var ordered = mine.OrderBy(p => p.order).ThenBy(p => p.name).ToList();

        var result = new Vector3[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
            result[i] = ordered[i].GetSpawnWorldPosition();

        // serverillä siivotaan placeholderit jos niin halutaan
        if (consumePlaceholdersOnServer && Mirror.NetworkServer.active)
            foreach (var ph in ordered) ph.Consume();

        return result;
    }

    public Vector3[] GetEnemySpawnPositions()
    {

        if (usePlaceholders)
        {
            var pos = GetSpawnPositionsFromPlaceholders(UnitSpawnPlaceholder.Side.Enemy);
            if (pos.Length > 0) return pos;
            Debug.LogWarning("[SpawnUnitsCoordinator] No enemy placeholders found, falling back to arrays.");
        }

        if (enemySpawnPositions.Length == 0)
        {
            Debug.LogError("Enemy spawn position array not set in SpawnUnitsCoordinator!");
            return new Vector3[0];
        }
        return enemySpawnPositions;
    }

    public Vector3[] GetSpawnPositionsForPlayer(bool isHost)
    {
        if (usePlaceholders)
        {
            var side = isHost ? UnitSpawnPlaceholder.Side.Host : UnitSpawnPlaceholder.Side.Client;
            var pos = GetSpawnPositionsFromPlaceholders(side);
            if (pos.Length > 0) return pos;
            Debug.LogWarning("[SpawnUnitsCoordinator] No placeholders found, falling back to arrays.");
        }

        if (hostSpawnPositions.Length == 0 || clientSpawnPositions.Length == 0)
        {
            Debug.LogError("Spawn position arrays not set in SpawnUnitsCoordinator!");
            return new Vector3[0];
        }
        return isHost ? hostSpawnPositions : clientSpawnPositions;
    }

    public void SpawnSinglePlayerUnits()
    {
        Scene targetScene = gameObject.scene;

        // PLAYER (Host) – hae paikat placeholdereista (tai fallback taulukkoon)
        var playerSpawns = GetSpawnPositionsForPlayer(true);

        for (int i = 0; i < playerSpawns.Length; i++)
        {
            var unit = SpawnRouter.SpawnLocal(
                prefab: unitHostPrefab,
                pos: playerSpawns[i],
                rot: Quaternion.identity,
                source: transform,
                sceneName: targetScene.name
            );

            InitUnitVision(unit, teamId: 0);
        }

        // ENEMY – samoin placeholdereista (tai fallback)
        var enemySpawns = GetEnemySpawnPositions();

        for (int i = 0; i < enemySpawns.Length; i++)
        {
            var enemy = SpawnRouter.SpawnLocal(
                prefab: GetEnemyPrefab(),
                pos: enemySpawns[i],
                rot: Quaternion.identity,
                source: transform,
                sceneName: targetScene.name
            );

            InitUnitVision(enemy, teamId: 1);
        }

        SetEnemiesSpawned(true);
    }

    public GameObject[] SpawnEnemies()
    {
        // 1) Hae paikat (placeholderit jos käytössä, muuten fallback-taulukko)
        var enemySpawns = GetEnemySpawnPositions();
        Scene targetScene = gameObject.scene;

        var spawnedEnemies = new GameObject[enemySpawns.Length];

        for (int i = 0; i < enemySpawns.Length; i++)
        {
            if (NetworkServer.active)
            {
                var go = SpawnRouter.SpawnNetworkServer(
                    prefab: GetEnemyPrefab(),
                    pos: enemySpawns[i],
                    rot: Quaternion.identity,
                    source: transform,
                    sceneName: targetScene.name,
                    parent: null,
                    owner: null
                );
                spawnedEnemies[i] = go;

                InitUnitVision(go, teamId: 1);
            }
            else
            {
                var go = SpawnRouter.SpawnLocal(
                    prefab: GetEnemyPrefab(),
                    pos: enemySpawns[i],
                    rot: Quaternion.identity,
                    source: transform,
                    sceneName: targetScene.name
                );
                spawnedEnemies[i] = go;

                InitUnitVision(go, teamId: 1);

            }
        }

        SetEnemiesSpawned(true);
        return spawnedEnemies;
    }
    
    /*
    private void InitUnitVision(GameObject go, int teamId)
    {
        if (NetworkServer.active && !NetworkClient.active) return;

        if (go.TryGetComponent<Unit>(out var u) && go.TryGetComponent<UnitVision>(out var uv))
        {
            if (uv.visionSkill == null) uv.visionSkill = u.archetype;

            uv.teamId = teamId;
            
            Debug.Log($"[SpawnUnitsCoordinator] InitUnitVision for {go.name}: Team {teamId}, Range {uv.visionSkill?.visionRange ?? 0}");
            
            uv.UpdateVisionNow();
        }
        else
        {
            Debug.LogWarning($"[SpawnUnitsCoordinator] {go.name} missing Unit or UnitVision component");
        }
    }
    */
    
    
    private void InitUnitVision(GameObject go, int teamId)
    {
        // Dedi-serverillä ei tarvita paikallista visualisointia
        if (NetworkServer.active && !NetworkClient.active) return;

        if (go.TryGetComponent<Unit>(out var u) && go.TryGetComponent<UnitVision>(out var uv))
        {
            // anna molemmat arvot yhdellä kutsulla ja anna UV:n hoitaa siivous & eka päivitys
            uv.InitializeVision(teamId, u.archetype);

            Debug.Log($"[SpawnUnitsCoordinator] InitUnitVision for {go.name}: Team {teamId}, Range {uv.visionSkill?.visionRange ?? 0}");
        }
        else
        {
            Debug.LogWarning($"[SpawnUnitsCoordinator] {go.name} missing Unit or UnitVision component");
        }
    }
    
    

}
