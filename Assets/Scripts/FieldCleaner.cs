using System.Linq;
using Mirror;
using UnityEngine;

public class FieldCleaner : MonoBehaviour
{
    public static void ClearAll()
    {
    
        // Find all friendly and enemy units (also inactive, just in case)
        var friendlies = Resources.FindObjectsOfTypeAll<FriendlyUnit>()
                          .Where(u => u != null && u.gameObject.scene.IsValid());
        var enemies    = Resources.FindObjectsOfTypeAll<EnemyUnit>()
                          .Where(u => u != null && u.gameObject.scene.IsValid());

        foreach (var u in friendlies) Despawn(u.gameObject);
        foreach (var e in enemies)    Despawn(e.gameObject);
    }

    static void Despawn(GameObject go)
    {
        // if server is active, use Mirror's destroy; otherwise normal Unity Destroy
        if (NetworkServer.active)
        {
            NetworkServer.Destroy(go);
        }
        else
        {
            Object.Destroy(go);
        }
    }
}