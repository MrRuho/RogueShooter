using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;
using Utp;
using Mirror;

public class RelayJoinCodeUI : MonoBehaviour
{
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