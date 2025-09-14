using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class is responsible for managing the grid system in the game.
/// It creates a grid of grid objects and provides methods to interact with the grid.
/// </summary>

public class LevelGrid : MonoBehaviour
{
    public static LevelGrid Instance { get; private set; }


    public event EventHandler onAnyUnitMoveGridPosition;
    [SerializeField] private Transform debugPrefab;
    private GridSystem gridSystem;
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

        gridSystem = new GridSystem(10, 10, 2f);
        gridSystem.CreateDebugObjects(debugPrefab);
    }

    public void AddUnitAtGridPosition(GridPosition gridPosition, Unit unit)
    {
        GridObject gridObject = gridSystem.GetGridObject(gridPosition);
        gridObject.AddUnit(unit);
    }

    public List<Unit> GetUnitListAtGridPosition(GridPosition gridPosition)
    {
        GridObject gridObject = gridSystem.GetGridObject(gridPosition);
        if (gridObject != null)
        {
            return gridObject.GetUnitList();
        }
        return null;
    }

    public void RemoveUnitAtGridPosition(GridPosition gridPosition, Unit unit)
    {
        GridObject gridObject = gridSystem.GetGridObject(gridPosition);
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
        return gridSystem.GetGridPosition(worldPosition);
    }

    public Vector3 GetWorldPosition(GridPosition gridPosition)
    {
        return gridSystem.GetWorldPosition(gridPosition);
    }

    public bool IsValidGridPosition(GridPosition gridPosition)
    {
        return gridSystem.IsValidGridPosition(gridPosition);
    }

    public int GetWidth()
    {
        return gridSystem.GetWidth();
    }
    public int GetHeight()
    {
        return gridSystem.GetHeight();
    }

    public bool HasAnyUnitOnGridPosition(GridPosition gridPosition)
    {
        GridObject gridObject = gridSystem.GetGridObject(gridPosition);
        return gridObject.HasAnyUnit();
    }

    public Unit GetUnitAtGridPosition(GridPosition gridPosition)
    {
        GridObject gridObject = gridSystem.GetGridObject(gridPosition);
        return gridObject.GetUnit();
    }

    public void ClearAllOccupancy()
    {
        for (int x = 0; x < gridSystem.GetWidth(); x++)
        {
            for (int z = 0; z < gridSystem.GetHeight(); z++)
            {
                var gridPosition = new GridPosition(x, z);
                var gridObject = gridSystem.GetGridObject(gridPosition);
                var list = gridObject.GetUnitList();
                list?.Clear();
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
