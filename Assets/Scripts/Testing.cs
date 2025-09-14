using UnityEngine;

/// <summary>
/// This class is responsible for testing the grid system and unit actions in the game.
/// It provides functionality to visualize the grid positions and interact with unit actions.
/// </summary>
public class Testing : MonoBehaviour
{
    
    [SerializeField] private Unit unit;
    private void Start()
    {
     
    }

    private void Update()
    {
        /*
        if (Input.GetKeyDown(KeyCode.T))
        {
            GridSystemVisual.Instance.HideAllGridPositions();
            GridSystemVisual.Instance.ShowGridPositionList(
                unit.GetMoveAction().
                GetValidGridPositionList());
        }
        */
    }
}
