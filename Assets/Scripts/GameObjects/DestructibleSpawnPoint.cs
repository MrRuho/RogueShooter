using Mirror;
using UnityEngine;
/// <summary>
/// This class is responsible for spawning destructible objects in the game.
/// This object is only placeholder, which spawns the actual destructible object and then destroys itself.
/// Because spawning must be done by the server, this object must exist on the server.
/// </summary>
public class DestructibleSpawnPoint : MonoBehaviour
{
    [SerializeField] private GameObject destructiblePrefab;
    public GameObject Prefab => destructiblePrefab;

    private void Start() 
    {
        // OFFLINE: ei verkkoa -> luo paikallisesti (näkyy heti)
        if (!NetworkClient.active && !NetworkServer.active)
        {
            Debug.Log($"[DestructibleSpawnPoint] (Offline) Spawning destructible at {transform.position}");
            Instantiate(destructiblePrefab, transform.position, transform.rotation);
            Destroy(gameObject);
        }
    }

 
    public void CreteObject()
    {
        Debug.Log("[DestructibleSpawnPoint] CreteObject called." + (NetworkServer.active ? "(Server)" : "(Client)"));

        // ONLINE: server luo ja spawnnaa
        if (NetworkServer.active)
        {
            Debug.Log($"[DestructibleSpawnPoint] Spawning destructible at {transform.position}");
            var go = Instantiate(destructiblePrefab, transform.position, transform.rotation);
            NetworkServer.Spawn(go);
            Destroy(gameObject);
            return;
        }
        else 
        {
            // Puhdas client (ei host): odota server-spawnia tai jätä tämän hoito serverille
            Destroy(gameObject);
        }
    }
}
