using Mirror;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
///     This class represents a unit in the game. 
///     Actions can be called on the unit to perform various actions like moving or shooting.
///     The class inherits from NetworkBehaviour to support multiplayer functionality.
/// </summary>
public enum Team { Player, Enemy }
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(MoveAction))]
[RequireComponent(typeof(TurnTowardsAction))]
[RequireComponent(typeof(CoverSkill))]
public class Unit : NetworkBehaviour
{
    public Team Team;

    [SerializeField] public CoverSkill Cover { get; private set; }

    //  This is off long as this is under dvelopment... 
    //  private const int ACTION_POINTS_MAX = 2;
    //  private int actionPoints = ACTION_POINTS_MAX;

    [SerializeField, Min(0)] private int ACTION_POINTS_MAX = 2;
    private int actionPoints;

    [SyncVar] public uint OwnerId;


    [SyncVar] private bool underFire = false;
    public bool IsUnderFire => underFire;

    public event Action<int, int> OnCoverPoolChanged;

    [SerializeField] public UnitArchetype archetype;
    [SerializeField] private WeaponDefinition currentWeapon;

    //Events
    public static event EventHandler OnAnyActionPointsChanged;
    public static event EventHandler OnAnyUnitSpawned;
    public static event EventHandler OnAnyUnitDead;

    public event Action<bool> OnHiddenChangedEvent;

    [SerializeField] public bool isEnemy;

    private GridPosition gridPosition;
    private HealthSystem healthSystem;

    private BaseAction[] baseActionsArray;

    private int maxMoveDistance;

    [SyncVar(hook = nameof(OnHiddenChanged))]
    private bool isHidden;

    private Renderer[] renderers;
    private Collider[] colliders;
    private Animator anim;

    private int grenadePCS;


    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        TryGetComponent(out anim);

        healthSystem = GetComponent<HealthSystem>();
        baseActionsArray = GetComponents<BaseAction>();
        maxMoveDistance = GetComponent<MoveAction>().GetMaxMoveDistance();
        Cover = GetComponent<CoverSkill>();
    }

    private void Start()
    {
        if (LevelGrid.Instance != null)
        {
            gridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
            LevelGrid.Instance.AddUnitAtGridPosition(gridPosition, this);
        }

        actionPoints = ACTION_POINTS_MAX;

        TurnSystem.Instance.OnTurnStarted += OnTurnStarted_HandleTurnStarted;
        TurnSystem.Instance.OnTurnEnded += OnTurnEnded_HandleTurnEnded;
        healthSystem.OnDead += HealthSystem_OnDead;

        OnAnyUnitSpawned?.Invoke(this, EventArgs.Empty);
        underFire = false;

        //****** Items ******
        grenadePCS = archetype.grenadeCapacity;

    }

    private void OnEnable()
    {
        if (Cover != null) Cover.OnCoverPoolChanged += ForwardCoverChanged;
    }

    private void OnDisable()
    {
        TurnSystem.Instance.OnTurnStarted -= OnTurnStarted_HandleTurnStarted;
        TurnSystem.Instance.OnTurnEnded -= OnTurnEnded_HandleTurnEnded;

        if (Cover != null) Cover.OnCoverPoolChanged -= ForwardCoverChanged;
    }

    private void ForwardCoverChanged(int cur, int max) => OnCoverPoolChanged?.Invoke(cur, max);

    private void Update()
    {
        GridPosition newGridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
        if (newGridPosition != gridPosition)
        {
            GridPosition oldGridposition = gridPosition;
            gridPosition = newGridPosition;
            LevelGrid.Instance.UnitMoveToGridPosition(oldGridposition, newGridPosition, this);
        }
    }

    /// <summary>
    ///     When unit get destroyed, this clears grid system under destroyed unit.  
    /// </summary>
    void OnDestroy()
    {
        if (LevelGrid.Instance != null)
        {
            gridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
            LevelGrid.Instance.RemoveUnitAtGridPosition(gridPosition, this);
        }
    }

    public T GetAction<T>() where T : BaseAction
    {
        foreach (BaseAction baseAction in baseActionsArray)
        {
            if (baseAction is T t)
            {
                return t;
            }
        }
        return null;
    }

    public GridPosition GetGridPosition()
    {
        return gridPosition;
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    public BaseAction[] GetBaseActionsArray()
    {
        return baseActionsArray;
    }

    public bool TrySpendActionPointsToTakeAction(BaseAction baseAction)
    {
        if (CanSpendActionPointsToTakeAction(baseAction))
        {
            SpendActionPoints(baseAction.GetActionPointsCost());
            return true;
        }
        return false;
    }

    public bool CanSpendActionPointsToTakeAction(BaseAction baseAction)
    {
        if (actionPoints >= baseAction.GetActionPointsCost())
        {
            return true;
        }
        return false;
    }

    private void SpendActionPoints(int amount)
    {
        actionPoints -= amount;

        OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
        NetworkSync.BroadcastActionPoints(this, actionPoints);
    }

    public int GetActionPoints()
    {
        return actionPoints;
    }

    /// <summary>
    ///    Online: Updating ActionPoints usage to otherplayers. 
    /// </summary>
    public void ApplyNetworkActionPoints(int ap)
    {
        if (actionPoints == ap) return;
        actionPoints = ap;
        OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsEnemy()
    {
        return isEnemy;
    }

    private void HealthSystem_OnDead(object sender, System.EventArgs e)
    {
        OnAnyUnitDead?.Invoke(this, EventArgs.Empty);
        if (!NetworkServer.active)
        {
            // OFFLINE: suoraan tuho
            if (!NetworkClient.active) { Destroy(gameObject); return; }
            return;
        }

        // Piilota jotta client ehtii kopioida omaan ragdolliin tiedot
        isHidden = true;
        SetSoftHiddenLocal(true);
        StartCoroutine(DestroyAfter(0.30f));
    }

    private IEnumerator DestroyAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        NetworkServer.Destroy(gameObject);
    }

    private void SetSoftHiddenLocal(bool hidden)
    {
        bool visible = !hidden;
        foreach (var r in renderers) if (r) r.enabled = visible;
        foreach (var c in colliders) if (c) c.enabled = visible;
        if (anim) anim.enabled = visible;
    }

    public float GetHealthNormalized()
    {
        return healthSystem.GetHealthNormalized();
    }

    private void OnHiddenChanged(bool oldVal, bool newVal)
    {
        OnHiddenChangedEvent?.Invoke(newVal);
    }

    public bool IsHidden()
    {
        return isHidden;
    }

    public int GetMaxMoveDistance()
    {
        return maxMoveDistance;
    }

    public void SetUnderFire(bool value)
    {
        if (!NetworkServer.active && !NetworkClient.active)
        {
            Debug.Log("Set underfire:" + value);
            underFire = value;
            return;
        }

        if (NetworkServer.active)
        {
            SetUnderFireServer(value);
            return;
        }

        var ni = GetComponent<NetworkIdentity>();
        if (NetworkClient.active && NetworkSyncAgent.Local != null && ni != null)
        {
            NetworkSyncAgent.Local.CmdSetUnderFire(ni.netId, value);
        }
    }

    [Server]
    public void SetUnderFireServer(bool value)
    {
        underFire = value;
        var agent = FindFirstObjectByType<NetworkSyncAgent>();
        if (agent != null)
            agent.ServerBroadcastUnderFire(this, value);
    }

    public void ApplyNetworkUnderFire(bool value)
    {
        underFire = value;
    }

    // ****** Cover Skill ***********
    public int GetPersonalCover() => Cover ? Cover.GetPersonalCover() : 0;
    public int GetPersonalCoverMax() => Cover ? Cover.GetPersonalCoverMax() : 1;
    public float GetCoverNormalized() => Cover ? Cover.GetCoverNormalized() : 0f;
    public int GetCoverRegenPerUnusedAP() => Cover ? Cover.GetCoverRegenPerUnusedAP() : 0;

    public bool HasMoved() => Cover ? Cover.HasMoved() : false;

    public void SetPersonalCover(int v) { if (Cover) Cover.SetPersonalCover(v); }
    //public void SetCoverBonus() { if (Cover) Cover.SetCoverBonus(); }
    public void SetCoverBonus() { if (Cover) Cover.ServerApplyCoverBonus(); }

    public int GetCurrentCoverBonus() => Cover ? Cover.GetCurrentCoverBonus() : 0;
    public void ResetCurrentCoverBonus() { if (Cover) Cover.ResetCurrentCoverBonus(); }

    public void RegenCoverBy(int amount) { if (Cover) Cover.RegenCoverBy(amount); }
    public void RegenCoverOnMove(int distance) { if (Cover) Cover.RegenCoverOnMove(distance); }

    public void ApplyNetworkCover(int cur, int max) { if (Cover) Cover.ApplyNetworkCover(cur, max); }

    // public void AddPersonalCover(int delta) { if (Cover) Cover.AddPersonalCover(delta); }

    //*********************************

    // ***** weapons ******
    public void UseGrenade()
    {
        if (grenadePCS <= 0)
        {
            grenadePCS = 0;
            return;
        }
        grenadePCS -= 1;
    }

    public int GetGrenadePCS() => grenadePCS;

    public WeaponDefinition GetCurrentWeapon()
    {
        return currentWeapon;
    }

    private int _lastApStartTurnId = -1;
    private void OnTurnStarted_HandleTurnStarted(Team startTurnTeam, int turnId)
    {
        if (NetworkClient.active && !NetworkServer.active) return;
        if (Team != startTurnTeam) return;            // vain oman puolen alussa
        if (_lastApStartTurnId == turnId) return;       // duplikaattisuojaksi
        _lastApStartTurnId = turnId;

        actionPoints = ACTION_POINTS_MAX;
        OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);

        // LISÄÄ TÄMÄ: Broadcastaa AP-muutos myös verkossa
        if (NetworkServer.active || NetworkClient.active)
        {
            NetworkSync.BroadcastActionPoints(this, actionPoints);
        }
    }

    private void OnTurnEnded_HandleTurnEnded(Team endTurnTeam, int turnId)
    {
        if (NetworkClient.active && !NetworkServer.active) return;
        if (Team != endTurnTeam) return;            // vain sen puolen lopussa joka oli vuorossa
        int ap = GetActionPoints();
        int per = GetCoverRegenPerUnusedAP();           // palauttaa >0 vain jos ei underFire
        if (ap > 0 && per > 0) Cover.RegenCoverBy(ap * per);  // coverSkill hoitaa clampit jne.
        // (valinnainen) nollaa AP:t heti vuoron päättyessä:
        actionPoints = 0;
        OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
    }
}
