using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// This script handles the unit action system in the game.
/// It allows the player to select units and perform actions on them, such as moving or shooting.
/// It also manages the state of the selected unit and action, and prevents the player from performing multiple actions at the same time.
/// Note: This class Script Execution Order is set to be executed before UnitManager.cs. High priority.
/// </summary>
public class UnitActionSystem : MonoBehaviour
{
    public static UnitActionSystem Instance { get; private set; }

    public event EventHandler OnSelectedUnitChanged;
    public event EventHandler OnSelectedActionChanged;
    public event EventHandler<bool> OnBusyChanged;
    public event EventHandler OnActionStarted;

    // This allows the script to only interact with objects on the specified layer
    [SerializeField] private LayerMask unitLayerMask;
    [SerializeField] private Unit selectedUnit;

    private BaseAction selectedAction;
 
    // Prevents the player from performing multiple actions at the same time
    private bool isBusy;

    private void Awake()
    {
        selectedUnit = null;
        // Ensure that there is only one instance in the scene
        if (Instance != null)
        {
            Debug.LogError("UnitActionSystem: More than one UnitActionSystem in the scene!" + transform + " " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {

    }
    private void Update()
    {
//        Debug.Log(LevelGrid.Instance.GetGridPosition(MouseWorld.GetMouseWorldPosition()));
        // Prevents the player from performing multiple actions at the same time
        if (isBusy) return;

        // if is not the player's turn, ignore input
        if (!TurnSystem.Instance.IsPlayerTurn()) return;

        // Ignore input if the mouse is over a UI element
        if (EventSystem.current.IsPointerOverGameObject()) return;

        // Check if the player is trying to select a unit or move the selected unit
        if (TryHandleUnitSelection()) return;
    
        HandleSelectedAction();
    }

    private void HandleSelectedAction()
    {
        if (selectedUnit == null || selectedAction == null) return;

        if (InputManager.Instance.IsMouseButtonDownThisFrame() && selectedAction is GranadeAction)
        {       
            if (selectedUnit.GetGrenadePCS() <= 0) return;
        }
        
        GridPosition targetGridPosition;

        if (InputManager.Instance.IsMouseButtonDownThisFrame() && selectedAction is ShootAction)
        {
            Ray ray = Camera.main.ScreenPointToRay(InputManager.Instance.GetMouseScreenPosition());
            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, unitLayerMask))
            {
                if (hit.transform.TryGetComponent<Unit>(out Unit unit))
                {
                    if (unit.IsEnemy())
                    {
                        targetGridPosition = unit.GetGridPosition();
                        TryExecuteSelectedAction(targetGridPosition);
                    }
                }
            }
        }
        else if (InputManager.Instance.IsMouseButtonDownThisFrame())
        {
            Vector3 world = MouseWorld.GetPositionOnlyHitVisible();
            targetGridPosition = LevelGrid.Instance.GetGridPosition(world);
            TryExecuteSelectedAction(targetGridPosition);
        }
    }

    private void TryExecuteSelectedAction(GridPosition gp)
    {

        int steps = selectedUnit.GetMaxMoveDistance();
        int moveBudgetCost = PathFinding.CostFromSteps(steps);
        int estCost = PathFinding.Instance.CalculateDistance(selectedUnit.GetGridPosition(), gp);
        if (estCost > moveBudgetCost * 10) return;

        if (!selectedAction.IsValidGridPosition(gp) ||
            !selectedUnit.TrySpendActionPointsToTakeAction(selectedAction)) return;

        SetBusy();
        selectedAction.TakeAction(gp, ClearBusy);
        OnActionStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    //      Prevents the player from performing multiple actions at the same time
    /// </summary>
    private void SetBusy()
    {
        isBusy = true;
        OnBusyChanged?.Invoke(this, isBusy);
    }

    /// <summary>
    ///     This method is called when the action is completed.
    /// </summary>
    private void ClearBusy()
    {
        isBusy = false;
        OnBusyChanged?.Invoke(this, isBusy);
    }

    /// <summary>
    ///     This method is called when the player clicks on a unit in the game world.
    ///     Check if the mouse is over a unit
    ///     If so, select the unit and return
    ///     If not, move the selected unit to the mouse position
    /// </summary>
    private bool TryHandleUnitSelection()
    {
        if (InputManager.Instance.IsMouseButtonDownThisFrame())
        {
            Ray ray = Camera.main.ScreenPointToRay(InputManager.Instance.GetMouseScreenPosition());
            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, unitLayerMask))
            {
                if (hit.transform.TryGetComponent<Unit>(out Unit unit))
                {
                    if (AuthorityHelper.HasLocalControl(unit) || unit == selectedUnit) return false;
                    SetSelectedUnit(unit);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Sets the selected unit and triggers the OnSelectedUnitChanged event.
    ///     By defaults set the selected action to the unit's move action. The most common action.
    /// </summary>
    private void SetSelectedUnit(Unit unit)
    {
        if (unit.IsEnemy())
        {
            if(selectedAction is ShootAction)
            {
                HandleSelectedAction();
            }
            return;
        }
        selectedUnit = unit;
        SetSelectedAction(unit.GetAction<MoveAction>());
        OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Sets the selected action and triggers the OnSelectedActionChanged event.
    ///  </summary>
    public void SetSelectedAction(BaseAction baseAction)
    {
        selectedAction = baseAction;
        OnSelectedActionChanged?.Invoke(this, EventArgs.Empty);
    }

    public Unit GetSelectedUnit()
    {
        return selectedUnit;
    }

    public BaseAction GetSelectedAction()
    {
        return selectedAction;
    }

    public void ResetSelectedAction()
    {
        selectedAction = null;
    }

    public void ResetSelectedUnit()
    {
        selectedUnit = null;
    }
    
    // Lock/Unlock input methods for PlayerController when playing online
    public void LockInput() { if (!isBusy) SetBusy(); }
    public void UnlockInput() { if (isBusy)  ClearBusy(); }
}
