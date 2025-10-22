// ClientPreJoinCleaner.cs
using System.Collections;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClientPreJoinCleaner
{
    public static IEnumerator PrepareForOnlineJoin()
    {
        Debug.Log("[ClientPreJoinCleaner] Pura ei Core Scenet");
        // 1) Pura kaikki ei-Core -scenet pois
        string core = LevelLoader.Instance ? LevelLoader.Instance.CoreSceneName : "Core";
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.name != core)
            {
                var op = SceneManager.UnloadSceneAsync(s);
                if (op != null) yield return op;
            }
        }

        // 2) Siivoa mahdolliset offline-jäänteet Coresta,
        //    jotka pitäisi tulla serveriltä netin kautta
        DestroyServerProvidedLeftoversInCore(core);

        // (valinnainen)
        yield return Resources.UnloadUnusedAssets();
    }

    static void DestroyServerProvidedLeftoversInCore(string core)
    {
        Debug.Log("[ClientPreJoinCleaner] Siivotaan kaikki offline jäänteet");
        var coreScene = SceneManager.GetSceneByName(core);
        if (!coreScene.IsValid()) return;

        // Hae kaikki juuret Core-scenen alta ja siivoa tunnetut tyypit
        foreach (var root in coreScene.GetRootGameObjects())
        {
            // Unitit (mukaan lukien ystävä/vihollis-tagit)
            foreach (var u in root.GetComponentsInChildren<Unit>(true)) Object.Destroy(u.gameObject);
            foreach (var f in root.GetComponentsInChildren<FriendlyUnit>(true)) Object.Destroy(f.gameObject);
            foreach (var e in root.GetComponentsInChildren<EnemyUnit>(true)) Object.Destroy(e.gameObject);

            // Tuhoutuvat objektit
            foreach (var d in root.GetComponentsInChildren<DestructibleObject>(true)) Object.Destroy(d.gameObject);

            // Ragdollit & binderit
            foreach (var rb in root.GetComponentsInChildren<RagdollPoseBinder>(true)) Object.Destroy(rb.gameObject);
            foreach (var rd in root.GetComponentsInChildren<UnitRagdoll>(true)) Object.Destroy(rd.gameObject);

            // (Tarvittaessa: ohjukset/granaatit, placeholderit yms.)
            // foreach (var b in root.GetComponentsInChildren<BulletProjectile>(true)) Object.Destroy(b.gameObject);
            // foreach (var g in root.GetComponentsInChildren<GrenadeProjectile>(true)) Object.Destroy(g.gameObject);
            // foreach (var ph in root.GetComponentsInChildren<ObjectSpawnPlaceHolder>(true)) Object.Destroy(ph.gameObject);
        }

        // Nollaa ruudukon miehitykset varmuudeksi
        LevelGrid.Instance?.ClearAllOccupancy();
    }
}
