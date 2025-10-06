using UnityEngine;

[DefaultExecutionOrder(500)] // After Pathfindingin
[DisallowMultipleComponent]

/// @file EdgeBaker.cs
/// @brief Edge-based obstacle detection and wall baking system for RogueShooter.
///
/// The EdgeBaker scans the environment to detect narrow obstacles (walls, fences, railings, doorframes)
/// between adjacent grid cells and encodes them as edge-wall flags in the pathfinding data.
/// This ensures that unit movement and line-of-sight calculations align precisely with physical geometry.
///
/// ### Overview
/// EdgeBaker operates immediately after walkability baking has been performed by the `PathFinding` system.
/// It iterates through all walkable cells and performs four narrow physics checks (north, east, south, west)
/// to detect thin colliders lying between grid borders. Any detected obstacle is stored as an `EdgeMask`
/// flag on both affected nodes to maintain symmetric connectivity.
///
/// ### System integration
/// - **LevelGrid** – Provides spatial dimensions and world↔grid coordinate mapping for each cell.
/// - **PathFinding** – Supplies the `PathNode` data structure where edge walls are stored and queried.
/// - **EdgeBaker** – Bridges the physical Unity scene and the logical pathfinding layer by detecting edge blockers.
///
/// ### Key features
/// - Detects fine-grained edge blockers that are smaller than a full grid cell.
/// - Writes edge-wall data symmetrically to adjacent nodes (no “one-way walls”).
/// - Supports incremental rebaking after runtime geometry changes (doors opening, walls destroyed).
/// - Uses Physics.CheckBox for reliable thin-edge detection with adjustable thickness and scan height.
/// - Operates deterministically and independently of Unity’s NavMesh system.
///
/// ### Why this exists in RogueShooter
/// - The game’s tactical combat requires accurate cover and movement restrictions based on geometry.
/// - Standard per-cell walkability alone cannot capture small barriers or partial walls.
/// - This system creates a precise “micro-collision” layer between cells, allowing units to interact
///   with the environment in a realistic and strategically meaningful way.
///
/// In summary, this file defines the edge-detection system that enhances the grid-based pathfinding
/// with sub-cell precision, ensuring that RogueShooter’s movement, visibility, and cover mechanics
/// reflect the actual physical layout of each combat environment.

/// <summary>
/// Automatically detects and marks impassable edges between walkable grid cells,
/// based on physical obstacles present in the scene (walls, fences, railings, doorframes, etc.).
/// 
/// This component “bakes” thin collision lines along cell borders using Physics.CheckBox tests,
/// writing wall data directly into the PathFinding grid nodes (via EdgeMask flags).
/// It ensures that movement and line-of-sight calculations align with the actual environment geometry.
/// 
/// Design notes specific to RogueShooter:
/// - Used right after walkability baking to identify fine-grained obstacles between adjacent cells.
/// - Prevents units from moving or shooting through narrow environmental blockers
///   that don’t occupy a full cell (e.g., half-walls, railings, or destroyed doorframes).
/// - Enables more realistic tactical cover and movement logic without relying on Unity’s full NavMesh system.
/// - Automatically rebakes affected areas when dynamic obstacles (like doors or destructible walls) change state.
/// </summary>
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

    [Header("Cover scan")]
    [SerializeField] private LayerMask coverMask;

    [Tooltip("Reunan skannauksen 'nauhan' paksuus suhteessa cellSizeen (0.05-0.2 on tyypillinen).")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float edgeStripThickness = 0.1f;

    [Tooltip("Kuinka korkealta skannataan (metreinä). Yleensä hieman ukkelin pään korkeuden yläpuolelle.")]
    [SerializeField] private float edgeScanHeight = 2.0f;

    [Header("Cover height")]
    [SerializeField] private float lowCoverY = 1.0f;      // ~vyötärö
    [SerializeField] private float highCoverY = 1.6f;     // ~pää/olkapää

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
    /// Performs a full edge bake across the entire grid.
    /// 
    /// Clears all previously marked walls, then scans every walkable cell
    /// in all floors to detect thin obstacles (edges) between neighboring cells.
    /// 
    /// Design notes specific to RogueShooter:
    /// - This is typically called once at level initialization, right after walkability checks.
    /// - It ensures that all cell borders reflect real physical blockers,
    ///   so units cannot move or shoot through walls, fences, or other narrow obstacles.
    /// - Provides the foundation for accurate tactical pathfinding and cover detection.
    /// </summary>
    public void BakeAllEdges()
    {
        if (!Preflight()) return;

        // 1) Clear all existing wall data from every node in every floor
        for (int f = 0; f < FloorAmount; f++)
            for (int x = 0; x < Width; x++)
                for (int z = 0; z < Height; z++)
                {
                    var node = PF.GetNode(x, z, f);
                    if (node != null) node.ClearWalls();
                }

        // 2) Scan each walkable cell and bake its N/E/S/W edge data
        for (int f = 0; f < FloorAmount; f++)
            for (int x = 0; x < Width; x++)
                for (int z = 0; z < Height; z++)
                {
                    var gp = new GridPosition(x, z, f);
                    if (!IsWalkable(gp)) continue;

                    BakeEdgesForCell(gp);
                }
    }

    /// <summary>
    /// Rebuilds edge data locally around a given grid position.
    /// 
    /// Used when the environment changes dynamically — for example,
    /// when a door opens or closes, or when a wall is destroyed.
    /// This function rescans a small area instead of rebaking the entire map,
    /// keeping pathfinding and cover data up to date with minimal performance cost.
    /// 
    /// Design notes specific to RogueShooter:
    /// - Ensures that tactical movement and line-of-sight stay accurate
    ///   after real-time map changes during combat.
    /// - Called automatically by interactive elements like doors or destructible props.
    /// </summary>
    public void RebakeEdgesAround(GridPosition center, int radius = 1)
    {
        if (!Preflight()) return;

        // Loop through a square area centered on the target grid position
        for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                var gp = new GridPosition(center.x + dx, center.z + dz, center.floor);
                if (!IsValidGridPosition(gp)) continue;

                var node = PF.GetNode(gp.x, gp.z, gp.floor);
                if (node == null) continue;

                // 1) Clear old wall data
                node.ClearWalls();

                // 2) Rescan and rebuild edge data for this cell
                BakeEdgesForCell(gp);
            }
    }

    // ------------------------- CORE -------------------------
    /// <summary>
    /// Scans the four borders (N/E/S/W) of a single walkable grid cell and writes edge-wall flags.
    ///
    /// What it does:
    /// - Builds four thin, axis-aligned 3D “strips” (AABBs) that sit exactly on the cell borders.
    /// - Uses Physics.CheckBox to detect narrow blockers (rails, thin walls, door frames) at a chosen height.
    /// - For every detected blocker, sets the matching EdgeMask flag on the current node
    ///   and mirrors the opposite flag on the neighboring node to keep graph connectivity symmetric.
    ///
    /// Why this exists in RogueShooter:
    /// - Our levels contain many obstacles that do NOT fill the whole cell but still block movement/LOS across an edge.
    /// - Baking per-edge blockers yields more faithful tactical movement and cover behavior than cell-only walkability.
    /// - Keeping the data symmetric (both sides of the shared edge agree) avoids pathfinding inconsistencies.
    ///
    /// Implementation notes:
    /// - Each cell does a constant amount of physics work (4 × Physics.CheckBox).
    /// - The strip thickness is a fraction of the cell size (edgeStripThickness), tuned to “catch” thin geometry
    ///   without overlapping neighboring interiors.
    /// - The scan runs at edgeScanHeight (centered at Y = edgeScanHeight * 0.5), typically around head-height,
    ///   so low floor clutter doesn’t cause false positives while walls/rails are still detected.
    /// </summary>
    private void BakeEdgesForCell(GridPosition gp)
    {
        // World-space center of this cell (at floor level)
        Vector3 center = LG.GetWorldPosition(gp);
        float s = CellSize;

        // Define half-extents for the thin scanning strips:
        // - North/South strips are long along Z, thin along X.
        // - East/West strips are long along X, thin along Z.
        // Height half-extent is half of edgeScanHeight (so total box height == edgeScanHeight).
        Vector3 halfNorthSouth = new(s * edgeStripThickness * 0.5f, edgeScanHeight * 0.5f, s * 0.45f);
        Vector3 halfEastWest = new(s * 0.45f, edgeScanHeight * 0.5f, s * edgeStripThickness * 0.5f);

        // Place the four strip centers exactly on the cell borders and lift to mid-scan height.
        float y = edgeScanHeight * 0.5f;
        Vector3 north = center + new Vector3(0f, y, +s * 0.5f);
        Vector3 south = center + new Vector3(0f, y, -s * 0.5f);
        Vector3 east = center + new Vector3(+s * 0.5f, y, 0f);
        Vector3 west = center + new Vector3(-s * 0.5f, y, 0f);

        var node = PF.GetNode(gp.x, gp.z, gp.floor);
        node.ClearCover();

        // Probe NORTH edge; if blocked, mark N on this node and S on the northern neighbor.
        if (HasEdgeBlock(north, halfNorthSouth, Quaternion.identity))
        {
            node.AddWall(EdgeMask.N);
            MarkOpposite(gp, +0, +1, EdgeMask.S);
        }
        // Probe SOUTH edge; mirror to the southern neighbor.
        if (HasEdgeBlock(south, halfNorthSouth, Quaternion.identity))
        {
            node.AddWall(EdgeMask.S);
            MarkOpposite(gp, +0, -1, EdgeMask.N);
        }
        // Probe EAST edge; mirror to the eastern neighbor.
        if (HasEdgeBlock(east, halfEastWest, Quaternion.identity))
        {
            node.AddWall(EdgeMask.E);
            MarkOpposite(gp, +1, +0, EdgeMask.W);
        }
        // Probe WEST edge; mirror to the western neighbor.
        if (HasEdgeBlock(west, halfEastWest, Quaternion.identity))
        {
            node.AddWall(EdgeMask.W);
            MarkOpposite(gp, -1, +0, EdgeMask.E);
        }

        // --- Cover (sama geometria saa olla eri layerillä kuin edgeBlocker) ---
        // Tehdään matala ja korkea testi erikseen: low = vain vyötäröosuma, high = osuu myös pään korkeuteen.
        // Rajataan boksi vain yhdelle Y-korkeudelle (pieni korkeus), ettei pöydän jalat tms. vaikuta.
        Vector3 lowHalfNS = new Vector3(s * edgeStripThickness * 0.5f, 0.1f, s * 0.45f);
        Vector3 lowHalfEW = new Vector3(s * 0.45f, 0.1f, s * edgeStripThickness * 0.5f);
        Vector3 highHalfNS = lowHalfNS;
        Vector3 highHalfEW = lowHalfEW;

        // pisteet cover-korkeuksille
        Vector3 nLow = new Vector3(north.x, lowCoverY, north.z);
        Vector3 nHigh = new Vector3(north.x, highCoverY, north.z);
        Vector3 sLow = new Vector3(south.x, lowCoverY, south.z);
        Vector3 sHigh = new Vector3(south.x, highCoverY, south.z);
        Vector3 eLow = new Vector3(east.x, lowCoverY, east.z);
        Vector3 eHigh = new Vector3(east.x, highCoverY, east.z);
        Vector3 wLow = new Vector3(west.x, lowCoverY, west.z);
        Vector3 wHigh = new Vector3(west.x, highCoverY, west.z);

        // North
        bool nLowHit = Physics.CheckBox(nLow, lowHalfNS, Quaternion.identity, coverMask);
        bool nHighHit = Physics.CheckBox(nHigh, highHalfNS, Quaternion.identity, coverMask);
        if (nHighHit) node.AddHighCover(CoverMask.N);
        else if (nLowHit) node.AddLowCover(CoverMask.N);

        // South
        bool sLowHit = Physics.CheckBox(sLow, lowHalfNS, Quaternion.identity, coverMask);
        bool sHighHit = Physics.CheckBox(sHigh, highHalfNS, Quaternion.identity, coverMask);
        if (sHighHit) node.AddHighCover(CoverMask.S);
        else if (sLowHit) node.AddLowCover(CoverMask.S);

        // East
        bool eLowHit = Physics.CheckBox(eLow, lowHalfEW, Quaternion.identity, coverMask);
        bool eHighHit = Physics.CheckBox(eHigh, highHalfEW, Quaternion.identity, coverMask);
        if (eHighHit) node.AddHighCover(CoverMask.E);
        else if (eLowHit) node.AddLowCover(CoverMask.E);

        // West
        bool wLowHit = Physics.CheckBox(wLow, lowHalfEW, Quaternion.identity, coverMask);
        bool wHighHit = Physics.CheckBox(wHigh, highHalfEW, Quaternion.identity, coverMask);
        if (wHighHit) node.AddHighCover(CoverMask.W);
        else if (wLowHit) node.AddLowCover(CoverMask.W);
    }

    /// <summary>
    /// Checks whether a physical obstacle exists along a specific cell edge.
    ///
    /// Uses Physics.CheckBox with the configured <see cref="edgeBlockerMask"/> to detect
    /// any geometry that should prevent movement or line-of-sight across that border.
    ///
    /// Why this exists in RogueShooter:
    /// - We rely on thin colliders (walls, railings, doorframes) placed between grid cells.
    /// - Detecting those lets the pathfinding system respect scene geometry more accurately
    ///   than simple per-cell walkability checks.
    /// - Called four times per cell (once for each direction) during edge baking.
    ///
    /// Implementation notes:
    /// - Returns true if *any* collider in the given layer mask overlaps the test volume.
    /// - QueryTriggerInteraction.Ignore avoids false positives from trigger colliders.
    /// </summary>
    private bool HasEdgeBlock(Vector3 center, Vector3 halfExtents, Quaternion rot)
    {
        return Physics.CheckBox(center, halfExtents, rot, edgeBlockerMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Mirrors an edge-wall flag to the neighboring grid cell so both sides of the shared border agree.
    ///
    /// What it does:
    /// - Computes the neighbor position by offset (dx, dz) on the same floor.
    /// - If the neighbor node exists, adds the opposite direction wall flag to it.
    ///
    /// Why this exists in RogueShooter:
    /// - Keeps pathfinding data consistent between adjacent nodes.
    /// - Prevents “one-way walls,” where one node thinks the edge is blocked
    ///   but its neighbor does not — a common cause of desyncs in tactical grids.
    ///
    /// Implementation notes:
    /// - This method assumes edge baking is done in grid order, so each pair
    ///   of adjacent cells will eventually synchronize their shared edge data.
    /// </summary>
    private void MarkOpposite(GridPosition a, int dx, int dz, EdgeMask oppositeDir)
    {
        var b = new GridPosition(a.x + dx, a.z + dz, a.floor);
        if (!IsValidGridPosition(b)) return;

        var nb = PF.GetNode(b.x, b.z, b.floor);
        if (nb == null) return;

        // Add the mirrored wall flag to the neighbor node
        nb.AddWall(oppositeDir);
    }

    // ------------------------- HELPERS -------------------------
    /// <summary>
    /// Performs a quick validation before baking begins.
    ///
    /// Checks that references to <see cref="PathFinding"/> and <see cref="LevelGrid"/> are valid,
    /// either through serialized fields or automatic runtime lookup.
    ///
    /// Why this exists in RogueShooter:
    /// - Prevents null-reference errors during scene startup.
    /// - Ensures that the grid and pathfinding systems are fully initialized
    ///   before attempting any edge scanning or node modification.
    ///
    /// Implementation notes:
    /// - Logs descriptive errors to help diagnose missing scene references.
    /// - Returns false if any critical dependency is missing, stopping the bake safely.
    /// </summary>
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

    /// <summary>
    /// Determines whether the specified grid position corresponds to a walkable node.
    ///
    /// Why this exists in RogueShooter:
    /// - Edge baking should only occur on cells that units can actually occupy.
    /// - Avoids unnecessary physics checks for blocked or void cells (improves performance).
    ///
    /// Implementation notes:
    /// - Fetches the node from PathFinding and queries its <c>GetIsWalkable()</c> flag.
    /// </summary>
    private bool IsWalkable(GridPosition gp)
    {
        var node = PF.GetNode(gp.x, gp.z, gp.floor);
        return node != null && node.GetIsWalkable();
    }

    /// <summary>
    /// Validates that a given grid position exists within the bounds of the level grid.
    ///
    /// Why this exists in RogueShooter:
    /// - Edge baking frequently queries neighboring cells (±1 in X/Z).
    /// - Ensures that no out-of-range indices are accessed, preventing runtime errors.
    ///
    /// Implementation notes:
    /// - Uses LevelGrid’s built-in <c>IsValidGridPosition()</c> if available for the current floor.
    /// - Falls back to manual bounds checking if no grid system reference is found.
    /// </summary>
    private bool IsValidGridPosition(GridPosition gp)
    {
        var gridSystem = LG.GetGridSystem(gp.floor);
        if (gridSystem != null) return gridSystem.IsValidGridPosition(gp);

        return gp.x >= 0 && gp.z >= 0 && gp.x < Width && gp.z < Height && gp.floor >= 0 && gp.floor < FloorAmount;
    }
}
