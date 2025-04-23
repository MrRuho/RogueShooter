using Mirror;
using UnityEngine;

/// <summary>
/// This class is responsible for managing the game mode and spawning units in the game.
/// It checks if the game is being played online or offline and spawns units accordingly.
/// </summary>

public class GameModeManager : MonoBehaviour
{
    public GameObject playerPrefab;

    public GameObject enemyPrefab;

    void Start()
    {
        
        if (NetworkClient.isConnected || NetworkServer.active)
        {
            // If the game is being played online, destroy this object to avoid duplicate spawning
            return;
        } else 
        {
            Debug.Log("Game is offline, spawning units.");
            SpawnUnits();
            SpawnEnemyUnits();
        }
    }
    // <summary>
    /// This method is responsible for spawning units in the game.
    /// In online the host will spawn this units.
    /// </summary>
    private void SpawnUnits()
    {
        // Create units in the scene
        // Instantiate the player prefab at the specified position and rotation
        Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity);

        Instantiate(playerPrefab, new Vector3(2, 0, 0), Quaternion.identity);
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

}
