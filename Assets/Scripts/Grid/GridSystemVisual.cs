using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GridSystemVisual : MonoBehaviour
{
    public static GridSystemVisual Instance { get; private set; }
    [SerializeField] private Transform gridSystemVisualSinglePrefab;

    private GridSystemVisualSingle [,] gridSystemVisualSingleArray;

    private void Awake()
    {

        // Ensure that there is only one instance in the scene
        if (Instance != null)
        {
            Debug.LogError("More than one GridSystemVisual in the scene!" + transform + " " + Instance);
            Destroy(gameObject);
            return;
        }

        Instance  = this;
    }

    private void Start()
    {
        gridSystemVisualSingleArray = new GridSystemVisualSingle[LevelGrid.Instance.GetWidth(), LevelGrid.Instance.GetHeight()];

        for (int x = 0 ;x < LevelGrid.Instance.GetWidth(); x++)
        {
            for (int z = 0; z < LevelGrid.Instance.GetHeight(); z++)
            {
                GridPosition gridPosition = new(x, z);
                Transform gridSystemVisualSingleTransform = Instantiate(gridSystemVisualSinglePrefab, LevelGrid.Instance.GetWorldPosition(gridPosition), Quaternion.identity);

                gridSystemVisualSingleArray[x, z] = gridSystemVisualSingleTransform.GetComponent<GridSystemVisualSingle>();
            }
        }
    }

    private void Update()
    {
        UpdateGridVisuals();
    }

    public void HideAllGridPositions()
    {
        for (int x = 0 ;x < LevelGrid.Instance.GetWidth(); x++)
        {
            for (int z = 0; z < LevelGrid.Instance.GetHeight(); z++)
            {     
                gridSystemVisualSingleArray[x, z].Hide(); 
            }
        }
    }

    public void ShowGridPositionList(List< GridPosition> gridPositionList)
    {
        HideAllGridPositions();
        foreach (GridPosition gridPosition in gridPositionList)
        {
            gridSystemVisualSingleArray[gridPosition.x, gridPosition.z].Show();
        }
    }

    private void UpdateGridVisuals()
    {
        HideAllGridPositions();

        Unit selectedUnit = UnitActionSystem.Instance.GetSelectedUnit();
        if (selectedUnit == null) return;
        
        ShowGridPositionList(
            selectedUnit.GetMoveAction().
            GetValidGridPositionList());
    }
    
}
