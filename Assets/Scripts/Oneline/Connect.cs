using UnityEngine;
using Utp;

public class Connect : MonoBehaviour
{
    public RelayNetworkManager networkManager;

    public void Host()
    {
        networkManager.StartRelayHost(2, null);
    }

    public void Client ()
    {
        networkManager.JoinRelayServer();
    }

}
