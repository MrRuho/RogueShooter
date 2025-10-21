
using UnityEngine;
using TMPro;
using Mirror;
using Utp;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// This class is responsible for connecting to a game as a host or client.
/// 
/// NOTE: Button callbacks are set in the Unity Inspector.
/// </summary>
public class Connect : MonoBehaviour
{
    [SerializeField] private GameNetworkManager gameNetworkManager; // vedä tämä Inspectorissa
    [SerializeField] private TMP_InputField ipField;
    [SerializeField] private GameModeSelectUI gameModeSelectUI;

    [SerializeField] private GameObject joinInputPanel;   // JoinInputPanel (inactive alussa)
    [SerializeField] private TMP_InputField joinCodeField;
    [SerializeField] private Button joinButton;

    void Awake()
    {
        // find the NetworkManager in the scene if not set in Inspector
        if (!gameNetworkManager) gameNetworkManager = NetworkManager.singleton as GameNetworkManager;
        if (!gameNetworkManager) gameNetworkManager = FindFirstObjectByType<GameNetworkManager>();
        if (!gameNetworkManager) Debug.LogError("[Connect] GameNetworkManager not found in scene.");

        if (joinInputPanel) joinInputPanel.SetActive(false);
        if (joinButton) joinButton.onClick.AddListener(JoinWithFieldValue);
        if (joinCodeField) joinCodeField.onSubmit.AddListener(_ => JoinWithFieldValue());

    }


    public void HostLAN()
    {
        if (!gameNetworkManager)
        {
            gameNetworkManager = NetworkManager.singleton as GameNetworkManager
                            ?? FindFirstObjectByType<GameNetworkManager>();
            if (!gameNetworkManager) { Debug.LogError("[Connect] GameNetworkManager not found."); return; }
        }

        gameNetworkManager.StartStandardHost();

    }


    public void ClientLAN()
    {
        // Jos syötekenttä puuttuu/tyhjä → oletus localhost (sama kone)
        string ip = (ipField != null && !string.IsNullOrWhiteSpace(ipField.text))
                      ? ipField.text.Trim()
                      : "localhost"; // tai 127.0.0.1

        Debug.Log($"[Connect] Joining server at {ip}");
        gameNetworkManager.networkAddress = ip;   // <<< TÄRKEIN KOHTA
        gameNetworkManager.JoinStandardServer();  // useRelay=false ja StartClient()
    }

    public void Host()
    {
        if (!gameNetworkManager)
        {
            gameNetworkManager = NetworkManager.singleton as GameNetworkManager
                            ?? FindFirstObjectByType<GameNetworkManager>();
            if (!gameNetworkManager) { Debug.LogError("[Connect] GameNetworkManager not found."); return; }
        }

        StartCoroutine(StartRelayHostThenShowCode());
    }

    private IEnumerator StartRelayHostThenShowCode()
    {
        if (NetworkServer.active) yield break;

        gameNetworkManager.StartRelayHost(2, null);

        yield return new WaitUntil(() => !string.IsNullOrEmpty(gameNetworkManager.relayJoinCode));
        RelayJoinCodeUI.Instance.ShowCode(gameNetworkManager.relayJoinCode);

        // NetLevelLoader hoitaa kentän latauksen automaattisesti

        yield return new WaitUntil(() => NetworkServer.connections != null &&
                                        NetworkServer.connections.Count >= 2);
        RelayJoinCodeUI.Instance.Hide();
    }


    private IEnumerator StartRelayHostThenLoadLevel()
    {
        if (NetworkServer.active) yield break;

        gameNetworkManager.StartRelayHost(2, null);

        // odota koodi & serveri aktiiviseksi
        yield return new WaitUntil(() => !string.IsNullOrEmpty(gameNetworkManager.relayJoinCode));
        RelayJoinCodeUI.Instance.ShowCode(gameNetworkManager.relayJoinCode);

        yield return new WaitUntil(() => NetworkServer.active);

        // ÄLÄ tee ServerChangeScenea. Varmista additiivinen lataus kuten LAN-case:
        yield return EnsureLevelLoadedAfterServerUp();

        // pidä koodi näkyvissä kunnes 2. pelaaja on mukana (host + 1 client)
        yield return new WaitUntil(() => NetworkServer.connections != null &&
                                        NetworkServer.connections.Count >= 2);
        RelayJoinCodeUI.Instance.Hide();
    }
    
    private IEnumerator EnsureLevelLoadedAfterServerUp()
    {
        // odota hetki, että NetLevelLoader ehtii startata
        yield return new WaitUntil(() => NetworkServer.active);
        yield return null;

        // jos NetLevelLoader ei ole vielä ehtinyt merkitä leveliä valmiiksi,
        // pyydä se lataamaan nykyinen/defu-level
        if (!LevelLoader.IsServerLevelReady)
        {
            string target = LevelLoader.Instance
                ? (LevelLoader.Instance.CurrentLevel ?? LevelLoader.Instance.DefaultLevel)
                : "Level 0";

            if (NetLevelLoader.Instance)
                NetLevelLoader.Instance.ServerLoadLevel(target);
            else
                Debug.LogError("[Connect] NetLevelLoader.Instance puuttuu Core-scenestä!");
        }
    }

    public void Client()
    {

        if (!gameNetworkManager)
        {
            gameNetworkManager = NetworkManager.singleton as GameNetworkManager
                               ?? FindFirstObjectByType<GameNetworkManager>();
            if (!gameNetworkManager)
            {
                Debug.LogError("[Connect] GameNetworkManager not found.");
                return;
            }
        }

        ShowJoinPanel();

    }

    // Join-nappi (tai Enter) — lukee kentän, asettaa koodin ja liittyy
    private void JoinWithFieldValue()
    {
        if (!gameNetworkManager)
        {
            Debug.LogError("[Connect] GameNetworkManager not set.");
            return;
        }

        string code = (joinCodeField ? joinCodeField.text : "").Trim().ToUpperInvariant();

        // kevyt validointi: 6 merkkiä, a–z/0–9 (muuta jos tarvitset)
        if (string.IsNullOrEmpty(code) || code.Length != 6)
        {
            Debug.LogWarning("[Connect] Join code missing/invalid.");
            return;
        }

        gameNetworkManager.relayJoinCode = code;
        gameNetworkManager.JoinRelayServer();
    }

    // Cancel tai Back
    private void HideJoinPanel()
    {
        if (joinInputPanel) joinInputPanel.SetActive(false);
        if (joinCodeField) { joinCodeField.text = ""; joinCodeField.DeactivateInputField(); }
    }

    private void ShowJoinPanel()
    {
        if (joinInputPanel) joinInputPanel.SetActive(true);
        if (joinCodeField)
        {
            // (valinnainen) esitäyttö leikepöydästä, jos näyttää koodilta
            var clip = GUIUtility.systemCopyBuffer?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(clip) && clip.Length == 6)
                joinCodeField.text = clip;

            joinCodeField.ActivateInputField();
            joinCodeField.caretPosition = joinCodeField.text.Length;
        }
    }

    /// <summary>
    /// Starts a LAN host and loads the current scene for all clients.
    /// </summary>
    public void LoadSceneToAllHostLAN()
    {
        gameNetworkManager.StartStandardHost();
        var sceneName = SceneManager.GetActiveScene().name;
        NetworkManager.singleton.ServerChangeScene(sceneName);
    }

    /// <summary>
    /// Starts a relay host and loads the current scene for all clients.
    /// </summary>
    public void LoadSceneToAllHost()
    {
        StartCoroutine(StartRelayHostThenChangeScene());
    }

    private IEnumerator StartRelayHostThenChangeScene()
    {

        if (NetworkServer.active) yield break;


        gameNetworkManager.StartRelayHost(2, null);

        // 1) Odota kunnes OIKEA relay-join-koodi on valmis
        yield return new WaitUntil(() => !string.IsNullOrEmpty(gameNetworkManager.relayJoinCode));
        RelayJoinCodeUI.Instance.ShowCode(gameNetworkManager.relayJoinCode);

        // 2) Odota kunnes serveri on aktiivinen
        yield return new WaitUntil(() => NetworkServer.active);

        // 2b) (Tarvitsetko varmasti scene-reloadin? Jos et, KOMMENTOI tämä pois.)
       // NetworkManager.singleton.ServerChangeScene(
        //    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
       // );

        // 3) Pidä koodi näkyvissä kunnes 2. pelaaja on mukana (host + 1 client)
        yield return new WaitUntil(() =>
            NetworkServer.connections != null && NetworkServer.connections.Count >= 2);

            RelayJoinCodeUI.Instance.Hide();
    }
}
