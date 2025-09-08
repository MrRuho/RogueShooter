using Mirror;
using UnityEngine;

public class NetworkSyncAgent : NetworkBehaviour
{
    public static NetworkSyncAgent Local;   // helppo osoitin vain omaan agenttiin
    [SerializeField] private GameObject bulletPrefab; // vedä Inspectorissa sama prefab

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Local = this;
    }

    // Client → Server
    [Command(requiresAuthority = true)]
    public void CmdSpawnBullet(Vector3 spawnPos, Vector3 targetPos)
    {
        if (bulletPrefab == null) { Debug.LogWarning("[NetSync] bulletPrefab missing"); return; }

        var go = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        if (go.TryGetComponent<BulletProjectile>(out var bp))
        {
            bp.Setup(new Vector3(targetPos.x, spawnPos.y, targetPos.z));
        }
        NetworkServer.Spawn(go);
    }
}
