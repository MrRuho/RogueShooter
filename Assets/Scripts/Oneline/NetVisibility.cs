using Mirror;
using UnityEngine;

public class NetVisibility : NetworkBehaviour
{
    [SerializeField] private GameObject target; // se esine jonka näkyvyyttä halutaan ohjata

    [SyncVar(hook = nameof(OnChanged))]
    private bool isVisible;

    void OnChanged(bool _, bool now) => Apply(now);

    public override void OnStartClient() => Apply(isVisible);

    private void Apply(bool now)
    {
        if (target) target.SetActive(now);
    }

    // --- SERVER-API ---
    [Server] public void ServerShow()            { isVisible = true;  Apply(true);  }
    [Server] public void ServerHide()            { isVisible = false; Apply(false); }
    [Server] public void ServerSetVisible(bool v){ isVisible = v;     Apply(v);     }

    // --- CLIENT-API (authority) ---
    [Command] private void CmdSetVisible(bool v) => ServerSetVisible(v);

    /// Kutsu tätä mistä tahansa: hoitaa sekä server- että client-puolen.
    public void SetVisibleAny(bool v)
    {
        if (isServer) ServerSetVisible(v);
        else          CmdSetVisible(v);  // vaatii client authorityn tälle objektille
    }
}
