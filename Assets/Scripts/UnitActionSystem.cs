using Mirror;
using System;
using UnityEngine;

// summary>
// This script handles the unit action system, allowing the player to select a unit and move it to a target position in the game world.
public class UnitActionSystem : MonoBehaviour
{
    public static UnitActionSystem Instance { get; private set; } // Singleton instance of the UnitActionSystem
    public event EventHandler OnSelectedUnitChanged; // Event triggered when the selected unit changes

    // The layer mask to use for detecting units in the game world
    // This allows the script to only interact with objects on the specified layer
    [SerializeField] private LayerMask unitLayerMask;
    [SerializeField] private Unit selectedUnit;

    private void Awake()
    {
        selectedUnit = null;
        // Ensure that there is only one instance of UnitActionSystem in the scene
        if (Instance != null)
        {
            Debug.LogError("UnitActionSystem: More than one UnitActionSystem in the scene!" + transform + " " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this; // Set the singleton instance to this object
    }
    private void Update()
    {
        // Check if the left mouse button is clicked
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
                    selectedUnit.GetMoveAction().Move(mouseGridPosition);
                }
            }
            
        }
    }

    /// Select a unit if the mouse is over it
    /// <returns>True if a unit was selected, false otherwise</returns>
    private bool TryHandleUnitSelection()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, unitLayerMask))
        {
            if (hit.transform.TryGetComponent<Unit>(out Unit unit))
            {
                if(AuthorityHelper.HasLocalControl(unit)) return false;
                SetSelectedUnit(unit); // Set the selected unit to the one that was clicked on
                // If the clicked unit is already selected, deselect it
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
        return selectedUnit; // Return the currently selected unit
    }
}
