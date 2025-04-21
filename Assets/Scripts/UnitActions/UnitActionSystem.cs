using Mirror;
using System;
using UnityEngine;

/// <summary>
///     This script handles the unit action system in the game.
///     It allows the player to select units and perform actions on them, such as moving or spinning.
/// </summary>

public class UnitActionSystem : MonoBehaviour
{
    public static UnitActionSystem Instance { get; private set; }
    public event EventHandler OnSelectedUnitChanged;

    // This allows the script to only interact with objects on the specified layer
    [SerializeField] private LayerMask unitLayerMask;
    [SerializeField] private Unit selectedUnit;

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
    private void Update()
    {
        // // Prevents the player from performing multiple actions at the same time
        if (isBusy) return;

        // Check if the player is trying to select a unit or move the selected unit
        if (Input.GetMouseButtonDown(0))
        {
            // Check if the mouse is over a unit
            // If so, select the unit and return
            // If not, move the selected unit to the mouse position
            if (TryHandleUnitSelection()) return;
            if (selectedUnit != null)
            {
                GridPosition mouseGridPosition = LevelGrid.Instance.GetGridPosition(MouseWorld.GetMouseWorldPosition());
                if( selectedUnit.GetMoveAction().IsValidGridPosition(mouseGridPosition))
                {
                    SetBusy();
                    selectedUnit.GetMoveAction().Move(mouseGridPosition, ClearBusy);
                }
            }      
        }

        if (Input.GetMouseButtonDown(1))
        {
            SetBusy();
            selectedUnit.GetSpinAction().Spin(ClearBusy);
        }
    }

    // Prevents the player from performing multiple actions at the same time
    private void SetBusy()
    {
        isBusy = true;
    }

    private void ClearBusy()
    {
        isBusy = false;
    }

    /// Select a unit if the mouse is over it
    private bool TryHandleUnitSelection()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, unitLayerMask))
        {
            if (hit.transform.TryGetComponent<Unit>(out Unit unit))
            {
                if(AuthorityHelper.HasLocalControl(unit)) return false;
                SetSelectedUnit(unit);
                return true;
            }
        }
        return false;
    }

    private void SetSelectedUnit(Unit unit)
    {
        selectedUnit = unit;
        OnSelectedUnitChanged?.Invoke(this, EventArgs.Empty); // Trigger the event when the selected unit changes
    }

    public Unit GetSelectedUnit()
    {
        return selectedUnit;
    }
}
