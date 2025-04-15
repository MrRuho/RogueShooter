using UnityEngine;

// This class represents a position on a grid with x and z coordinates.
// It is used to define the position of objects in a grid-based system.
public class GridSystem
{
    private int width;
    private int height;
    private float cellSize;

    private GridObject[,] gridObjectsArray;
    public GridSystem(int width, int height, float cellSize)
    {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;

        gridObjectsArray = new GridObject[width, height];

        for (int x = 0; x< width; x++)
        {
            for(int z = 0; z < height; z++)
            {
                GridPosition gridPosition = new GridPosition(x, z);
                gridObjectsArray[x, z] = new GridObject(this, gridPosition);
            }
        }
    }

/// This method converts grid coordinates (x, z) to world coordinates.
/// It multiplies the grid coordinates by the cell size to get the world position.
    public Vector3 GetWorldPosition(GridPosition gridPosition)
    {
        return new Vector3(gridPosition.x, 0, gridPosition.z )* cellSize;
    }

/// This method converts world coordinates to grid coordinates.
/// It divides the world position by the cell size and rounds it to the nearest integer.
    public GridPosition GetGridPosition(Vector3 worldPosition)
    {
        return new GridPosition( Mathf.RoundToInt(worldPosition.x/cellSize), Mathf.RoundToInt(worldPosition.z/cellSize));
    }

    public void CreateDebugObjects(Transform debugPrefab)
    {
        for (int x = 0; x< width; x++)
        {
            for(int z = 0; z < height; z++)
            {
                GridPosition gridPosition = new GridPosition(x, z);
                Transform debugTransform = GameObject.Instantiate(debugPrefab, GetWorldPosition(gridPosition), Quaternion.identity);
                GridDebugObject gridDebugObject = debugTransform.GetComponent<GridDebugObject>();
                gridDebugObject.SetGridObject(GetGridObject(gridPosition));
            }
        }
    }

    public GridObject GetGridObject(GridPosition gridPosition)
    {
        return gridObjectsArray[gridPosition.x, gridPosition.z];
    }
}
