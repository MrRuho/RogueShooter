using UnityEngine;

public class SingleplayerManager : MonoBehaviour
{
    public GameObject playerPrefab;


    void Start()
    {
        // Create units in the scene
        // Instantiate the player prefab at the specified position and rotation
        Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity);

        Instantiate(playerPrefab, new Vector3(-2, 0, 0), Quaternion.identity);
    }

}

// This script is responsible for managing the singleplayer game mode. It creates units in the scene when the game starts.
// The player prefab is instantiated at the specified position and rotation.