using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClearDebris
{
    /*
    private const string CORE_SCENE_NAME = "Core";
    private const string DEBRIS_LAYER_NAME = "Debris";

    public static void DestroyAllDebrisInLevelScene()
    {
        Scene levelScene = FindLevelScene();
        
        if (!levelScene.IsValid() || !levelScene.isLoaded)
        {
            Debug.LogWarning("[ClearDebris] Level scene not found or not loaded");
            return;
        }

        int debrisLayer = LayerMask.NameToLayer(DEBRIS_LAYER_NAME);
        if (debrisLayer == -1)
        {
            Debug.LogWarning($"[ClearDebris] Layer '{DEBRIS_LAYER_NAME}' not found");
            return;
        }

        GameObject[] rootObjects = levelScene.GetRootGameObjects();
        int destroyedCount = 0;

        foreach (GameObject rootObj in rootObjects)
        {
            destroyedCount += DestroyDebrisInHierarchy(rootObj.transform, debrisLayer);
        }

        Debug.Log($"[ClearDebris] Destroyed {destroyedCount} debris objects from scene '{levelScene.name}'");
    }

    private static Scene FindLevelScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.name != CORE_SCENE_NAME)
            {
                return scene;
            }
        }
        return default;
    }

    private static int DestroyDebrisInHierarchy(Transform parent, int debrisLayer)
    {
        int count = 0;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            
            if (child.gameObject.layer == debrisLayer)
            {
                Object.Destroy(child.gameObject);
                count++;
            }
            else
            {
                count += DestroyDebrisInHierarchy(child, debrisLayer);
            }
        }

        return count;
    }
    */
}
