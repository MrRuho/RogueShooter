using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;
#endif

[ExecuteInEditMode]
public class LevelGridGizmos : MonoBehaviour
{
    [Header("Visualization Settings")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private bool showGridNumbers = false;
    [SerializeField] private bool showEdgeWalls = true;
    [SerializeField] private bool showCoverLines = true;

    [Header("Grid Settings (for Edit Mode)")]
    [SerializeField] private int editorWidth = 10;
    [SerializeField] private int editorHeight = 10;
    [SerializeField] private float editorCellSize = 2f;
    [SerializeField] private int editorFloorAmount = 1;

    [Header("Colors")]
    [SerializeField] private Color gridColor = new Color(0.7f, 0.7f, 0.7f, 0.4f);
    [SerializeField] private Color wallColor = new Color(1f, 0f, 0f, 0.8f);
    [SerializeField] private Color lowCoverColor = new Color(1f, 1f, 0f, 0.6f);
    [SerializeField] private Color highCoverColor = new Color(1f, 0.5f, 0f, 0.8f);

    [Header("Line Thickness")]
    [SerializeField] private float gridLineThickness = 1f;
    [SerializeField] private float wallLineThickness = 3f;
    [SerializeField] private float coverLineThickness = 2f;


    public enum DepthMode { XRay, Occluded, Dual }
    [Header("Depth/Overlay")]
    [SerializeField] private DepthMode depthMode = DepthMode.Occluded;
    [SerializeField] private float gridYOffset = 0.01f; // pieni nosto lattian yläpuolelle

    [Header("References")]
    [SerializeField] private LevelGrid levelGrid;

    private const float WALL_HEIGHT_OFFSET = 0.5f;
    private const float COVER_HEIGHT_OFFSET = 0.3f;

    private void OnValidate()
    {
        if (levelGrid == null)
        {
            levelGrid = GetComponent<LevelGrid>();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGrid && !showEdgeWalls && !showCoverLines) return;

        if (Application.isPlaying && levelGrid != null)
        {
            DrawPlayModeGizmos();
        }
        else if (!Application.isPlaying)
        {
            DrawEditModeGizmos();
        }
    }

    private static Vector3 Centerize(Vector3 corner, float cell)
    => corner + new Vector3(cell * 0.5f, 0f, cell * 0.5f);

    private void DrawPlayModeGizmos()
    {
        if (levelGrid == null) return;

        int width = 0;
        int height = 0;
        int floorAmount = 0;
        float cellSize = 0f;

        try
        {
            width = levelGrid.GetWidth();
            height = levelGrid.GetHeight();
            floorAmount = levelGrid.GetFloorAmount();
            cellSize = levelGrid.GetCellSize();
        }
        catch
        {
            return;
        }

        if (width <= 0 || height <= 0 || cellSize <= 0) return;

        for (int floor = 0; floor < floorAmount; floor++)
        {
            if (showGrid)
            {
                DrawGridLines(width, height, cellSize, floor);
            }

            if (showGridNumbers)
            {
                DrawGridNumbers(width, height, floor);
            }

            if (showEdgeWalls || showCoverLines)
            {
                DrawEdgesAndCovers(width, height, floor);
            }
        }
    }

    private void DrawEditModeGizmos()
    {
        if (!showGrid) return;

        for (int floor = 0; floor < editorFloorAmount; floor++)
        {
            DrawGridLines(editorWidth, editorHeight, editorCellSize, floor);

            if (showGridNumbers)
            {
                DrawEditModeGridNumbers(editorWidth, editorHeight, editorCellSize, floor);
            }
        }
    }


    private void DrawGridLines(int width, int height, float cellSize, int floor)
    {
        Gizmos.color = gridColor;
        float floorY = floor * LevelGrid.FLOOR_HEIGHT + gridYOffset; // pieni offset

        float o = 0.5f * cellSize; // siirto että ruudukko alkaa (0,0) vasen/alareunasta

        for (int x = 0; x <= width; x++)
        {
            Vector3 start = new Vector3(x * cellSize - o, floorY, -o);
            Vector3 end = new Vector3(x * cellSize - o, floorY, height * cellSize - o);
            DrawThickLine(start, end, gridLineThickness);
        }

        for (int z = 0; z <= height; z++)
        {
            Vector3 start = new Vector3(-o, floorY, z * cellSize - o);
            Vector3 end = new Vector3(width * cellSize - o, floorY, z * cellSize - o);
            DrawThickLine(start, end, gridLineThickness);
        }
    }

#if UNITY_EDITOR
    // Pieni apu: piirrä viiva depth-asetuksella
    private void DrawWithDepth(System.Action draw)
    {
        var prev = Handles.zTest;
        switch (depthMode)
        {
            case DepthMode.XRay:
                Handles.zTest = CompareFunction.Always;     // aina näkyvissä
                draw();
                break;
            case DepthMode.Occluded:
                Handles.zTest = CompareFunction.LessEqual;  // kunnioittaa syvyyttä
                draw();
                break;
            case DepthMode.Dual:
                // 1) haalea x-ray taustalle
                Handles.zTest = CompareFunction.Always;
                var c = Gizmos.color; var faint = new Color(c.r, c.g, c.b, c.a * 0.25f);
                Handles.DrawBezier(_s, _e, _s, _e, faint, null, _t);
                // 2) täysi viiva vain näkyvissä osissa
                Handles.zTest = CompareFunction.LessEqual;
                draw();
                break;
        }
        Handles.zTest = prev;
    }

    // Väliaikaiset muuttujat Dual-moodin sisäpiirtoa varten
    private Vector3 _s, _e; private float _t;
#endif
    private void DrawThickLine(Vector3 start, Vector3 end, float thickness)
    {
#if UNITY_EDITOR
        _s = start; _e = end; _t = thickness;
        DrawWithDepth(() =>
        {
            Handles.DrawBezier(start, end, start, end, Gizmos.color, null, thickness);
        });
#else
        Gizmos.DrawLine(start, end);
#endif
    }


    private void DrawGridNumbers(int width, int height, int floor)
    {
    #if UNITY_EDITOR
        float cell = levelGrid.GetCellSize();
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            GridPosition gp = new GridPosition(x, z, floor);
            Vector3 worldPosCorner;
            try { worldPosCorner = levelGrid.GetWorldPosition(gp); }
            catch { continue; }

            Vector3 worldPos = Centerize(worldPosCorner, cell);
            worldPos.y += 0.1f;

            var style = new GUIStyle { normal = { textColor = Color.white }, fontSize = 10, alignment = TextAnchor.MiddleCenter };
            Handles.Label(worldPos, $"{x},{z}", style);
        }
    #endif
    }

   private void DrawEditModeGridNumbers(int width, int height, float cellSize, int floor)
    {
    #if UNITY_EDITOR
        float floorY = floor * LevelGrid.FLOOR_HEIGHT;
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            Vector3 worldPos = new Vector3((x + 0.5f) * cellSize, floorY, (z + 0.5f) * cellSize);
            worldPos.y += 0.1f;

            var style = new GUIStyle { normal = { textColor = Color.white }, fontSize = 10, alignment = TextAnchor.MiddleCenter };
            Handles.Label(worldPos, $"{x},{z}", style);
        }
    #endif
    }

    private void DrawEdgesAndCovers(int width, int height, int floor)
    {
        var pathfinding = PathFinding.Instance;
        if (pathfinding == null) return;

        float cell = levelGrid.GetCellSize();

        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            PathNode node;
            try { node = pathfinding.GetNode(x, z, floor); } catch { continue; }
            if (node == null) continue;

            GridPosition gp = new GridPosition(x, z, floor);
            Vector3 corner;
            try { corner = levelGrid.GetWorldPosition(gp); } catch { continue; }

            // *** TÄRKEÄ: käytä ruudun keskikohtaa piirtämisen lähtöpisteenä
            Vector3 center = Centerize(corner, cell);

            if (showEdgeWalls)  DrawEdgeWalls(node, center, cell);
            if (showCoverLines) DrawCoverLines(node, center, cell);
        }
    }

    private void DrawEdgeWalls(PathNode node, Vector3 center, float cellSize)
    {
        Gizmos.color = wallColor;
        float halfCell = cellSize * 0.5f;
        float y = center.y + WALL_HEIGHT_OFFSET;

        if (node.HasWall(EdgeMask.N))
        {
            Vector3 start = new Vector3(center.x - halfCell, y, center.z + halfCell);
            Vector3 end = new Vector3(center.x + halfCell, y, center.z + halfCell);
            DrawThickLine(start, end, wallLineThickness);
        }

        if (node.HasWall(EdgeMask.S))
        {
            Vector3 start = new Vector3(center.x - halfCell, y, center.z - halfCell);
            Vector3 end = new Vector3(center.x + halfCell, y, center.z - halfCell);
            DrawThickLine(start, end, wallLineThickness);
        }

        if (node.HasWall(EdgeMask.E))
        {
            Vector3 start = new Vector3(center.x + halfCell, y, center.z - halfCell);
            Vector3 end = new Vector3(center.x + halfCell, y, center.z + halfCell);
            DrawThickLine(start, end, wallLineThickness);
        }

        if (node.HasWall(EdgeMask.W))
        {
            Vector3 start = new Vector3(center.x - halfCell, y, center.z - halfCell);
            Vector3 end = new Vector3(center.x - halfCell, y, center.z + halfCell);
            DrawThickLine(start, end, wallLineThickness);
        }
    }

    private void DrawCoverLines(PathNode node, Vector3 center, float cellSize)
    {
        float halfCell = cellSize * 0.5f;
        float y = center.y + COVER_HEIGHT_OFFSET;

        if (node.HasHighCover(CoverMask.N))
        {
            Gizmos.color = highCoverColor;
            Vector3 start = new Vector3(center.x - halfCell, y, center.z + halfCell);
            Vector3 end = new Vector3(center.x + halfCell, y, center.z + halfCell);
            DrawThickLine(start, end, coverLineThickness);
        }
        else if (node.HasLowCover(CoverMask.N))
        {
            Gizmos.color = lowCoverColor;
            Vector3 start = new Vector3(center.x - halfCell, y, center.z + halfCell);
            Vector3 end = new Vector3(center.x + halfCell, y, center.z + halfCell);
            DrawThickLine(start, end, coverLineThickness);
        }

        if (node.HasHighCover(CoverMask.S))
        {
            Gizmos.color = highCoverColor;
            Vector3 start = new Vector3(center.x - halfCell, y, center.z - halfCell);
            Vector3 end = new Vector3(center.x + halfCell, y, center.z - halfCell);
            DrawThickLine(start, end, coverLineThickness);
        }
        else if (node.HasLowCover(CoverMask.S))
        {
            Gizmos.color = lowCoverColor;
            Vector3 start = new Vector3(center.x - halfCell, y, center.z - halfCell);
            Vector3 end = new Vector3(center.x + halfCell, y, center.z - halfCell);
            DrawThickLine(start, end, coverLineThickness);
        }

        if (node.HasHighCover(CoverMask.E))
        {
            Gizmos.color = highCoverColor;
            Vector3 start = new Vector3(center.x + halfCell, y, center.z - halfCell);
            Vector3 end = new Vector3(center.x + halfCell, y, center.z + halfCell);
            DrawThickLine(start, end, coverLineThickness);
        }
        else if (node.HasLowCover(CoverMask.E))
        {
            Gizmos.color = lowCoverColor;
            Vector3 start = new Vector3(center.x + halfCell, y, center.z - halfCell);
            Vector3 end = new Vector3(center.x + halfCell, y, center.z + halfCell);
            DrawThickLine(start, end, coverLineThickness);
        }

        if (node.HasHighCover(CoverMask.W))
        {
            Gizmos.color = highCoverColor;
            Vector3 start = new Vector3(center.x - halfCell, y, center.z - halfCell);
            Vector3 end = new Vector3(center.x - halfCell, y, center.z + halfCell);
            DrawThickLine(start, end, coverLineThickness);
        }
        else if (node.HasLowCover(CoverMask.W))
        {
            Gizmos.color = lowCoverColor;
            Vector3 start = new Vector3(center.x - halfCell, y, center.z - halfCell);
            Vector3 end = new Vector3(center.x - halfCell, y, center.z + halfCell);
            DrawThickLine(start, end, coverLineThickness);
        }
    }
}
