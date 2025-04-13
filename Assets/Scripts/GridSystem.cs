using UnityEngine;

// This class represents a position on a grid with x and z coordinates.
// It is used to define the position of objects in a grid-based system.
public class GridSystem
{
    private int width;
    private int height;
    private float cellSize;
    public GridSystem(int width, int height, float cellSize)
    {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;

        for (int x = 0; x< width; x++)
        {
            for(int z = 0; z < height; z++)
            {
                Debug.DrawLine(GetWorldPosition( x, z), GetWorldPosition( x , z)+ Vector3.right * .2f, Color.white, 1000f);
            }
        }
    }

/// This method converts grid coordinates (x, z) to world coordinates.
/// It multiplies the grid coordinates by the cell size to get the world position.
    public Vector3 GetWorldPosition(int x, int z)
    {
        return new Vector3(x, 0, z )* cellSize;
    }

/// This method converts world coordinates to grid coordinates.
/// It divides the world position by the cell size and rounds it to the nearest integer.
    public GridPosition GetGridPosition(Vector3 worldPosition)
    {
        return new GridPosition( Mathf.RoundToInt(worldPosition.x/cellSize), Mathf.RoundToInt(worldPosition.z/cellSize));
    }
   
}
