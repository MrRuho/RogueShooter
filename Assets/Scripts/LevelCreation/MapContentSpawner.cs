using Mirror;
using UnityEngine;

public class MapContentSpawner : NetworkBehaviour
{
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[MapContentSpawner] OnStartServer - Spawning map content.");

        // Find all Destructibleobjects placeholders in the scene and spawn real destructible objects
        var spawnPoints = FindObjectsByType<DestructibleSpawnPoint>(FindObjectsSortMode.None);
        foreach (var sp in spawnPoints)
        {
            sp.CreteObject();
        }
    }        
}
