using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectSpawnPlaceHolder : MonoBehaviour
{
    [SerializeField] private GameObject objectPrefab;
    public GameObject Prefab => objectPrefab;

    private void Start()
    {
        // OFFLINE: käytä SpawnRouter.SpawnLocal → asettaa instanssin samaan level-sceneen kuin placeholder
        if (!NetworkClient.active && !NetworkServer.active)
        {
            SpawnRouter.SpawnLocal(
                prefab: objectPrefab,
                pos: transform.position,
                rot: transform.rotation,
                source: transform,
                sceneName: gameObject.scene.name
            );
            Destroy(gameObject);
            return;
        }

        // PUHDAS CLIENT: server spawnaa → placeholder pois
        if (NetworkClient.active && !NetworkServer.active)
        {
            Destroy(gameObject);
        }
    }

    // Kutsutaan serveriltä (esim. MapContentSpawnerista)
    public GameObject CreteObject()
    {
        if (!NetworkServer.active) return null;

        Scene levelScene = gameObject.scene;

        var go = SpawnRouter.SpawnNetworkServer(
            prefab: objectPrefab,
            pos: transform.position,
            rot: transform.rotation,
            source: transform,            // → tämän placeholderin scene
            sceneName: levelScene.name,   // → ohita Core, lukitse oikeaan level-sceneen
            parent: null,
            owner: null,                  // ympäristöpropsit eivät tarvitse owneria
            beforeSpawn: obj =>
            {
                // Jos haluat inittejä (HP tms.), tee ne tässä
                // esim: if (obj.TryGetComponent<DestructibleObject>(out var d)) d.Init(...);
            }
        );

        Destroy(gameObject);
        return go;
    }
}
