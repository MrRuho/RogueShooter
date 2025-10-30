using Mirror;
using UnityEngine;

public class NetworkVisionRpcHub : NetworkBehaviour
{
    public static NetworkVisionRpcHub Instance;
    void Awake() => Instance = this;

    [ClientRpc]
    public void RpcResetLocalVisionAndRebuild()
    {
        var tvs = TeamVisionService.Instance;
        if (tvs != null)
        {
            tvs.ClearTeamVision(0);
            tvs.ClearTeamVision(1);
        }

        foreach (var uv in Object.FindObjectsByType<UnitVision>(FindObjectsSortMode.None))
            if (uv != null && uv.IsInitialized)
                uv.UpdateVisionNow();
    }
}