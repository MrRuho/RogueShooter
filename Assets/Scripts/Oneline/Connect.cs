using UnityEngine;
using Mirror;
using Utp;

/// <summary>
/// This class is responsible for connecting to the Unity Relay service.
/// It provides methods to host a game and join a game as a client.
/// </summary>
public class Connect : MonoBehaviour
{
     [SerializeField] private GameNetworkManager nm; // vedä tämä Inspectorissa

    void Awake()
    {
        // find the NetworkManager in the scene if not set in Inspector
        if (!nm) nm = NetworkManager.singleton as GameNetworkManager;
        if (!nm) nm = FindFirstObjectByType<GameNetworkManager>();
        if (!nm) Debug.LogError("[Connect] GameNetworkManager not found in scene.");
    }

    public void Host()
    {
        if (!nm)
        {
            Debug.LogError("[Connect] GameNetworkManager not found in scene.");
            return;
        }

        nm.StartRelayHost(2, null);
    }

    public void Client ()
    {
        if (!nm)
        {
            Debug.LogError("[Connect] GameNetworkManager not found in scene.");
            return;
        }
        
        nm.JoinRelayServer();
    }

}
