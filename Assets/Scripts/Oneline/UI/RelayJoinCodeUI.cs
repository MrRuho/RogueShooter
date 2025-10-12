using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;
using Utp;
using Mirror;

public class RelayJoinCodeUI : MonoBehaviour
{
    /*
    [SerializeField] private TMP_Text joinCodeText;   // vedä Inspectorissa
    [SerializeField] private GameObject container;     // valinnainen: paneeli jonka näytät/piilotat

    // Hyväksyy 4–10 merkkiä. Tarvittaessa muuta pituutta.
    static readonly Regex Rx = new Regex(@"join code:\s*([A-Za-z0-9]{4,10})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    void Awake()
    {
        // (Valinnainen) säilyy scene-vaihdon yli, jos käytät ServerChangeScene:
        // DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        Application.logMessageReceived += OnLog;
        // Jos koodi on jo luotu ennen tätä UI:ta (esim. scene reload), yritä lukea se suoraan managerista:
        TryShowExistingCode();
    }

    void OnDisable()
    {
        Application.logMessageReceived -= OnLog;
    }

    void OnLog(string condition, string stackTrace, LogType type)
    {
        var m = Rx.Match(condition);
        if (!m.Success) return;
        Show(m.Groups[1].Value.ToUpperInvariant());
    }

    void TryShowExistingCode()
    {
        // Jos GameNetworkManagerissa on jo koodi tallessa, näytä se heti.
        var gnm = FindFirstObjectByType<GameNetworkManager>();
        if (gnm != null && !string.IsNullOrWhiteSpace(gnm.relayJoinCode))
        {
            Show(gnm.relayJoinCode.Trim().ToUpperInvariant());
        }
    }

    void Show(string code)
    {
        if (joinCodeText != null) joinCodeText.text = $"JOIN CODE: {code}";
        if (container != null) container.SetActive(true);
    }
    */

    public static RelayJoinCodeUI Instance { get; private set; }

    [SerializeField] private GameObject root;      // vedä tähän CodeCanvas TAI paneelin juuri
    [SerializeField] private TMP_Text codeText;    // vedä JoinCodeText

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (root == null) root = gameObject;       // fallback: käytä CodeCanvasia juurena
        DontDestroyOnLoad(root);                   // pysyy scene-vaihdon yli
        Hide();
    }

    public void ShowCode(string code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        if (codeText) codeText.text = $"JOIN CODE: {c}";
        root.SetActive(true);
    }

    public void Hide() => root.SetActive(false);
    
}