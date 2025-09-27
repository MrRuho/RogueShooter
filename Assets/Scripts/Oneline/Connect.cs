using UnityEngine;
using TMPro;
using Mirror;
using Utp;
using UnityEngine.SceneManagement;

/// <summary>
/// This class is responsible for connecting to a game as a host or client.
/// 
/// NOTE: Button callbacks are set in the Unity Inspector.
/// </summary>
public class Connect : MonoBehaviour
{
    [SerializeField] private GameNetworkManager gameNetworkManager; // vedä tämä Inspectorissa
    [SerializeField] private TMP_InputField ipField;

    void Awake()
    {
        // find the NetworkManager in the scene if not set in Inspector
        if (!gameNetworkManager) gameNetworkManager = NetworkManager.singleton as GameNetworkManager;
        if (!gameNetworkManager) gameNetworkManager = FindFirstObjectByType<GameNetworkManager>();
        if (!gameNetworkManager) Debug.LogError("[Connect] GameNetworkManager not found in scene.");
    }


    public void HostLAN()
    {
        // Debug.Log("HostLAN clicked");
        // gameNetworkManager.StartStandardHost(); // tämä asettaa useRelay=false ja käynnistää hostin
        LoadSceneToAllHostLAN();
    }


    public void ClientLAN()
    {
        // Jos syötekenttä puuttuu/tyhjä → oletus localhost (sama kone)
        string ip = (ipField != null && !string.IsNullOrWhiteSpace(ipField.text))
                      ? ipField.text.Trim()
                      : "localhost"; // tai 127.0.0.1

        gameNetworkManager.networkAddress = ip;   // <<< TÄRKEIN KOHTA
        gameNetworkManager.JoinStandardServer();  // useRelay=false ja StartClient()
    }

    public void Host()
    {
        if (!gameNetworkManager)
        {
            Debug.LogError("[Connect] GameNetworkManager not found in scene.");
            return;
        }

        // gameNetworkManager.StartRelayHost(2, null);
        LoadSceneToAllHost();
    }

    public void Client()
    {
        if (!gameNetworkManager)
        {
            Debug.LogError("[Connect] GameNetworkManager not found in scene.");
            return;
        }

        gameNetworkManager.JoinRelayServer();
    }

    public void LoadSceneToAllHostLAN()
    {
        gameNetworkManager.StartStandardHost();       
        var sceneName = SceneManager.GetActiveScene().name;
        NetworkManager.singleton.ServerChangeScene(sceneName);
    }

    public void LoadSceneToAllHost()
    {
        gameNetworkManager.StartRelayHost(2, null);          
        var sceneName = SceneManager.GetActiveScene().name;
        NetworkManager.singleton.ServerChangeScene(sceneName);
    }
}
