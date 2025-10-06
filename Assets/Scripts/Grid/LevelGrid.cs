using System;
using System.Collections.Generic;
using UnityEngine;

/// @file LevelGrid.cs
/// @brief Core grid management system for RogueShooter.
///
/// The LevelGrid defines and manages the tactical grid used by all gameplay systems.
/// It stores spatial occupancy data, translates between world-space and grid-space coordinates,
/// and provides the structural backbone for the pathfinding and edge-baking systems.
///
/// ### Overview
/// Each level in RogueShooter is represented as one or more layered grids (floors).
/// Every grid cell corresponds to a physical area in the game world and may contain
/// references to units, obstacles, or other gameplay entities. The LevelGrid keeps
/// this data synchronized with the actual scene state and provides efficient lookup
/// and update operations.
///
/// ### System integration
/// - **LevelGrid** – Manages spatial layout, unit occupancy, and coordinate conversions.
/// - **EdgeBaker** – Uses LevelGrid data (width, height, cell size, floor count) to detect edge obstacles.
/// - **PathFinding** – Queries LevelGrid to determine walkable areas and world↔grid mapping for A* searches.
///
/// ### Key features
/// - Multi-floor grid architecture with configurable width, height, and cell size.
/// - Fast world↔grid coordinate conversion for unit and object placement.
/// - Real-time occupancy tracking of all units on the grid.
/// - Scene rebuild capability (`RebuildOccupancyFromScene`) for reinitializing unit positions after reload.
/// - Event-driven notifications for unit movement (`onAnyUnitMoveGridPosition`).
///
/// ### Why this exists in RogueShooter
/// - The game’s turn-based, tile-based design requires precise spatial logic independent of Unity’s physics.
/// - Provides a unified “source of truth” for spatial relationships used by both AI and player systems.
/// - Keeps the game’s tactical layer deterministic, debuggable, and efficient.
///
/// In summary, this file defines the foundational grid layer of RogueShooter’s tactical engine,
/// acting as the shared coordinate and occupancy system for all movement, visibility, and interaction logic.

/// <summary>
/// This class is responsible for managing the game's grid system.
/// It keeps track of the units on the grid and their positions.
/// It provides methods to add, remove, and move units on the grid.
/// Note: This class Script Execution Order is set to be executed after UnitManager.cs. High priority.
/// </summary>
public class LevelGrid : MonoBehaviour
{
    public static LevelGrid Instance { get; private set; }

    public const float FLOOR_HEIGHT = 4f;
    public event EventHandler onAnyUnitMoveGridPosition;

    [SerializeField] private Transform debugPrefab;
    // [SerializeField] private bool debugVisible = true;
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private float cellSize;
    [SerializeField] private int floorAmount;

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
            //gridSystem.CreateDebugObjects(debugPrefab);
            gridSystemList.Add(gridSystem); // NullReferenceException: Object reference not set to an instance of an object!

        }
    }

    private void Start()
    {
        PathFinding.Instance.Setup(width, height, cellSize, floorAmount);
    }

    public GridSystem<GridObject> GetGridSystem(int floor)
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
        if (gridPosition.floor < 0 || gridPosition.floor >= floorAmount)
        {
            return false;
        }
        return GetGridSystem(gridPosition.floor).IsValidGridPosition(gridPosition);
    }

    public int GetWidth() => GetGridSystem(0).GetWidth();

    public int GetHeight() => GetGridSystem(0).GetHeight();

    public int GetFloorAmount() => floorAmount;

    public float GetCellSize() => cellSize;

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
                    var gp = new GridPosition(x, z, floor);
                    var gridObj = grid.GetGridObject(gp);
                    gridObj?.GetUnitList()?.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Rebuilds all grid occupancy data by scanning the current scene for active units.
    ///
    /// What it does:
    /// - Clears all existing unit occupancy from the <see cref="LevelGrid"/>.
    /// - Finds every active <see cref="Unit"/> in the scene.
    /// - Converts each unit’s world position into a grid position and re-registers it.
    ///
    /// Why this exists in RogueShooter:
    /// - Used after a scene or level is (re)loaded to ensure that the grid accurately reflects
    ///   the current in-scene unit placements.
    /// - Called by systems like <see cref="GameModeSelectUI"/> and <see cref="ServerBootstrap"/>
    ///   to synchronize game state after spawning or initialization events.
    ///
    /// Implementation notes:
    /// - Intended for runtime reinitialization, not per-frame updates.
    /// - Safe to call at any time; automatically rebuilds the occupancy layer from scratch.
    /// </summary>
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
