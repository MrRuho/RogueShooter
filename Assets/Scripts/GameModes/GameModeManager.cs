/*
using System.Collections;
using UnityEngine;
using Utp;

/// <summary>
/// This class is responsible for managing the game mode
/// It checks if the game is being played online or offline and spawns units accordingly.
/// </summary>
public enum GameMode { SinglePlayer, CoOp, Versus }
public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }
    public static GameMode SelectedMode { get; private set; } = GameMode.SinglePlayer;
    public static void SetSinglePlayer() => SelectedMode = GameMode.SinglePlayer;
    public static void SetCoOp() => SelectedMode = GameMode.CoOp;
    public static void SetVersus() => SelectedMode = GameMode.Versus;

    void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("More than one GameModeManager in the scene!" + transform + " " + Instance);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    
    void Start()
    {
        // if game is offline, spawn singleplayer units
        if (!GameNetworkManager.Instance.IsNetworkActive())
        {
            StartCoroutine(OfflineBootstrap());
        }
        else
        {
            Debug.Log("Game is online, waiting for host/client to spawn units.");
        }
    }

    private IEnumerator OfflineBootstrap()
    {
        // Odota että Level-scenessä oleva SpawnUnitsCoordinator on herännyt
        yield return new WaitUntil(() => SpawnUnitsCoordinator.Instance != null);

        // (Valinnaiset mutta suositeltavat guardit)
        yield return new WaitUntil(() => LevelGrid.Instance != null && PathFinding.Instance != null);

        if (SelectedMode == GameMode.SinglePlayer)
        {
            SpawnUnitsCoordinator.Instance.SpwanSinglePlayerUnits(); // sama metodi kuin ennen
            // Jos haluat varmistaa ruudukon tilan heti spawnausten jälkeen:
            LevelGrid.Instance.RebuildOccupancyFromScene();
        }
    }
}
*/
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utp;

public enum GameMode { SinglePlayer, CoOp, Versus }

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }
    
    public static GameMode SelectedMode { get; private set; } = GameMode.SinglePlayer;
    
    public static void SetSinglePlayer() => SelectedMode = GameMode.SinglePlayer;
    public static void SetCoOp() => SelectedMode = GameMode.CoOp;
    public static void SetVersus() => SelectedMode = GameMode.Versus;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (!GameNetworkManager.Instance.IsNetworkActive())
        {
            StartCoroutine(OfflineBootstrap());
        }
        else
        {
            Debug.Log("Game is online, waiting for host/client to spawn units.");
        }
    }

    public bool LevelIsLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.name != "Core")
            {
                return true;
            }
        }
        return false;
    }

    public Scene GetLoadedLevelScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.name != "Core")
            {
                return scene;
            }
        }
        return default;
    }

    private void SpawnUnits()
    {
        if (SelectedMode == GameMode.SinglePlayer)
        {
            SpawnUnitsCoordinator.Instance.SpwanSinglePlayerUnits();
            return;
        }
    }

    private IEnumerator OfflineBootstrap()
    {
        yield return new WaitUntil(() => LevelIsLoaded());
        
        yield return new WaitUntil(() => SpawnUnitsCoordinator.Instance != null);

        yield return new WaitUntil(() => LevelGrid.Instance != null && PathFinding.Instance != null);

        if (SelectedMode == GameMode.SinglePlayer)
        {
            SpawnUnitsCoordinator.Instance.SpwanSinglePlayerUnits();
            LevelGrid.Instance.RebuildOccupancyFromScene();
        }
    }
}

