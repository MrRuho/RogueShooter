using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapContentSpawner : MonoBehaviour // ⟵ EI NetworkBehaviour
{
    private void Start()
    {
        if (!NetMode.IsServer) return;   // varmistus
        StartCoroutine(SpawnThenBake_ServerOnly());
    }

    private IEnumerator SpawnThenBake_ServerOnly()
    {
        Scene mapScene = gameObject.scene;

        var placeholders = FindObjectsByType<ObjectSpawnPlaceHolder>(FindObjectsSortMode.None);
        int spawned = 0;
        foreach (var sp in placeholders)
        {
            if (sp.gameObject.scene == mapScene)
            {
                var go = sp.CreteObject();   // tämä jo käyttää SpawnRouteria → assetId-prefab spawn, ei sceneId
                if (go) spawned++;
            }
        }

        // Odota, että LevelGrid/PathFinding/EdgeBaker ovat valmiit
        yield return new WaitUntil(() =>
            EdgeBaker.Instance != null &&
            LevelGrid.Instance  != null &&
            PathFinding.Instance != null
        );
        yield return null;

        // Server bake
        EdgeBaker.Instance.BakeAllEdges();

    }
}

