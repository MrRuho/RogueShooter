/*
using System.Collections;
using Mirror;
using UnityEngine;


/// <summary>
/// Online: Client need this to get destroyed unit rootbone to create ragdoll form it.
/// </summary>
public class RagdollPoseBinder : NetworkBehaviour
{
    [SyncVar] public uint sourceUnitNetId;
    [SyncVar] public Vector3 lastHitPos;
    [SyncVar] public int overkill;

    [ClientCallback]
    private void Start()
    {
        StartCoroutine(ApplyPoseWhenReady());
    }

    private IEnumerator ApplyPoseWhenReady()
    {
        var (root, why) = TryFindOriginalRootBone(sourceUnitNetId);
        if (root != null)
        {
            if (TryGetComponent<UnitRagdoll>(out var unitRagdoll))
            {
                unitRagdoll.SetOverkill(overkill);
                unitRagdoll.SetLastHitPosition(lastHitPos);
                unitRagdoll.Setup(root);
            }
            yield break;
        }

        Debug.Log($"[Ragdoll] waiting root for netId {sourceUnitNetId} ({why})");

        yield return new WaitForEndOfFrame();
        Debug.LogWarning($"[RagdollPoseBinder] Source root not found for netId {sourceUnitNetId}");
    }

    private static (Transform root, string why) TryFindOriginalRootBone(uint netId)
    {
        if (netId == 0) return (null, "netId==0");
        if (!Mirror.NetworkClient.spawned.TryGetValue(netId, out var id) || id == null)
            return (null, "identity not in NetworkClient.spawned");

        // Löydä UnitRagdollSpawn myös hierarkiasta
        var spawner = id.GetComponent<UnitRagdollSpawn>()
                ?? id.GetComponentInChildren<UnitRagdollSpawn>(true)
                ?? id.GetComponentInParent<UnitRagdollSpawn>();
        if (spawner == null) return (null, "UnitRagdollSpawn missing under identity");

        if (spawner.OriginalRagdollRootBone == null) return (null, "OriginalRagdollRootBone null");
        return (spawner.OriginalRagdollRootBone, null);
    }

}
*/
using Mirror;
using UnityEngine;
using System.Collections;

public class RagdollPoseBinder : NetworkBehaviour
{
    [SyncVar] public uint sourceUnitNetId;
    [SyncVar] public Vector3 lastHitPos;
    [SyncVar] public int overkill;

    [SerializeField] float bindTimeout = 0.75f;   // varalta jos hierarkia/NetID tulee myöhässä

    UnitRagdoll ragdoll;

    void Awake()
    {
        ragdoll = GetComponent<UnitRagdoll>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        StartCoroutine(Co_TryBindUntilFound());
    }

    IEnumerator Co_TryBindUntilFound()
    {
        float t = 0f;
        while (t < bindTimeout)
        {
            Transform rootBone = TryResolveOriginalRootBoneOnClient();
            if (rootBone != null)
            {
                // siirrä metadatat ja sido pose
                if (ragdoll != null)
                {
                    ragdoll.SetOverkill(overkill);
                    ragdoll.SetLastHitPosition(lastHitPos);
                    ragdoll.Setup(rootBone);
                }
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[RagdollPoseBinder] Failed to bind original root bone in time.");
    }

    Transform TryResolveOriginalRootBoneOnClient()
    {
        if (!NetworkClient.active) return null;
        if (!NetworkClient.spawned.TryGetValue(sourceUnitNetId, out var srcNi) || srcNi == null) return null;

        // Hae kaatuneen unitin spawneri, jossa viite on serialized
        var spawner = srcNi.GetComponentInChildren<UnitRagdollSpawn>(true);
        if (spawner != null && spawner.OriginalRagdollRootBone != null)   // (huom. kirjoitusasu)
            return spawner.OriginalRagdollRootBone;

        return null;
    }
}
