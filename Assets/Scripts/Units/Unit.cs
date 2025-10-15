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
public class Unit : NetworkBehaviour
{
    [SerializeField] private int newCoverBonusHalf = 3;
    [SerializeField] private int newCoverBonusFull = 6;
    private const int ACTION_POINTS_MAX = 2;

    [SyncVar] public uint OwnerId;

    // --- Cover state ---
    private int personalCover;
    private int personalCoverMax;
    private int thisTurnStartingCover;
    [SyncVar] private bool underFire = false;
    public bool IsUnderFire => underFire;

    // Valinnainen: UI:lle
    public event Action<int, int> OnCoverPoolChanged;

    // Skillit:
    // [SerializeField] private UnitSkills skills; // sisältää CoverAbilityn tason tms.
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
    }

    private void Start()
    {

        if (archetype != null)
        {
            personalCoverMax = archetype.personalCoverMax;
        }
        personalCover = personalCoverMax;

        // kerro UI:lle heti
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);

        if (LevelGrid.Instance != null)
        {
            gridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
            LevelGrid.Instance.AddUnitAtGridPosition(gridPosition, this);
        }

        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;

        healthSystem.OnDead += HealthSystem_OnDead;

        OnAnyUnitSpawned?.Invoke(this, EventArgs.Empty);

        if (archetype != null)
        {
            personalCoverMax = archetype.personalCoverMax;
        }

        // **** Cover Skill *****
        personalCover = personalCoverMax;
        thisTurnStartingCover = personalCover;
        underFire = false;

        //****** Items ******
        grenadePCS = archetype.grenadeCapacity;

    }

    private void OnDisable()
    {
        TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
    }

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
    ///     
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
        thisTurnStartingCover = personalCover;
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


    
    ///****** CoverSystem!************

    public int GetPersonalCover()
    {
        return personalCover;
    }

    public void SetPersonalCover(int value)
    {
        // OFFLINE: ei Mirroria → päivitä suoraan paikallisesti
        if (!NetworkServer.active && !NetworkClient.active)
        {
            ApplyCoverLocal(value);
            return;
        }

        // ONLINE SERVER/HOST: päivitä totuusarvo ja broadcastaa
        if (NetworkServer.active)
        {
            ApplyCoverServer(value);
            return;
        }

        // ONLINE CLIENT: pyydä serveriä asettamaan (EI paikallista asettamista → ei "välähdystä")
        var ni = GetComponent<NetworkIdentity>();
        if (NetworkClient.active && NetworkSyncAgent.Local != null && ni != null)
        {
            NetworkSyncAgent.Local.CmdSetUnitCover(ni.netId, value);
        }
        // ei paikallista muutosta täällä
    }

    private void ApplyCoverLocal(int value)
    {
        personalCover = Mathf.Clamp(value, 0, personalCoverMax);
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax); // UI päivittyy heti
    }

    [Server] // kutsutaan vain serverillä
    private void ApplyCoverServer(int value)
    {
        personalCover = Mathf.Clamp(value, 0, personalCoverMax);
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
        NetworkSync.UpdateCoverUI(this); // server → Rpc → kaikkien UI:t
    }

    public void RegenCoverOnMove(int distance)
    {
        int regenPerTile = archetype != null ? archetype.coverRegenOnMove : 5;
        int tileDelta = distance / 10;
        int coverChange = regenPerTile * tileDelta;
        int newCover = personalCover + coverChange;

        if (newCover <= thisTurnStartingCover)
        {
            newCover = thisTurnStartingCover;
        }

        personalCover = Mathf.Clamp(newCover, 0, personalCoverMax);

        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
    }

    public void RegenCoverBy(int amount)
    {
        int before = personalCover;
        personalCover = Mathf.Clamp(personalCover + amount, 0, personalCoverMax);


        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
    }

    public int GetCoverRegenPerUnusedAP()
    {
        if (!underFire)
        {
            return archetype != null ? archetype.coverRegenPerUnusedAP : 1;
        }
        return 0;
    }

    public int GetPersonalCoverMax() => personalCoverMax;


    public float GetCoverNormalized()
    {
        return (float)personalCover / personalCoverMax;
    }

    public void ApplyNetworkCover(int current, int max)
    {
        personalCoverMax = max;
        personalCover = Mathf.Clamp(current, 0, max);
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
    }

    public void AddPersonalCover(int delta)
    {
        if (delta == 0) return;
        personalCover = Mathf.Clamp(personalCover + delta, 0, personalCoverMax);
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
        NetworkSync.UpdateCoverUI(this); // jos verkossa
    }

    public void SetCoverBonus()
    {
        // Bonusta vain jos EI olla tulen alla
        if (underFire) return;

        // 1) hae nykyinen ruutu
        var gp = GetGridPosition(); // tai: LevelGrid.Instance.GetGridPosition(transform.position)

        // 2) hae node
        var pf = PathFinding.Instance;
        if (pf == null) return;
        var node = pf.GetNode(gp.x, gp.z, gp.floor);
        if (node == null) return;

        // 3) mikä tahansa cover riittää (High > Low > None)
        var t = CoverService.GetNodeAnyCover(node);
        int bonus = t == CoverService.CoverType.High ? newCoverBonusFull :
                    t == CoverService.CoverType.Low ? newCoverBonusHalf : 0;

        if (bonus > 0)
            AddPersonalCover(bonus); // sinulla jo oleva apuri
    }

    // **********************************

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