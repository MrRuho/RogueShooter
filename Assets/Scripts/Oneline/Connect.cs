using UnityEngine;
using TMPro;
using Mirror;
using Utp;

/// <summary>
/// This class is responsible for connecting to the Unity Relay service.
/// It provides methods to host a game and join a game as a client.
/// </summary>
public class Connect : MonoBehaviour
{
    [SerializeField] private GameNetworkManager nm; // vedä tämä Inspectorissa
    [SerializeField] private TMP_InputField ipField; 

    void Awake()
    {
        // find the NetworkManager in the scene if not set in Inspector
        if (!nm) nm = NetworkManager.singleton as GameNetworkManager;
        if (!nm) nm = FindFirstObjectByType<GameNetworkManager>();
        if (!nm) Debug.LogError("[Connect] GameNetworkManager not found in scene.");
    }

    // HOST (LAN): ei Relaytä
    public void HostLAN()
    {
        nm.StartStandardHost(); // tämä asettaa useRelay=false ja käynnistää hostin
    }

    // CLIENT (LAN): ei Relaytä
    public void ClientLAN()
    {
        // Jos syötekenttä puuttuu/tyhjä → oletus localhost (sama kone)
        string ip = (ipField != null && !string.IsNullOrWhiteSpace(ipField.text))
                      ? ipField.text.Trim()
                      : "localhost"; // tai 127.0.0.1

        nm.networkAddress = ip;   // <<< TÄRKEIN KOHTA
        nm.JoinStandardServer();  // useRelay=false ja StartClient()
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
