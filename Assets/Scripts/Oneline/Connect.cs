
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
        string ip = (ipField != null && !string.IsNullOrWhiteSpace(ipField.text))
                      ? ipField.text.Trim()
                      : "localhost"; // tai 127.0.0.1

        Debug.Log($"[Connect] Joining server at {ip}");
    
        gameNetworkManager.networkAddress = ip;
        // 1) Puhdista clientin oma kenttä ja offline-jäänteet
        StartCoroutine(CleanThenJoin());

    }

    private IEnumerator CleanThenJoin()
    {
        // 1) Puhdista clientin oma kenttä ja offline-jäänteet
        yield return ClientPreJoinCleaner.PrepareForOnlineJoin();
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
        // Odota että serveri on oikeasti ylhäällä
        yield return new WaitUntil(() => NetworkServer.active);
        yield return null; // 1 frame väliin, että Core-komponentit ehtivät herätä

        // Jos taso ei ole vielä valmis → kysy lataus NetLevelLoaderilta
        if (!LevelLoader.IsServerLevelReady)
        {
            // 1) Jos LevelLoader kertoo nykyisen tai oletustason, käytä sitä
            string desired = LevelLoader.Instance
                ? (LevelLoader.Instance.CurrentLevel ?? LevelLoader.Instance.DefaultLevel)
                : null;

            // 2) Muuten pyydä NetLevelLoaderilta sen oletustaso
            if (string.IsNullOrEmpty(desired) && NetLevelLoader.Instance)
                desired = NetLevelLoader.Instance.ResolveDefaultLevelName();

            if (!string.IsNullOrEmpty(desired) && NetLevelLoader.Instance)
            {
                NetLevelLoader.Instance.ServerLoadLevel(desired);
            }
            else
            {
                Debug.LogError("[Connects] Ei pystytty ratkaisemaan ladattavaa leveliä: puuttuuko LevelLoader.DefaultLevel tai NetLevelLoader?");
            }
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
        StartCoroutine(Co_CleanThenJoin(code));
    }

    private IEnumerator Co_CleanThenJoin(string code)
    {
        // 1) Puhdista clientin oma kenttä ja offline-jäänteet
        yield return ClientPreJoinCleaner.PrepareForOnlineJoin();

        // 2) Aseta koodi ja liity
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

        // 3) Pidä koodi näkyvissä kunnes 2. pelaaja on mukana (host + 1 client)
        yield return new WaitUntil(() =>
            NetworkServer.connections != null && NetworkServer.connections.Count >= 2);

            RelayJoinCodeUI.Instance.Hide();
    }
}
