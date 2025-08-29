using Mirror;
using UnityEngine;

/// <summary>
/// This class is responsible for managing the game mode and spawning units in the game.
/// It checks if the game is being played online or offline and spawns units accordingly.
/// </summary>
public enum GameMode { SinglePlayer, CoOp, Versus }
public class GameModeManager : MonoBehaviour
{
    public static GameMode SelectedMode { get; private set; } = GameMode.SinglePlayer;

    public static void SetSinglePlayer() => SelectedMode = GameMode.SinglePlayer;
    public static void SetCoOp()         => SelectedMode = GameMode.CoOp;
    public static void SetVersus()       => SelectedMode = GameMode.Versus;


    [Header("Prefabs (all must have NetworkIdentity if used online)")]
    public GameObject playerPrefab;

    public GameObject coOpPlayerPrefab;

    public GameObject enemyPrefab;

    void Start()
    {

        // OFFLINE: Singleplayer -> instanssoi heti
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            if (SelectedMode == GameMode.SinglePlayer)
            {
                Debug.Log("Game is offline, spawning singleplayer units.");
                SinglePlayerGameMode();
                return;
            }

            // Jos tänne päädytään ja tila on CoOp/Versus, se tarkoittaa
            // että UI ei vielä käynnistänyt verkkoa. Ei spawnailla mitään.
            Debug.Log("Selected online mode without network active. Waiting for host/client.");
            return;
        }

        // ONLINE: Server hoitaa spawnaamisen, clientit eivät tee mitään Startissa
        if (NetworkServer.active)
        {
            Debug.Log($"Server online in {SelectedMode} mode. Server will spawn enemies.");
            // Pelaaja-objektit tulevat NetworkManager.OnServerAddPlayerissa.
            ServerSpawnInitialForSelectedMode();
        }
    }

    // ==== UI-KUTSUT ====
    public void OnClickSinglePlayer()
    {
        SelectedMode = GameMode.SinglePlayer;
        // Lataa peliscene jos et jo siellä, tai anna tämän skriptin Start() hoitaa
        // SceneManager.LoadScene("Game");
    }
    public void OnClickCoOp()
    {
        SelectedMode = GameMode.CoOp;
        Debug.Log("SelectedMode set to CoOp. Now start host or client.");
        // Tässä voit näyttää ConnectCanvasin, tai käynnistää suoraan host/clientin:
        // NetworkManager.singleton.StartHost(); // host napille
        // NetworkManager.singleton.StartClient(); // client napille
    }

    public void OnClickVersus()
    {   
        SelectedMode = GameMode.Versus;
        // Samoin kuin CoOp
    }

    // ==== OFFLINE (Singleplayer) ====
    void SinglePlayerGameMode()
    {
        SpawnPlayer1UnitsOffline();
        SpawnEnemyUnitsOffline();   
    }

    void SpawnPlayer1UnitsOffline()
    {
        Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
        Instantiate(playerPrefab, new Vector3(2, 0, 0), Quaternion.identity);
    }

    void SpawnEnemyUnitsOffline()
    {
        Instantiate(enemyPrefab, new Vector3(4, 0, 8), Quaternion.identity);
        Instantiate(enemyPrefab, new Vector3(6, 0, 8), Quaternion.identity);
    }

    // <summary>
    /// This method is responsible for spawning units in the game.
    /// In online the host will spawn this units.
    /// </summary>
    private void SpawnPlayer1Units()
    {
        // Create units in the scene
        // Instantiate the player prefab at the specified position and rotation
        Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity);

        Instantiate(playerPrefab, new Vector3(2, 0, 0), Quaternion.identity);
    }

    private void SpawnPlayer2Units()
    {
        // Create units in the scene
        // Instantiate the player prefab at the specified position and rotation
        Instantiate(coOpPlayerPrefab, new Vector3(4, 0, 0), Quaternion.identity);

        Instantiate(coOpPlayerPrefab, new Vector3(6, 0, 0), Quaternion.identity);
    }
    /// <summary>
    /// This method is responsible for spawning enemy units in the game.
    /// In online the client will spawn this units.
    /// </summary>
    private void SpawnEnemyUnits()
    {
        // Create enemy units in the scene
        // Instantiate the enemy prefab at the specified position and rotation
        Instantiate(enemyPrefab, new Vector3(4, 0, 6), Quaternion.identity);

        Instantiate(enemyPrefab, new Vector3(6, 0, 6), Quaternion.identity);
    }

    // ==== ONLINE (Server only) ====
    [Server]
    void ServerSpawnInitialForSelectedMode()
    {
        switch (SelectedMode)
        {
            case GameMode.CoOp:
                ServerSpawnEnemies();
                break;
            case GameMode.Versus:
                // Versuksessa ei ehkä ole AI-vihollisia -> jätä tyhjäksi tai spawnaa neutraaleja
                break;
            case GameMode.SinglePlayer:
                // Ei pitäisi tapahtua verkossa, mutta varmistetaan
                ServerSpawnEnemies();
                break;
        }
    }

    [Server]
    void ServerSpawnEnemies()
    {
        var e1 = Instantiate(enemyPrefab, new Vector3(4, 0, 6), Quaternion.identity);
        NetworkServer.Spawn(e1);
        var e2 = Instantiate(enemyPrefab, new Vector3(6, 0, 6), Quaternion.identity);
        NetworkServer.Spawn(e2);
    }

}
