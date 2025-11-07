using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;


/// <summary>
/// Base class for all unit actions in the game.
/// This class inherits from NetworkBehaviour and provides common functionality for unit actions.
/// </summary>
[RequireComponent(typeof(Unit))]
public abstract class BaseAction : NetworkBehaviour
{
    public static event EventHandler OnAnyActionStarted;
    public static event EventHandler OnAnyActionCompleted;

    protected Unit unit;
    protected bool isActive;
    protected Action onActionComplete;

    //private HealthSystem ownerHealth;

    protected virtual void Awake()
    {
        /*
        if (ownerHealth == null)
            ownerHealth = GetComponentInParent<HealthSystem>();
        */

        unit = GetComponent<Unit>();
    }
    
    void OnEnable()
    {    
        /*
        if (ownerHealth == null) ownerHealth = GetComponentInParent<HealthSystem>();
        if (ownerHealth != null)
        {
            ownerHealth.OnDying += HandleUnitDying;
            // DEBUG
            Debug.Log($"[BaseAction:{GetType().Name}] Subscribed OnDying for {unit?.name}");
        }
        else
        {
            Debug.LogWarning($"[BaseAction:{GetType().Name}] ownerHealth == null on {gameObject.name}");
        }
        */   
    }

    void OnDisable()
    {
        /*
        if (ownerHealth != null)
            ownerHealth.OnDying -= HandleUnitDying;
        */

    }

    /*
    private void HandleUnitDying(object sender, EventArgs e)
    {
        ForceCompleteNow();
    }

    // DODO Testaa toimiiko tämä AI.n jumiutumisessa.

    protected virtual void ForceCompleteNow()
    {

        if (!isActive) return;
        isActive = false;
        Debug.Log("[BaseAction] Set isActive: " + isActive);
        Action callback = onActionComplete;
        Debug.Log("[BaseAction] onActionComplete: " + onActionComplete);
        Debug.Log("[BaseAction] Action callback: " + callback);
        onActionComplete = null;
        Debug.Log("[BaseAction] onActionComplete: " + onActionComplete);
        StopAllCoroutines();
        Debug.Log("[BaseAction] StopAllCorroutines");

        OnAnyActionCompleted?.Invoke(this, EventArgs.Empty);
    }
    */
    public bool IsActionActive()
    {
        return isActive;
    }

    public static bool AnyActionActive()
    {
        var allActions = FindObjectsByType<BaseAction>(FindObjectsSortMode.None);
        foreach (var action in allActions)
        {
            if (action != null && action.isActive)
            {
                return true;
            }
        }
        return false;
    }

    public static void ForceCompleteAllActiveActions()
    {
        var allActions = FindObjectsByType<BaseAction>(FindObjectsSortMode.None);
        int forcedCount = 0;
        
        foreach (var action in allActions)
        {
            if (action != null && action.isActive)
            {
                Debug.LogWarning($"[BaseAction] Pakkolopetetaan aktiivinen action: {action.GetType().Name} unitilta {action.unit?.name}");
                action.ForceComplete();
                forcedCount++;
            }
        }
        
        if (forcedCount > 0)
        {
            Debug.LogWarning($"[BaseAction] Pakkolopetettiin {forcedCount} actionia");
        }
    }

    public void ForceComplete()
    {
        if (!isActive) return;

        isActive = false;
        
        Action callback = onActionComplete;
        onActionComplete = null;
        
        StopAllCoroutines();
        
        OnAnyActionCompleted?.Invoke(this, EventArgs.Empty);
        
        callback?.Invoke();
    }

    // Defines the action button text for the Unit UI.
    public abstract string GetActionName();

    // Executes the action at the specified grid position and invokes the callback upon completion.
    public abstract void TakeAction(GridPosition gridPosition, Action onActionComplete);

    // Checks if the specified grid position is valid for the action, when mouse is over a grid position.
    public virtual bool IsValidGridPosition(GridPosition gridPosition)
    {   
        List<GridPosition> validGridPositionsList = GetValidGridPositionList();
        return validGridPositionsList.Contains(gridPosition);
    }

    // Returns a list of valid grid positions for the action.
    public abstract List<GridPosition> GetValidGridPositionList();

    // Returns the action points cost for performing the action.
    public virtual int GetActionPointsCost()
    {
        return 1;
    }

    // Called when the action starts, sets the action as active and stores the completion callback.
    // Prevents the player from performing multiple actions at the same time.
    protected void ActionStart(Action onActionComplete)
    {
        // Tarkoituksia perutaan vain pelaajan vuorolla. (Tai vuoron alussa)
        // jotkin actionit jatkuvat vuorojen yli. 
        if(TurnSystem.Instance.IsPlayerTurn())
        {
            CanselAllIntents();
        }

        isActive = true;
        this.onActionComplete = onActionComplete;
        OnAnyActionStarted?.Invoke(this, EventArgs.Empty);
    }

    // Called when the action is completed, sets the action as inactive and invokes the completion callback.
    // Allows the player to perform new actions.
    protected void ActionComplete()
    {
        if (!isActive) { return; }
        
        isActive = false;
        onActionComplete();
        OnAnyActionCompleted?.Invoke(this, EventArgs.Empty);
    }

    public void ResetChostActions()
    {
        ActionComplete();
    }
    
    // Perutaan kaikki Unitin aikomukset jos Unit tekee jotakin muuta.
    private void CanselAllIntents()
    {
        unit.GetComponent<OverwatchAction>().CancelOverwatchIntent();
    }

    public Unit GetUnit()
    {
        return unit;
    }

    public void MakeDamage(int damage, Unit targetUnit)
    {
        if (targetUnit == null || targetUnit.IsDying() || targetUnit.IsDead()) return;
        
        Vector3 attacerPos = unit.GetWorldPosition() + Vector3.up * 1.6f;
        Vector3 targetPos = targetUnit.GetWorldPosition() + Vector3.up * 1.2f;
        Vector3 dir = targetPos - attacerPos;
        
        if (dir.sqrMagnitude < 0.0001f) dir = targetUnit.transform.forward;
        dir.Normalize();

        float backOffset = 0.7f;
        Vector3 hitPosition = targetPos - dir * backOffset;

        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
        hitPosition += side * UnityEngine.Random.Range(-0.1f, 0.1f);

        NetworkSync.ApplyDamageToUnit(targetUnit, damage, hitPosition, this.GetActorId());
    }

    public void ApplyHit(int damage, bool bypassCover, bool coverOnly, Unit targetUnit, bool melee = false)
    {
        if (targetUnit == null || targetUnit.IsDying() || targetUnit.IsDead()) return;
        
        targetUnit.SetUnderFire(true);
        var ct = GetCoverType(targetUnit);

        if (ct == CoverService.CoverType.None && !melee)
        {
            MakeDamage(damage, targetUnit);
            return;
        }

        float mitigate = 1;
        if (targetUnit.GetPersonalCover() > 0)
        {
            mitigate = CoverService.GetCoverMitigationPoints(ct);
        }

        int toCover = Mathf.RoundToInt(damage * mitigate);
        int before = targetUnit.GetPersonalCover();
        int after = before - toCover;
        
        if (melee)
        {
            after -= damage;
        }

        if (after >= 0)
        {
            targetUnit.SetPersonalCover(after);
            NetworkSync.UpdateCoverUI(targetUnit);

            // Vaikka suojaa jäisi niin kriittinen osuma tekee vahinkoa silti.
            if(bypassCover) MakeDamage(damage, targetUnit); 
        }
        else
        {
            targetUnit.SetPersonalCover(0);
            NetworkSync.UpdateCoverUI(targetUnit);

            // Ainoastaan oikeat osumat voivat tehdä vahinkoa.
            if (!coverOnly && !bypassCover) MakeDamage(damage - before, targetUnit);

            // Kriittinen osuma osuu kokonaisuudessaan.
            if(bypassCover) MakeDamage(damage, targetUnit); 
        }
    }

    public CoverService.CoverType GetCoverType(Unit targetUnit)
    {
        var gp = targetUnit.GetGridPosition();
        var node = PathFinding.Instance.GetNode(gp.x, gp.z, gp.floor);
        var ct = CoverService.EvaluateCoverHalfPlane(unit.GetGridPosition(), targetUnit.GetGridPosition(), node);
        return ct;
    }

    public bool RotateTowards(Vector3 targetPosition, float rotSpeedDegPerSec = 720f, float epsilonDeg = 2f)
    {
        Vector3 to = targetPosition - transform.position;
        to.y = 0f;

        if (to.sqrMagnitude < 1e-6f) return true;

        Vector3 dir = to.normalized;
        Quaternion desired = Quaternion.LookRotation(dir, Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, desired, rotSpeedDegPerSec * Time.deltaTime
        );

        return Quaternion.Angle(transform.rotation, desired) <= epsilonDeg;
    }



    // -------------- ENEMY AI ACTIONS -------------

    /// <summary>
    /// ENEMY AI:
    /// Empty ENEMY AI ACTIONS abstract class. 
    /// Every Unit action like MoveAction.cs, ShootAction.cs and so on defines this differently
    /// Contains gridposition and action value
    /// </summary>
    public abstract EnemyAIAction GetEnemyAIAction(GridPosition gridPosition);

    /// <summary>
    /// ENEMY AI:
    /// Making a list all possible actions an enemy Unit can take, and shorting them 
    /// based on highest action value.(Gives the enemy the best outcome) 
    /// The best Action is in the enemyAIActionList[0]
    /// </summary>
    public EnemyAIAction GetBestEnemyAIAction()
    {
        List<EnemyAIAction> enemyAIActionList = new();

        List<GridPosition> validActionGridPositionList = GetValidGridPositionList();


        foreach (GridPosition gridPosition in validActionGridPositionList)
        {
            // All actions have own EnemyAIAction to set griposition and action value.
            EnemyAIAction enemyAIAction = GetEnemyAIAction(gridPosition);
            enemyAIActionList.Add(enemyAIAction);
        }

        if (enemyAIActionList.Count > 0)
        {
            enemyAIActionList.Sort((a, b) => b.actionValue - a.actionValue);
            return enemyAIActionList[0];
        }
        else
        {
            // No possible Enemy AI Actions
            return null;
        }
    }
}
