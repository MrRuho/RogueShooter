using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Finds a shortest path on a grid between two grid cells using the A* algorithm
/// with 8-directional movement (N, NE, E, SE, S, SW, W, NW).
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

        gridSystem = new GridSystem<PathNode>(10, 10, 2f,
            (GridSystem<PathNode> g, GridPosition gridPosition) => new PathNode(gridPosition));
        gridSystem.CreateDebugObjects(gridDebugPrefab);
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
    public List<GridPosition> FindPath(GridPosition startGridPosition, GridPosition endGridPosition)
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
                GridPosition gridPosition = new GridPosition(x, z);
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
        return gridSystem.GetGridObject(new GridPosition(x, z));
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
}
