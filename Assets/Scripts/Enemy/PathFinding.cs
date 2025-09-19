using UnityEngine;

public class PathFinding : MonoBehaviour
{
    [SerializeField] private Transform gridDebugPrefab;
    private int width;
    private int height;
    private float cellSize;

    private GridSystem<PathNode> gridSystem;

    private void Awake()
    {
        gridSystem = new GridSystem<PathNode>(10, 10, 2f,
            (GridSystem<PathNode> g, GridPosition gridPosition) => new PathNode(gridPosition));
        gridSystem.CreateDebugObjects(gridDebugPrefab);
    }


}
