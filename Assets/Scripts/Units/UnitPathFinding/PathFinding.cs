using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds a shortest path on a grid between two grid cells using the A* algorithm
/// with 8-directional movement (N, NE, E, SE, S, SW, W, NW).
/// Note: This class Script Execution Order is set to be executed after LevelGrid.cs. High priority.
/// </summary>
public class PathFinding : MonoBehaviour
{
    public static PathFinding Instance { get; private set; }

    /// <summary>
    /// Movement cost for a straight (orthogonal) step.
    /// </summary>
    private const int MOVE_STRAIGHT_COST = 10;

    /// <summary>
    /// Movement cost for a diagonal step.
    /// </summary>
    private const int MOVE_DIAGONAL_COST = 14;

    /// <summary>
    /// (Optional) Prefab used to draw debug visuals for the grid.
    /// </summary>
    [SerializeField] private Transform gridDebugPrefab;

    [SerializeField] private LayerMask obstaclesLayerMask;
    private int width;
    private int height;
    private float cellSize;

    /// <summary>
    /// Logical grid holding <see cref="PathNode"/> objects used by A*.
    /// </summary>
    private GridSystem<PathNode> gridSystem;

    private void Awake()
    {
        // Ensure that there is only one instance in the scene
        if (Instance != null)
        {
            Debug.LogError("PathFinding: More than one PathFinding in the scene!" + transform + " " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Setup(int width, int height, float cellSize)
    {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;

        gridSystem = new GridSystem<PathNode>(width, height, cellSize, 0, LevelGrid.FLOOR_HEIGHT,
            (GridSystem<PathNode> g, GridPosition gridPosition) => new PathNode(gridPosition));

        // NOTE! This is for the testing.
        gridSystem.CreateDebugObjects(gridDebugPrefab);

        // Set grids where is object like wall (Obstacles layer) to notwalkable 
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < width; z++)
            {
                GridPosition gridPosition = new GridPosition(x, z, 0);
                Vector3 wordPosition = LevelGrid.Instance.GetWorldPosition(gridPosition);

                // Raycast shooting start little bit lower so it is not collider to self.
                // Note. This can be fix allso in Unity setup if needed
                float RaycastOffSetDistance = 5f;
                // Raycast max distance, so it ignores roof and doors
                float maxCheckHeight = 5f;

                if (Physics.Raycast(
                         wordPosition + Vector3.down * RaycastOffSetDistance,
                         Vector3.up, maxCheckHeight,
                         obstaclesLayerMask))
                {
                    GetNode(x, z).SetIsWalkable(false);
                }
            }
        }

    }

    /// <summary>
    /// Computes the shortest path from <paramref name="startGridPosition"/> to <paramref name="endGridPosition"/>
    /// using A* search. Allows both orthogonal and diagonal moves.
    /// </summary>
    /// <param name="startGridPosition">Start cell in grid coordinates.</param>
    /// <param name="endGridPosition">Target cell in grid coordinates.</param>
    /// <returns>
    /// Ordered list of grid positions from start to end (inclusive) if a path exists;
    /// otherwise <c>null</c>.
    /// </returns>
    public List<GridPosition> FindPath(GridPosition startGridPosition, GridPosition endGridPosition, out int pathLeght)
    {
        List<PathNode> openList = new();
        List<PathNode> closedList = new();

        PathNode startNode = gridSystem.GetGridObject(startGridPosition);
        PathNode endNode = gridSystem.GetGridObject(endGridPosition);

        openList.Add(startNode);

        // Initialize all nodes with "infinite" g-cost and clear path data.
        for (int x = 0; x < gridSystem.GetWidth(); x++)
        {
            for (int z = 0; z < gridSystem.GetHeight(); z++)
            {
                GridPosition gridPosition = new GridPosition(x, z, 0);
                PathNode pathNode = gridSystem.GetGridObject(gridPosition);

                pathNode.SetGCost(int.MaxValue);
                pathNode.SetHCost(0);
                pathNode.CalculateFCost();
                pathNode.ResetCameFromPathNode();
            }
        }

        // Seed start node.
        startNode.SetGCost(0);
        startNode.SetHCost(CalculeteDistance(startGridPosition, endGridPosition));
        startNode.CalculateFCost();

        // A* loop.
        while (openList.Count > 0)
        {
            PathNode currentNode = GetLowestFCostPathNode(openList);

            // Goal reached: reconstruct and return path.
            if (currentNode == endNode)
            {
                // Prevent Unit to move longer path then pathfinding allows.
                pathLeght = endNode.GetFCost();

                return CalculatePath(endNode);
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            foreach (PathNode neighbourNode in GetNeighbourList(currentNode))
            {
                if (closedList.Contains(neighbourNode))
                {
                    continue;
                }

                // add unwalkable grids like walls, boxs and so on, to the closed list. 
                if (!neighbourNode.GetIsWalkable())
                {
                    closedList.Add(neighbourNode);
                    continue;
                }

                int tentativeGCost =
                    currentNode.GetGCost() + CalculeteDistance(currentNode.GetGridPosition(), neighbourNode.GetGridPosition());

                // Found a cheaper path to neighbour: update its scores and parent.
                if (tentativeGCost < neighbourNode.GetGCost())
                {
                    neighbourNode.SetCameFromPathNode(currentNode);
                    neighbourNode.SetGCost(tentativeGCost);
                    neighbourNode.SetHCost(CalculeteDistance(neighbourNode.GetGridPosition(), endGridPosition));
                    neighbourNode.CalculateFCost();

                    if (!openList.Contains(neighbourNode))
                    {
                        openList.Add(neighbourNode);
                    }
                }
            }
        }

        // No Path found
        pathLeght = 0;
        return null;
    }

    /// <summary>
    /// Heuristic + step cost between two grid positions assuming 8-directional movement:
    /// uses the standard "octile" distance with straight and diagonal step costs.
    /// </summary>
    /// <param name="gridPositionA">First grid position.</param>
    /// <param name="gridPositionB">Second grid position.</param>
    /// <returns>Estimated movement cost from A to B.</returns>
    public int CalculeteDistance(GridPosition gridPositionA, GridPosition gridPositionB)
    {
        GridPosition gridPositionDistance = gridPositionA - gridPositionB;
        int xDistance = Mathf.Abs(gridPositionDistance.x);
        int zDistance = Mathf.Abs(gridPositionDistance.z);
        int remaining = Math.Abs(xDistance - zDistance);
        return MOVE_DIAGONAL_COST * Mathf.Min(xDistance, zDistance) + MOVE_STRAIGHT_COST * remaining;
    }

    /// <summary>
    /// Returns the node with the lowest f-cost from the given list.
    /// </summary>
    /// <param name="pathNodeList">Candidate nodes (typically the open list).</param>
    /// <returns>Node with the smallest f-cost.</returns>
    private PathNode GetLowestFCostPathNode(List<PathNode> pathNodeList)
    {
        PathNode lowestFCostPathNode = pathNodeList[0];
        for (int i = 0; i < pathNodeList.Count; i++)
        {
            if (pathNodeList[i].GetFCost() < lowestFCostPathNode.GetFCost())
            {
                lowestFCostPathNode = pathNodeList[i];
            }
        }
        return lowestFCostPathNode;
    }

    /// <summary>
    /// Gets the path node at grid coordinates (x, z).
    /// </summary>
    private PathNode GetNode(int x, int z)
    {
        return gridSystem.GetGridObject(new GridPosition(x, z, 0));
    }

    /// <summary>
    /// Returns all valid 8-directional neighbours of the given node (clamped to grid bounds).
    /// Order: left (and diagonals), right (and diagonals), then vertical up/down.
    /// </summary>
    /// <param name="currentNode">Node whose neighbours are requested.</param>
    /// <returns>List of neighbouring <see cref="PathNode"/> objects.</returns>
    private List<PathNode> GetNeighbourList(PathNode currentNode)
    {
        List<PathNode> neighbourList = new();

        GridPosition gridPosition = currentNode.GetGridPosition();

        if (gridPosition.x - 1 >= 0)
        {
            // Left
            neighbourList.Add(GetNode(gridPosition.x - 1, gridPosition.z + 0));

            if (gridPosition.z - 1 >= 0)
            {
                // Left Down
                neighbourList.Add(GetNode(gridPosition.x - 1, gridPosition.z - 1));
            }

            if (gridPosition.z + 1 < gridSystem.GetHeight())
            {
                // left Up
                neighbourList.Add(GetNode(gridPosition.x - 1, gridPosition.z + 1));
            }
        }

        if (gridPosition.x + 1 < gridSystem.GetWidth())
        {
            // Right
            neighbourList.Add(GetNode(gridPosition.x + 1, gridPosition.z + 0));

            if (gridPosition.z - 1 >= 0)
            {
                // Right Down
                neighbourList.Add(GetNode(gridPosition.x + 1, gridPosition.z - 1));
            }

            if (gridPosition.z + 1 < gridSystem.GetHeight())
            {
                // Right Up
                neighbourList.Add(GetNode(gridPosition.x + 1, gridPosition.z + 1));
            }
        }

        if (gridPosition.z - 1 >= 0)
        {
            // Down
            neighbourList.Add(GetNode(gridPosition.x - 0, gridPosition.z - 1));
        }
        if (gridPosition.z + 1 < gridSystem.GetHeight())
        {
            // Up
            neighbourList.Add(GetNode(gridPosition.x + 0, gridPosition.z + 1));
        }

        return neighbourList;
    }

    /// <summary>
    /// Reconstructs the path by walking back from <paramref name="endNode"/> via CameFrom pointers,
    /// then converts it into a list of <see cref="GridPosition"/>s from start to end.
    /// </summary>
    /// <param name="endNode">Goal node reached by A*.</param>
    /// <returns>Ordered list of grid positions representing the path.</returns>
    private List<GridPosition> CalculatePath(PathNode endNode)
    {
        List<PathNode> pathNodeList = new List<PathNode>();
        pathNodeList.Add(endNode);
        PathNode currentNode = endNode;
        while (currentNode.GetCameFromPathNode() != null)
        {
            pathNodeList.Add(currentNode.GetCameFromPathNode());
            currentNode = currentNode.GetCameFromPathNode();
        }

        pathNodeList.Reverse();

        List<GridPosition> gridPositionList = new();
        foreach (PathNode pathNode in pathNodeList)
        {
            gridPositionList.Add(pathNode.GetGridPosition());
        }

        return gridPositionList;
    }

    public bool IsWalkableGridPosition(GridPosition gridPosition)
    {
        return gridSystem.GetGridObject(gridPosition).GetIsWalkable();
    }

    public void SetIsWalkableGridPosition(GridPosition gridPosition, bool isWalkable)
    {
        gridSystem.GetGridObject(gridPosition).SetIsWalkable(isWalkable);
    }
   
    // Prevent to go grid position where is no path. Like surrounded by unwalkable grids.
    public bool HasPath(GridPosition startGridPosition, GridPosition endGridPosition)
    {
        return FindPath(startGridPosition, endGridPosition, out int pathLeght) != null;
    }

    public int GetPathLeght(GridPosition startGridPosition, GridPosition endGridPosition)
    {
        FindPath(startGridPosition, endGridPosition, out int pathLeght);
        return pathLeght;
    }
}
