using Mirror;
using UnityEngine;

/// <summary>
/// This class is responsible for managing the game mode and spawning units in the game.
/// It checks if the game is being played online or offline and spawns units accordingly.
/// </summary>

public class GameModeManager : MonoBehaviour
{
    public GameObject playerPrefab;

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
        }
    }

    private void SpawnUnits()
    {
        // Create units in the scene
        // Instantiate the player prefab at the specified position and rotation
        Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity);

        Instantiate(playerPrefab, new Vector3(2, 0, 0), Quaternion.identity);
    }

}
