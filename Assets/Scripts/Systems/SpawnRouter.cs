// Assets/Scripts/Runtime/Systems/SpawnRouter.cs
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public static class SpawnRouter
{
    /// <summary>Muokkaa tätä jos Core on eri nimellä.</summary>
    public static string CoreSceneName = "Core";

    /// <summary>
    /// Paikallinen spawn (ei NetworkServer.Spawn). Palauttaa instanssin.
    /// </summary>
    public static GameObject SpawnLocal(
        GameObject prefab,
        Vector3 pos,
        Quaternion rot,
        Transform source = null,
        string sceneName = null,
        Transform parent = null,
        Action<GameObject> beforeReturn = null)
    {
        var go = UnityEngine.Object.Instantiate(prefab, pos, rot);
        if (parent != null) go.transform.SetParent(parent, true);

        var targetScene = ResolveScene(source, sceneName);
        if (targetScene.IsValid()) SceneManager.MoveGameObjectToScene(go, targetScene);

        beforeReturn?.Invoke(go);
        return go;
    }

    /// <summary>
    /// Server-spawn (Mirror). Luo instanssin, siirtää sen level-scenelle ja kutsuu NetworkServer.Spawn.
    /// </summary>
    public static GameObject SpawnNetworkServer(
        GameObject prefab,
        Vector3 pos,
        Quaternion rot,
        Transform source = null,
        string sceneName = null,
        Transform parent = null,
        NetworkConnectionToClient owner = null,
        Action<GameObject> beforeSpawn = null)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[SpawnRouter] SpawnNetworkServer() called without an active server.");
            return null;
        }

        var go = UnityEngine.Object.Instantiate(prefab, pos, rot);
        if (parent != null) go.transform.SetParent(parent, true);

        var targetScene = ResolveScene(source, sceneName);
        if (targetScene.IsValid()) SceneManager.MoveGameObjectToScene(go, targetScene);

        beforeSpawn?.Invoke(go);

        if (!go.TryGetComponent<NetworkIdentity>(out _))
            Debug.LogWarning("[SpawnRouter] Network spawn prefab has no NetworkIdentity.");

        if (owner != null) NetworkServer.Spawn(go, owner);
        else NetworkServer.Spawn(go);

        return go;
    }

    /// <summary>
    /// Yhtenäinen scene-resoluutio:
    /// 1) nimellä, 2) lähdeobjektin scene, 3) ensimmäinen ladattu ei-Core, 4) muuten default (invalid).
    /// </summary>
    private static Scene ResolveScene(Transform source, string sceneName)
    {
        // 1) Nimen perusteella
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            var byName = SceneManager.GetSceneByName(sceneName);
            if (byName.IsValid() && byName.isLoaded) return byName;
        }

        // 2) Lähdeobjektin scene (esim. kuollut unit, ase tms.)
        if (source != null)
        {
            var fromSource = source.gameObject.scene;
            if (fromSource.IsValid() && fromSource.isLoaded) return fromSource;
        }

        // 3) Ensimmäinen ladattu ei-Core-scene (nykyinen level)
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && !string.Equals(s.name, CoreSceneName, StringComparison.OrdinalIgnoreCase))
                return s;
        }

        // 4) Ei löytynyt – palautetaan invalid (kutsuja voi itse päättää mitä tekee)
        return default;
    }
}
