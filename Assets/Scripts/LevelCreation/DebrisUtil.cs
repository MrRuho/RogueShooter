using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Kevyt util: poista Debris-layerillä olevat paikalliset GameObjectit kaikista ei-Core -sceneistä.
/// Tämä on pelkkä “varmistuskerros” – varsinainen siivous tapahtuu jo scene-unloadissa.
/// </summary>
public static class DebrisUtil
{
    public static int DestroyAllDebrisExceptCore(string coreName = "Core", string debrisLayerName = "Debris")
    {
        int debrisLayer = LayerMask.NameToLayer(debrisLayerName);
        if (debrisLayer == -1) return 0;

        int destroyed = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded || s.name == coreName) continue;

            var roots = s.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                destroyed += DestroyDebrisRecursive(roots[r].transform, debrisLayer);
            }
        }
        return destroyed;
    }

    private static int DestroyDebrisRecursive(Transform t, int debrisLayer)
    {
        int cnt = 0;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i);
            if (c.gameObject.layer == debrisLayer)
            {
                Object.Destroy(c.gameObject);
                cnt++;
            }
            else
            {
                cnt += DestroyDebrisRecursive(c, debrisLayer);
            }
        }
        return cnt;
    }
}
