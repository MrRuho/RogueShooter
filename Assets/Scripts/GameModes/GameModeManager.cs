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

    private void OnEnable()
    {
        LevelLoader.LevelReady += OnLevelReady;    // ← kuuntele jokaista level-latausta
    }

    private void OnDisable()
    {
        LevelLoader.LevelReady -= OnLevelReady;
    }

     private void OnLevelReady(Scene _)
    {
        if (!NetMode.IsOnline) return; // vain offline
        StartCoroutine(OfflineBootstrap()); // ← käynnistä spawnaus myös reloadin jälkeen
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

    private IEnumerator OfflineBootstrap()
    {
        // Nämä guardit ovat jo projektissa: odota että kaikki on olemassa
        yield return new WaitUntil(() => SpawnUnitsCoordinator.Instance != null);
        yield return new WaitUntil(() => LevelGrid.Instance != null && PathFinding.Instance != null);

        if (SelectedMode == GameMode.SinglePlayer)
        {
            // Spawn offline -unitit siihen sceneen, missä koordinaattori on
            SpawnUnitsCoordinator.Instance.SpwanSinglePlayerUnits();
            LevelGrid.Instance.RebuildOccupancyFromScene();
        }
    }
    
    void Handle_LevelReady(Scene s)
    {
        if (!NetMode.IsServer) return;           // vain server
        StartCoroutine(Co_ServerSpawnUnits());
    }

    IEnumerator Co_ServerSpawnUnits()
    {
        // odota riippuvuudet
        yield return new WaitUntil(() =>
            SpawnUnitsCoordinator.Instance != null &&
            LevelGrid.Instance != null &&
            PathFinding.Instance != null);

        // Spawnaa serveriltä nykyisen pelitilan mukaan
        // (tee koordinaattoriin yksi sisäänajo, ettei tarvitse miettiä moodia tässä)
        //SpawnUnitsCoordinator.Instance.ServerSpawnUnitsForCurrentMode();

        // päivitä miehitys varmuudeksi
        LevelGrid.Instance.RebuildOccupancyFromScene();
    }
    
}

