using UnityEngine;
using TMPro;

[DefaultExecutionOrder(-100)]
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
        if (root.activeSelf) root.SetActive(false);
        //Hide();
    }

    // Turvahaku siltä varalta, että Instance ei ole vielä asetettu
    public static RelayJoinCodeUI GetOrFind()
    {
        if (Instance != null) return Instance;
        var found = FindFirstObjectByType<RelayJoinCodeUI>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            if (found.root == null) found.root = found.gameObject;
        }
        return Instance;
    }

    public void ShowCode(string code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        if (codeText) codeText.text = $"JOIN CODE: {c}";
        root.SetActive(true);
    }

    public void Hide() => root.SetActive(false);

}