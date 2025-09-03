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
    public static void SetCoOp() => SelectedMode = GameMode.CoOp;
    public static void SetVersus() => SelectedMode = GameMode.Versus;

    void Start()
    {
        // if game is offline, spawn singleplayer units
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            SpawnUnits();
        } else
        {
            Debug.Log("Game is online, waiting for host/client to spawn units.");
        }
    }
    
    private void SpawnUnits()
    {
        if (SelectedMode == GameMode.SinglePlayer)
        {
            Debug.Log("Game is offline, spawning singleplayer units.");
            SpawnUnitsCoordinator.Instance.SpwanSinglePlayerUnits();
            return;
        }
    }
}
