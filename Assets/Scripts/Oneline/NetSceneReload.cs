using Mirror;
using UnityEngine.SceneManagement;

public static class NetSceneReload {
    public static void ReloadForAll()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        NetworkManager.singleton.ServerChangeScene(sceneName);
    }
}
