using Mirror;
using UnityEngine;

/// <summary>
/// Spawns map content such as destructible objects when the server starts.
/// </summary>
public class MapContentSpawner : NetworkBehaviour
{
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[MapContentSpawner] OnStartServer - Spawning map content.");

        // Find all Destructibleobjects placeholders in the scene and spawn real destructible objects
        var spawnPoints = FindObjectsByType<ObjectSpawnPlaceHolder>(FindObjectsSortMode.None);
        foreach (var sp in spawnPoints)
        {
            sp.CreteObject();
        }
    }
}
