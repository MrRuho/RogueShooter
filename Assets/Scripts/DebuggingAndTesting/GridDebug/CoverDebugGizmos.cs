using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class CoverDebugGizmos : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PathFinding pathfinding;
    [SerializeField] private LevelGrid levelGrid;

    [Header("Filter")]
    [Tooltip("Piirretään vain tämä kerros (floor).")]
    [SerializeField] private int visibleFloor = 0;
    [SerializeField] private bool drawOnlyVisibleFloor = true;

    [Header("What to draw")]
    [SerializeField] private bool drawWalls = true;
    [SerializeField] private bool drawLowCover = true;
    [SerializeField] private bool drawHighCover = true;

    [Header("Style")]
    [SerializeField] private float yOffset = 0.05f;     // nosta viivaa vähän lattiasta
    [SerializeField] private float edgeInset = 0.48f;   // kuinka lähellä ruudun reunaa (0.5 = ihan reunalla)
    [SerializeField] private float wallThickness = 0.08f;
    [SerializeField] private float coverThickness = 0.05f;
    [SerializeField] private float coverLength = 0.35f; // viivan pituus reunan suuntaisesti

    [Header("Colors")]
    [SerializeField] private Color wallColor = new Color(1f, 0.4f, 0.1f, 0.9f); // oranssi
    [SerializeField] private Color lowColor = new Color(0.2f, 1f, 0.2f, 0.9f); // vihreä
    [SerializeField] private Color highColor = new Color(0.2f, 0.5f, 1f, 0.9f); // sininen

    private PathFinding PF => pathfinding ? pathfinding : (pathfinding = FindFirstObjectByType<PathFinding>());
    private LevelGrid LG => levelGrid ? levelGrid : (levelGrid = LevelGrid.Instance);

    private void OnDrawGizmos()
    {
        if (PF == null || LG == null) return;

        int width = PF.GetWidth();
        int height = PF.GetHeight();
        int floors = LG.GetFloorAmount();
        float s = LG.GetCellSize();

        for (int f = 0; f < floors; f++)
        {
            if (drawOnlyVisibleFloor && f != visibleFloor) continue;

            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                {
                    var node = PF.GetNode(x, z, f);
                    if (node == null) continue;

                    Vector3 c = LG.GetWorldPosition(new GridPosition(x, z, f));
                    c.y += yOffset;

                    // TESTI: piirrä pieni pallo jos ruudulla on coveria
                    if (node.GetHighCoverMask() != 0 || node.GetLowCoverMask() != 0)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawSphere(c + Vector3.up * 0.2f, 0.05f);
                    }

                    // Reunakohdat (keskitettyinä reunoille)
                    Vector3 n = c + new Vector3(0, 0, +s * edgeInset);
                    Vector3 s_ = c + new Vector3(0, 0, -s * edgeInset);
                    Vector3 e = c + new Vector3(+s * edgeInset, 0, 0);
                    Vector3 w = c + new Vector3(-s * edgeInset, 0, 0);

                    // Seinät
                    if (drawWalls)
                    {
                        Gizmos.color = wallColor;
                        if (node.HasWall(EdgeMask.N)) DrawEdgeBar(n, Vector3.right, wallThickness, s * 0.9f);
                        if (node.HasWall(EdgeMask.S)) DrawEdgeBar(s_, Vector3.right, wallThickness, s * 0.9f);
                        if (node.HasWall(EdgeMask.E)) DrawEdgeBar(e, Vector3.forward, wallThickness, s * 0.9f);
                        if (node.HasWall(EdgeMask.W)) DrawEdgeBar(w, Vector3.forward, wallThickness, s * 0.9f);
                    }

                    // Cover (valinnainen: toimii, jos lisäsit CoverMaskin PathNodeen)
                    if (drawLowCover)
                    {
                        Gizmos.color = lowColor;
                        if (node.HasLowCover(CoverMask.N)) DrawEdgeBar(n, Vector3.right, coverThickness, s * coverLength);
                        if (node.HasLowCover(CoverMask.S)) DrawEdgeBar(s_, Vector3.right, coverThickness, s * coverLength);
                        if (node.HasLowCover(CoverMask.E)) DrawEdgeBar(e, Vector3.forward, coverThickness, s * coverLength);
                        if (node.HasLowCover(CoverMask.W)) DrawEdgeBar(w, Vector3.forward, coverThickness, s * coverLength);
                    }

                    if (drawHighCover)
                    {
                        Gizmos.color = highColor;
                        if (node.HasHighCover(CoverMask.N)) DrawEdgeBar(n + Vector3.up * 0.02f, Vector3.right, coverThickness, s * coverLength);
                        if (node.HasHighCover(CoverMask.S)) DrawEdgeBar(s_ + Vector3.up * 0.02f, Vector3.right, coverThickness, s * coverLength);
                        if (node.HasHighCover(CoverMask.E)) DrawEdgeBar(e + Vector3.up * 0.02f, Vector3.forward, coverThickness, s * coverLength);
                        if (node.HasHighCover(CoverMask.W)) DrawEdgeBar(w + Vector3.up * 0.02f, Vector3.forward, coverThickness, s * coverLength);
                    }
                }
        }
    }

    // Piirtää “paksun viivan” reunan suuntaisesti pienenä laatikkona
    private void DrawEdgeBar(Vector3 center, Vector3 along, float thickness, float length)
    {
        // along = joko Vector3.right (itä-länsi) tai Vector3.forward (pohjois-etelä)
        Vector3 size = new Vector3(
            Mathf.Abs(along.x) > 0 ? length : thickness,
            thickness,
            Mathf.Abs(along.z) > 0 ? length : thickness
        );
        Gizmos.DrawCube(center, size);
    }
}
