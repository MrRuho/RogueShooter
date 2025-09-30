using System;
using UnityEngine;
using Mirror;
public class InteractableItem : NetworkBehaviour, IInteractable
{
    [Header("State")]
    [SyncVar(hook = nameof(OnIsInteractChanged))]
    [SerializeField] private bool isGreen;

    [Header("Visuals")]
    [SerializeField] private Material greenMaterial;
    [SerializeField] private Material redMaterial;
    [SerializeField] private MeshRenderer meshRenderer;

    [Header("Interact")]
    [SerializeField] private float interactDuration = 0.5f;

    private GridPosition gridPosition;
    private Action onInteractComplete;
    private bool isActive;
    private float timer;

    private static bool NetOffline => !NetworkClient.active && !NetworkServer.active;

    void Awake()
    {
        // Pakota alkupose heti oikein (ei välähdyksiä)
        if (!meshRenderer) meshRenderer = GetComponentInChildren<MeshRenderer>();
        SetVisualFromState(isGreen);
    }
    private void Start()
    {
        gridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
        LevelGrid.Instance.SetInteractableAtGridPosition(gridPosition, this);
       // SetColorRed();
    }
    private void Update()
    {
        if (!isActive) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            isActive = false;
            onInteractComplete?.Invoke();
            onInteractComplete = null;
        }
    }

    private void SetColorGreen()
    {
        isGreen = true;
        meshRenderer.material = greenMaterial;
    }

    private void SetColorRed()
    {
        isGreen = false;
        meshRenderer.material = redMaterial;
    }

    public void Interact(Action onInteractComplete)
    {
        this.onInteractComplete = onInteractComplete;
        isActive = true;
        timer = interactDuration;

        if (NetOffline)
        {
            // SINGLEPLAYER: vaihda paikallisesti
            ToggleLocal();
        }
        else if (isServer)
        {
            // HOST / SERVER: vaihda suoraan serverillä
            ToggleServer();
        }
        else
        {
            // PUHDAS CLIENT: pyydä serveriä
            CmdToggleServer();
        }
    }

    private void ToggleLocal()
    {
        isGreen = !isGreen;
        SetVisualFromState(isGreen);
    }

    [Server]
    private void ToggleServer()
    {
        // SERVER: muuta vain tila; visuaali päivittyy hookista kaikkialla
        isGreen = !isGreen;
        SetVisualFromState(isGreen); // valinnainen: tekee serverille välittömän visuaalin ilman uutta SyncVar-kirjoitusta
    }

    [Command(requiresAuthority = false)]
    void CmdToggleServer() => ToggleServer();

    private void OnIsInteractChanged(bool oldValue, bool newVal)
    {
        SetVisualFromState(newVal);
    }

    private void SetVisualFromState(bool state)
    {
        if (!meshRenderer) return;
        meshRenderer.material = state ? greenMaterial : redMaterial;
    }
}
