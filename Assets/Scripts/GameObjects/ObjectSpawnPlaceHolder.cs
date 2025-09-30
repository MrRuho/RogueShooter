using Mirror;
using UnityEngine;
/// <summary>
/// This class is responsible for spawning objects in the game.
/// This object is only placeholder, which spawns the actual object and then destroys itself.
/// Because spawning must be done by the server, this object must exist on the server.
/// </summary>
public class ObjectSpawnPlaceHolder : MonoBehaviour
{
    [SerializeField] private GameObject objectPrefab;
    public GameObject Prefab => objectPrefab;

    private void Start()
    {
        // OFFLINE: ei verkkoa -> luo paikallisesti (näkyy heti)
        if (!NetworkClient.active && !NetworkServer.active)
        {
            Instantiate(objectPrefab, transform.position, transform.rotation);
            Destroy(gameObject);
        }

        // PUHDAS CLIENT: serveri spawnaa oikean → poista placeholder heti
        if (NetworkClient.active && !NetworkServer.active)
        {
            Destroy(gameObject);
            return;
        }
  
    }

    public void CreteObject()
    {
        // ONLINE: server luo ja spawnnaa
        if (NetworkServer.active)
        {
            Debug.Log($"[objectSpawnPoint] Spawning object at {transform.position}");
            var go = Instantiate(objectPrefab, transform.position, transform.rotation);
            NetworkServer.Spawn(go);
            Destroy(gameObject);
            return;
        }
    }
}
