using Mirror;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
///     This class represents a unit in the game. 
///     Actions can be called on the unit to perform various actions like moving or shooting.
///     The class inherits from NetworkBehaviour to support multiplayer functionality.
/// </summary>
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(MoveAction))]
[RequireComponent(typeof(TurnTowardsAction))]
[RequireComponent(typeof(CoverSkill))]
public class Unit : NetworkBehaviour
{   
    [SerializeField] public CoverSkill Cover { get; private set; }

    private const int ACTION_POINTS_MAX = 2;

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

    private int actionPoints = ACTION_POINTS_MAX;

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

        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;

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
        TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
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
    ///     This method is called when the turn changes. It resets the action points to the maximum value.
    /// </summary>
    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        actionPoints = ACTION_POINTS_MAX;
       // thisTurnStartingCover = personalCover;
        OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
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

    public void SetUnderFire(bool value) => underFire = value;

    // ****** Cover Skill ***********
    public int  GetPersonalCover() => Cover ? Cover.GetPersonalCover() : 0;
    public int  GetPersonalCoverMax() => Cover ? Cover.GetPersonalCoverMax() : 1;
    public float GetCoverNormalized() => Cover ? Cover.GetCoverNormalized() : 0f;
    public int  GetCoverRegenPerUnusedAP() => Cover ? Cover.GetCoverRegenPerUnusedAP() : 0;

    public void SetPersonalCover(int v) { if (Cover) Cover.SetPersonalCover(v); }
    public void SetCoverBonus() { if (Cover) Cover.SetCoverBonus(); }
    
    public void RegenCoverBy(int amount) { if (Cover) Cover.RegenCoverBy(amount); }
    public void RegenCoverOnMove(int distance){ if (Cover) Cover.RegenCoverOnMove(distance); }

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

}