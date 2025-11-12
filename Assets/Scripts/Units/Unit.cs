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
[RequireComponent(typeof(OverwatchAction))]
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

    private int reactionPoins = 0;

    [SyncVar] public uint OwnerId;

    [SyncVar] private bool underFire = false;
    public bool IsUnderFire => underFire;

    public event Action<int, int> OnCoverPoolChanged;

    [SerializeField] public UnitArchetype archetype;
    [SerializeField] private WeaponDefinition currentWeapon;

    //Events
    public static event EventHandler OnAnyActionPointsChanged;
    public static event EventHandler ActionPointUsed;
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

    private bool _deathHandled;


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
        var hs = GetComponent<HealthSystem>();
        if (hs != null) hs.OnDying += HandleDying_ServerFirst;
    }

    private void OnDisable()
    {
        TurnSystem.Instance.OnTurnStarted -= OnTurnStarted_HandleTurnStarted;
        TurnSystem.Instance.OnTurnEnded -= OnTurnEnded_HandleTurnEnded;

        if (Cover != null) Cover.OnCoverPoolChanged -= ForwardCoverChanged;

        var hs = GetComponent<HealthSystem>();
        if (hs != null) hs.OnDying -= HandleDying_ServerFirst;
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


    [Server]
    public static void RaiseActionPointUsed(Unit unit)
    {
        ActionPointUsed?.Invoke(unit, EventArgs.Empty);
    }

    private void SpendActionPoints(int amount)
    {
        actionPoints -= amount;
        var team = Team;
       // Debug.Log("[Unit] Tiimi: " + Team + "Unitilla actionpisteitä nyt: " + actionPoints);

        // Nosta eventti vain authoritative-puolella:
        if (NetMode.ServerOrOff)
        {
            ActionPointUsed?.Invoke(this, EventArgs.Empty);
            OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
        }

        OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);

        NetworkSync.BroadcastActionPoints(this, actionPoints);
    }
    
   

    public int GetActionPoints()
    {
        return actionPoints;
    }

    public int GetReactionPoints()
    {
      //  if (reactionPoins <= 0) { Debug.Log("[Unit] Tiimi: " + Team + "Unit ei voi ragoida. Reaktiopisteitä: " + reactionPoins); }
        return reactionPoins;
    }
    
    // Käytetään tilanteessa jossa toimitaan toisen vuorolla, kuten Overwach.
    public void SpendReactionPoints()
    {
        if(reactionPoins > 0)
        {
            reactionPoins -= 1; 
        }
    }

    /// <summary>
    ///    Online: Updating ActionPoints usage to otherplayers. 
    /// </summary>
    public void ApplyNetworkActionPoints(int ap)
    {
        if (actionPoints == ap) return;
        actionPoints = ap;
        ActionPointUsed?.Invoke(this, EventArgs.Empty);
        OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsEnemy()
    {
        return isEnemy;
    }

    public bool IsDying()
    {
        return healthSystem != null && healthSystem.IsDying();
    }

    public bool IsDead()
    {
        return healthSystem != null && healthSystem.IsDead();
    }

    private void HealthSystem_OnDead(object sender, System.EventArgs e)
    {
        
        OnAnyUnitDead?.Invoke(this, EventArgs.Empty);
        if (NetworkServer.active)
        {
            RpcBroadcastUnitDead();  // Laukaise event myös clienteilla!
        }

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

    [ClientRpc]
    private void RpcBroadcastUnitDead()
    {
        // Clientilla: laukaise event
        OnAnyUnitDead?.Invoke(this, EventArgs.Empty);
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
        if (IsDying() || IsDead()) return;

        if (!NetworkServer.active && !NetworkClient.active)
        {
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
            if (this == null || IsDying() || IsDead())
            {
                return;
            }
            
            NetworkSyncAgent.Local.CmdSetUnderFire(ni.netId, value);
        }
    }

    [Server]
    public void SetUnderFireServer(bool value)
    {
        if (IsDying() || IsDead()) return;
        
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

    // public void SetPersonalCover(int v) { if (Cover) Cover.SetPersonalCover(v); }
    public void SetPersonalCover(int v)
    {
        if (IsDying() || IsDead()) return;
        if (Cover) Cover.SetPersonalCover(v);
    }

    //public void SetCoverBonus() { if (Cover) Cover.ServerApplyCoverBonus(); }
    public void SetCoverBonus() 
    { 
        if (IsDying() || IsDead()) return;
        if (Cover) Cover.ServerApplyCoverBonus(); 
    }


    public int GetCurrentCoverBonus() => Cover ? Cover.GetCurrentCoverBonus() : 0;
    public void ResetCurrentCoverBonus() { if (Cover) Cover.ResetCurrentCoverBonus(); }

    public void RegenCoverBy(int amount) { if (Cover) Cover.RegenCoverBy(amount); }
    public void RegenCoverOnMove(int distance) { if (Cover) Cover.RegenCoverOnMove(distance); }

    public void ApplyNetworkCover(int cur, int max) { if (Cover) Cover.ApplyNetworkCover(cur, max); }

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
        reactionPoins = 0;
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


        reactionPoins = actionPoints;

        OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
    }

    public int GetTeamID()
    {
        if (NetMode.Offline) // !NetworkServer.active && !NetworkClient.active
        {
            // Offline: käytä isEnemy flagia
            return isEnemy ? 1 : 0;
        }

        // Online: Versus vs Co-op
        var mode = GameModeManager.SelectedMode;
        if (mode == GameMode.Versus)
        {
            // CLIENTILLA: tarkista onko tämä unitti omistettu täällä
            if (NetworkClient.active && !NetworkServer.active)
            {
                var ni = GetComponent<NetworkIdentity>();
                if (ni != null)
                {
                    // Jos tämä on mun unitti, palauta 1 (Client team)
                    // Muuten palauta 0 (Host team)
                    return ni.isOwned ? 1 : 0;
                }
            }

            // SERVERILLÄ/HOSTILLA: käytä OwnerId-tarkistusta
            return NetworkSync.IsOwnerHost(OwnerId) ? 0 : 1;
        }

        // Co-Op / SinglePlayer: pelaajat = 0, viholliset = 1
        return isEnemy ? 1 : 0;
    }

    private void FreezeBeforeDespawn()
    {
        // 1) Estä AE/NetworkAnimator-lähetykset varmasti
        if (TryGetComponent<UnitAnimator>(out var ua))
        {
            var na = ua.GetComponent<NetworkAnimator>();
            if (na != null)
            {
                na.enabled = false;
            }
        }

        // 2) Estä aseiden näkyvyys-Commandit (hae lapsista)
        var vis = GetComponentInChildren<WeaponVisibilitySync>(true); // true jos haluat löytää myös inaktiivit
        if (vis != null)
        {
            vis.enabled = false;
        }

        // 3) Estä myöhästyneet action-päivitykset
        foreach (var ba in GetComponents<BaseAction>())
        {
            ba.enabled = false;
        }

        var lv = GetComponent<LocalVisibility>();
        if (lv == null) lv = gameObject.AddComponent<LocalVisibility>();
        lv.Apply(false);  // disabloi kaikki renderöijät ja canvasit hierarkiasta

        // Piilota maailman-UI heti
        var worldUi = GetComponentInChildren<UnitWorldUI>(true);
        if (worldUi != null) worldUi.SetVisible(false);

        // --- UUTTA: viimeinen varmistus että input/UI vapautuu ---
        UnitActionSystem.Instance.UnlockInput();

    }

    // <<< Kuolemaketjun alku (server) >>>
    /*
    [Server]
    private void HandleDying_ServerFirst(object sender, System.EventArgs e)
    {
        if (!isServer) return; 
        if (_deathHandled) return;
        _deathHandled = true;

        // a) Peilaa jäädytys clientille heti:
        RpcFreezeClientSide();

        // b) Katkaise serverin liike deterministisesti + snap (tyhjentää clientin interp-puskurit)
        DeathStopper.TryHalt(this);

        // c) Sammuta serverillä (nykyinen teidän metodi)
        FreezeBeforeDespawn();
    }
    */
    
    private void HandleDying_ServerFirst(object sender, System.EventArgs e)
    {
        if (_deathHandled) return;
        _deathHandled = true;

        if (NetworkServer.active)
        {
            // online/server-polku
            RpcFreezeClientSide();          // ajetaan vain jos server aktiivinen
            DeathStopper.TryHalt(this);     // server-varmistus liikkeen pysäytykseen
            FreezeBeforeDespawn();          // teidän nykyinen sulku ennen despawnia
        }
        else
        {
            // offline-polku: ei RPC:tä
            DeathStopper.TryHaltOfline(this);
            FreezeBeforeDespawn();
        }
    }

    [ClientRpc]
    void RpcFreezeClientSide()
    {
        // 1) Katkaise clientin actionit
        foreach (var ba in GetComponents<BaseAction>()) ba.enabled = false;

        // 2) Sammuta animaatio- ja ase-synkat
        var ua = GetComponent<UnitAnimator>();
        if (ua)
        {
            var na = ua.GetComponent<NetworkAnimator>();
            if (na) na.enabled = false;
        }
        var anim = GetComponentInChildren<Animator>(true);
        if (anim) anim.enabled = false;

        var vis = GetComponentInChildren<WeaponVisibilitySync>(true);
        if (vis) vis.enabled = false;

        // 3) Piilota KAIKKI renderöijät+canvasit → myös valintarengas
        var lv = GetComponent<LocalVisibility>();
        if (lv == null) lv = gameObject.AddComponent<LocalVisibility>();
        lv.Apply(false);

        // 4) Piilota world-space UI varmuudeksi
        var worldUI = GetComponentInChildren<UnitWorldUI>(true);
        if (worldUI) worldUI.SetVisible(false);

        // 5) Vapauta UI (käytä teidän olemassa olevaa metodia)
        UnitActionSystem.Instance.UnlockInput();

        // -- VAPAUTA KAMERA / INPUT AINA --
        CameraThaw.Thaw("Unit dying (RpcFreezeClientSide)");
    }
}
