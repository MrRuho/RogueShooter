using UnityEngine;
using Utp;

/// <summary>
/// This class is responsible for connecting to the Unity Relay service.
/// It provides methods to host a game and join a game as a client.
/// </summary>
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
