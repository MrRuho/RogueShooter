using UnityEngine;
using Mirror;
using System;

public class Door : NetworkBehaviour
{
    [Header("State")]
    [SyncVar(hook = nameof(OnIsOpenChanged))]
    [SerializeField] private bool isOpen = false;   // alkutila scene-objektille

    [SerializeField] string openParam = "IsOpen";
    [SerializeField] float interactDuration = 0.5f;


    private GridPosition gridPosition;
    private Animator animator;

    // Interact-viiveen hallinta (vain kutsujan koneella UI/turn-rytmitystä varten)
    private Action onInteractComplete;
    private bool isActive;
    private float timer;

    private static bool NetOffline => !NetworkClient.active && !NetworkServer.active;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Pakota alkupose heti oikein (ei välähdyksiä)
        animator.SetBool("IsOpen", isOpen);
        animator.Play(isOpen ? "DoorOpen" : "DoorClose", 0, 1f);
        animator.Update(0f);
    }

    private void Start()
    {
        gridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
        LevelGrid.Instance.SetDoorAtGridPosition(gridPosition, this);

        // Alun käveltävyys: serverillä tai täysin offline-tilassa
        if (NetworkServer.active || NetOffline)
            PathFinding.Instance.SetIsWalkableGridPosition(gridPosition, isOpen);
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

    // KUTSUTAAN InteractActionista (sekä offline, host että puhdas client)
    public void Interact(Action onInteractComplete)
    {
        // Gate (estää spämmin)
        if (isActive) return;

        this.onInteractComplete = onInteractComplete;
        isActive = true;
        timer = interactDuration; // haluttu viive actionille

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

    [Command(requiresAuthority = false)]
    private void CmdToggleServer()
    {
        ToggleServer();
    }

    [Server]
    private void ToggleServer()
    {
        isOpen = !isOpen; // Tämä käynnistää hookin kaikilla
        // EI suoraa animator-kutsua täällä; hook hoitaa sen kauniisti
    }

    private void ToggleLocal()
    {
        // Offline-haara: päivitä animaatio ja pathfinding paikallisesti
        isOpen = !isOpen;
        ApplyAnimator(isOpen);
        PathFinding.Instance.SetIsWalkableGridPosition(gridPosition, isOpen);
    }

    // SyncVar hook – ajetaan kaikilla kun isOpen muuttuu serverillä
    private void OnIsOpenChanged(bool oldVal, bool newVal)
    {
        ApplyAnimator(newVal);

        // Pathfinding vain serverillä (tai offline Startissa/ToggleLocalissa)
        if (NetworkServer.active)
            PathFinding.Instance.SetIsWalkableGridPosition(gridPosition, newVal);
    }

    private void ApplyAnimator(bool open) 
    {
        animator.SetBool(openParam, open);
    }

    // Nämä jätetään jos muu koodi tarvitsee suoraviivaisia kutsuja
    public void OpenDoor()
    {
        if (NetOffline || NetworkServer.active)
        {
            isOpen = true; // käynnistää hookin vain serverillä; offline: päivitä itse
            if (NetOffline)
            {
                ApplyAnimator(true);
                PathFinding.Instance.SetIsWalkableGridPosition(gridPosition, true);
            }
        }
    }

    public void CloseDoor()
    {
        if (NetOffline || NetworkServer.active)
        {
            isOpen = false;
            if (NetOffline)
            {
                ApplyAnimator(false);
                PathFinding.Instance.SetIsWalkableGridPosition(gridPosition, false);
            }
        }
    }
}
