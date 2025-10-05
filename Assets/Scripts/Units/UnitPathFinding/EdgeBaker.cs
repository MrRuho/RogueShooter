using UnityEngine;

[DefaultExecutionOrder(500)]                       // Aja Pathfindingin jälkeen (jos sillä on negatiivinen order)
[DisallowMultipleComponent]
public class EdgeBaker : MonoBehaviour
{
    public static EdgeBaker Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PathFinding pathfinding;   // Jos jätät tyhjäksi, etsitään automaattisesti
    [SerializeField] private LevelGrid levelGrid;       // Jos jätät tyhjäksi, käytetään LevelGrid.Instance

    [Header("When to run")]
    [SerializeField] private bool autoBakeOnStart = true;

    [Header("Edge scan")]
    [Tooltip("Layerit, jotka edustavat RUUTUJEN VÄLISIÄ, ohuita liikkumista estäviä juttuja (kaiteet, seinäviivat, ovenpielet, tms.)")]
    [SerializeField] private LayerMask edgeBlockerMask;

    [Tooltip("Reunan skannauksen 'nauhan' paksuus suhteessa cellSizeen (0.05-0.2 on tyypillinen).")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float edgeStripThickness = 0.1f;

    [Tooltip("Kuinka korkealta skannataan (metreinä). Yleensä hieman ukkelin pään korkeuden yläpuolelle.")]
    [SerializeField] private float edgeScanHeight = 2.0f;

    // ---- Lyhyet aliasit, ettei tarvitse arvailla mistä mikäkin tulee ----
    private PathFinding PF => pathfinding != null ? pathfinding : (pathfinding = FindFirstObjectByType<PathFinding>());
    private LevelGrid LG => levelGrid != null ? levelGrid : (levelGrid = LevelGrid.Instance);

    private int Width;
    private int Height;
    private int FloorAmount;
    private float CellSize;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (pathfinding == null) pathfinding = FindFirstObjectByType<PathFinding>();
        if (levelGrid == null) levelGrid = LevelGrid.Instance;

        Width = levelGrid.GetWidth();
        Height = levelGrid.GetHeight();
        FloorAmount = levelGrid.GetFloorAmount();
        CellSize = levelGrid.GetCellSize();


    }

    private void Start()
    {
        if (autoBakeOnStart) BakeAllEdges();
    }

    // ------------------------- PUBLIC API -------------------------

    /// <summary>
    /// Pääajon reunoille. Nollaa ensin kaikki seinät ja skannaa N/E/S/W jokaiselle ruudulle.
    /// Aja tämä heti sen jälkeen, kun olet tehnyt ruutujen käveltävyysraycastit (lattia/este).
    /// </summary>
    public void BakeAllEdges()
    {
        if (!Preflight()) return;

        // 1) Tyhjennä vanhat
        for (int f = 0; f < FloorAmount; f++)
        for (int x = 0; x < Width; x++)
        for (int z = 0; z < Height; z++)
        {
            var node = PF.GetNode(x, z, f);
            if (node != null) node.ClearWalls();
        }

        // 2) Skannaa
        for (int f = 0; f < FloorAmount; f++)
        for (int x = 0; x < Width; x++)
        for (int z = 0; z < Height; z++)
        {
            var gp = new GridPosition(x, z, f);
            // Jos itse soluun ei voi astua, reunat harvoin merkityksellisiä – skippaa (halutessasi voit poistaa tämän ehdon)
            if (!IsWalkable(gp)) continue;

            BakeEdgesForCell(gp);
        }
    }

    /// <summary>
    /// Inkrementaalinen päivitys. Kutsu esim. oven avautuessa/sulkeutuessa, tuhoutuvan seinän jälkeen jne.
    /// </summary>
    public void RebakeEdgesAround(GridPosition center, int radius = 1)
    {
        if (!Preflight()) return;

        for (int dx = -radius; dx <= radius; dx++)
        for (int dz = -radius; dz <= radius; dz++)
        {
            var gp = new GridPosition(center.x + dx, center.z + dz, center.floor);
            if (!IsValidGridPosition(gp)) continue;

            var node = PF.GetNode(gp.x, gp.z, gp.floor);
            if (node == null) continue;

            node.ClearWalls();
            BakeEdgesForCell(gp);
        }
    }

    // ------------------------- CORE -------------------------

    private void BakeEdgesForCell(GridPosition gp)
    {
        Vector3 center = LG.GetWorldPosition(gp);
        float s = CellSize;

        // Kapeat “nauhat” reunan päällä. North/South nauha on pitkä Z-suunnassa, East/West pitkä X-suunnassa.
        Vector3 halfNorthSouth = new Vector3((s * edgeStripThickness) * 0.5f, edgeScanHeight * 0.5f, s * 0.45f);
        Vector3 halfEastWest   = new Vector3(s * 0.45f, edgeScanHeight * 0.5f, (s * edgeStripThickness) * 0.5f);

        // Reunojen keskikohdat, nostetaan laatikkoa ylös puoli korkeutta
        float y = edgeScanHeight * 0.5f;
        Vector3 north = center + new Vector3(0f, y, +s * 0.5f);
        Vector3 south = center + new Vector3(0f, y, -s * 0.5f);
        Vector3 east  = center + new Vector3(+s * 0.5f, y, 0f);
        Vector3 west  = center + new Vector3(-s * 0.5f, y, 0f);

        var node = PF.GetNode(gp.x, gp.z, gp.floor);

        // N
        if (HasEdgeBlock(north, halfNorthSouth, Quaternion.identity))
        {
            node.AddWall(EdgeMask.N);
            MarkOpposite(gp, +0, +1, EdgeMask.S);
        }
        // S
        if (HasEdgeBlock(south, halfNorthSouth, Quaternion.identity))
        {
            node.AddWall(EdgeMask.S);
            MarkOpposite(gp, +0, -1, EdgeMask.N);
        }
        // E
        if (HasEdgeBlock(east, halfEastWest, Quaternion.identity))
        {
            node.AddWall(EdgeMask.E);
            MarkOpposite(gp, +1, +0, EdgeMask.W);
        }
        // W
        if (HasEdgeBlock(west, halfEastWest, Quaternion.identity))
        {
            node.AddWall(EdgeMask.W);
            MarkOpposite(gp, -1, +0, EdgeMask.E);
        }
    }

    private bool HasEdgeBlock(Vector3 center, Vector3 halfExtents, Quaternion rot)
    {
        // Nopea ja yksinkertainen. Vaihda halutessasi BoxCast/CapsuleCastiksi.
        return Physics.CheckBox(center, halfExtents, rot, edgeBlockerMask, QueryTriggerInteraction.Ignore);
    }

    private void MarkOpposite(GridPosition a, int dx, int dz, EdgeMask oppositeDir)
    {
        var b = new GridPosition(a.x + dx, a.z + dz, a.floor);
        if (!IsValidGridPosition(b)) return;

        var nb = PF.GetNode(b.x, b.z, b.floor);
        if (nb == null) return;

        nb.AddWall(oppositeDir);
    }

    // ------------------------- HELPERS -------------------------

    private bool Preflight()
    {
        if (PF == null)
        {
            Debug.LogError("[EdgeBaker] Pathfinding reference missing (and not found automatically).");
            return false;
        }
        if (LG == null)
        {
            Debug.LogError("[EdgeBaker] LevelGrid reference missing (and not found automatically).");
            return false;
        }
        return true;
    }

    private bool IsWalkable(GridPosition gp)
    {
        var node = PF.GetNode(gp.x, gp.z, gp.floor);
        return node != null && node.GetIsWalkable();
    }

    private bool IsValidGridPosition(GridPosition gp)
    {
        // Hyödynnetään suoraan LevelGridin GridSystemiä, jos saatavilla
        var gridSystem = LG.GetGridSystem(gp.floor);
        if (gridSystem != null) return gridSystem.IsValidGridPosition(gp);

        // Varalla, jos ei ole getteröitä:
        return gp.x >= 0 && gp.z >= 0 && gp.x < Width && gp.z < Height && gp.floor >= 0 && gp.floor < FloorAmount;
    }
}
