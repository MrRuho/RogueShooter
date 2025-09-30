using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class is responsible for managing the game's grid system.
/// It keeps track of the units on the grid and their positions.
/// It provides methods to add, remove, and move units on the grid.
/// Note: This class Script Execution Order is set to be executed after UnitManager.cs. High priority.
/// </summary>
public class LevelGrid : MonoBehaviour
{
    public static LevelGrid Instance { get; private set; }

    public const float FLOOR_HEIGHT = 3f;
    public event EventHandler onAnyUnitMoveGridPosition;

    [SerializeField] private Transform debugPrefab;
    [SerializeField]private int width;
    [SerializeField]private int height;
    [SerializeField]private float cellSize;
    [SerializeField]private int floorAmount;

    private List<GridSystem<GridObject>> gridSystemList;
    private void Awake()
    {

        // Ensure that there is only one instance in the scene
        if (Instance != null)
        {
            Debug.LogError("LevelGrid: More than one LevelGrid in the scene!" + transform + " " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        gridSystemList = new List<GridSystem<GridObject>>(floorAmount);

        for (int floor = 0; floor < floorAmount; floor++)
        {
            var gridSystem = new GridSystem<GridObject>(
                width, height, cellSize, floor, FLOOR_HEIGHT,
                (GridSystem<GridObject> g, GridPosition gridPosition) => new GridObject(g, gridPosition)
                );
            // gridSystem.CreateDebugObjects(debugPrefab);
            gridSystemList.Add(gridSystem); // NullReferenceException: Object reference not set to an instance of an object!

        }
    }

    private void Start()
    {
         PathFinding.Instance.Setup(width, height, cellSize);
    }


    private GridSystem<GridObject> GetGridSystem(int floor)
    {
        if (floor < 0 || floor >= gridSystemList.Count) { Debug.LogError($"Invalid floor {floor}"); return null; }
        return gridSystemList[floor];
    }

    public int GetFloor(Vector3 worldPosition)
    {
        return Mathf.RoundToInt(worldPosition.y / FLOOR_HEIGHT);
    }

    public void AddUnitAtGridPosition(GridPosition gridPosition, Unit unit)
    {
        GridObject gridObject = GetGridSystem(gridPosition.floor).GetGridObject(gridPosition);
        gridObject.AddUnit(unit);
    }

    public List<Unit> GetUnitListAtGridPosition(GridPosition gridPosition)
    {
        GridObject gridObject = GetGridSystem(gridPosition.floor).GetGridObject(gridPosition);
        if (gridObject != null)
        {
            return gridObject.GetUnitList();
        }
        return null;
    }

    public IInteractable GetInteractableAtGridPosition(GridPosition gridPosition)
    {
        GridObject gridObject = GetGridSystem(gridPosition.floor).GetGridObject(gridPosition);
        if (gridObject != null)
        {
            return gridObject.GetInteractable();
        }
        return null;
    }

     public void SetInteractableAtGridPosition(GridPosition gridPosition, IInteractable interactable)
    {
        GridObject gridObject = GetGridSystem(gridPosition.floor).GetGridObject(gridPosition);
        gridObject?.SetInteractable(interactable);

    }

    public void RemoveUnitAtGridPosition(GridPosition gridPosition, Unit unit)
    {
        GridObject gridObject = GetGridSystem(gridPosition.floor).GetGridObject(gridPosition);
        gridObject.RemoveUnit(unit);
    }

    public void UnitMoveToGridPosition(GridPosition fromGridPosition, GridPosition toGridPosition, Unit unit)
    {
        RemoveUnitAtGridPosition(fromGridPosition, unit);
        AddUnitAtGridPosition(toGridPosition, unit);
        onAnyUnitMoveGridPosition?.Invoke(this, EventArgs.Empty);
    }

    public GridPosition GetGridPosition(Vector3 worldPosition)
    {
        int floor = GetFloor(worldPosition);
        return GetGridSystem(floor).GetGridPosition(worldPosition);
    }

    public Vector3 GetWorldPosition(GridPosition gridPosition)
    {
        return GetGridSystem(gridPosition.floor).GetWorldPosition(gridPosition);
    }

    public bool IsValidGridPosition(GridPosition gridPosition)
    {
        return GetGridSystem(gridPosition.floor).IsValidGridPosition(gridPosition);
    }

    public int GetWidth()
    {
        return GetGridSystem(0).GetWidth();
    }
    public int GetHeight()
    {
        return GetGridSystem(0).GetHeight();
    }

    public bool HasAnyUnitOnGridPosition(GridPosition gridPosition)
    {
        GridObject gridObject = GetGridSystem(gridPosition.floor).GetGridObject(gridPosition);
        return gridObject.HasAnyUnit();
    }

    public Unit GetUnitAtGridPosition(GridPosition gridPosition)
    {
        GridObject gridObject = GetGridSystem(gridPosition.floor).GetGridObject(gridPosition);
        return gridObject.GetUnit();
    }

    public void ClearAllOccupancy()
    {       
        if (gridSystemList == null) return;

        for (int floor = 0; floor < gridSystemList.Count; floor++)
        {
            var grid = gridSystemList[floor];
            if (grid == null) continue;

            for (int x = 0; x < grid.GetWidth(); x++)
            {
                for (int z = 0; z < grid.GetHeight(); z++)
                {
                    var gp = new GridPosition(x, z, floor); // ⬅️ huom: kerros mukaan
                    var gridObj = grid.GetGridObject(gp);
                    gridObj?.GetUnitList()?.Clear();
                }
            }
        }         
    }

    public void RebuildOccupancyFromScene()
    {
        ClearAllOccupancy();
        var units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (var u in units)
        {
            var gp = GetGridPosition(u.transform.position);
            AddUnitAtGridPosition(gp, u);
        }
    }

}
