using UnityEngine;

public class UnitActionSystem : MonoBehaviour
{
    [SerializeField] private LayerMask unitLayerMask;
    private Unit selectedUnit;

    private void Update()
    {
        // Check if the left mouse button is clicked
        if (Input.GetMouseButtonDown(0))
        {
            // Check if the mouse is over a unit
            // If so, select the unit and return
            // If not, move the selected unit to the mouse position
            if (UnitSelection()) return;
            if (selectedUnit != null)
            {
                selectedUnit.Move(MouseWorld.GetMouseWorldPosition());
            }
            
        }
    }

    /// Select a unit if the mouse is over it
    /// <returns>True if a unit was selected, false otherwise</returns>
    private bool UnitSelection()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, unitLayerMask))
        {
            if (hit.transform.TryGetComponent<Unit>(out Unit unit))
            {
                selectedUnit = unit;
                return true;
            }
        }
        return false;
    }
}
// This script handles the unit action system, allowing the player to select a unit and move it to a target position in the game world.
// It uses the MouseWorld class to get the mouse position in world coordinates and calls the Move method on the selected unit.
// The script also handles unit selection by checking if the mouse is over a unit when the left mouse button is clicked. If a unit is selected, it updates the selectedUnit variable and returns true. If no unit is selected, it returns false.
// The script uses a LayerMask to filter the raycast to only hit units, ensuring that only units can be selected and moved.