using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DestroyWithScene : MonoBehaviour
{
    Scene boundScene;
    public void BindToSceneOf(GameObject go)
    {
        boundScene = go.scene;                     // instanssi!
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    void OnDestroy() => SceneManager.sceneUnloaded -= OnSceneUnloaded;

    void OnSceneUnloaded(Scene s)
    {
        if (s == boundScene) Destroy(gameObject);  // siivoaa varmasti
    }
}
