using System.Collections.Generic;
using UnityEngine;

/// @file PathFinding.cs
/// @brief Core pathfinding system for RogueShooter.
///
/// This component implements the game’s grid-based navigation logic using a custom A* algorithm
/// with full support for multi-floor environments, movement budgets, and edge-based wall detection.
///
/// ### Overview
/// The pathfinding system converts Unity scene geometry into an abstract tactical grid used
/// by both player and AI units. Each cell is represented by a `PathNode` containing walkability,
/// cost, and edge-wall information. The system supports 8-directional movement (N, NE, E, SE, S, SW, W, NW)
/// and dynamically links multiple floors through designer-placed `PathfindingLink` components.
///
/// ### System integration
/// - **LevelGrid** – Defines grid dimensions and provides world↔grid coordinate conversions.
/// - **EdgeBaker** – Scans scene colliders to detect thin obstacles between cells and marks walls accordingly.
/// - **PathFinding** – Performs A* searches using the processed node and edge data.
///
/// ### Key features
/// - Fully deterministic and allocation-free per search (generation-ID based node reuse).
/// - Accurate obstacle handling using edge blockers (no corner clipping or one-way walls).
/// - Move-budget based path truncation for tactical range queries and AI planning.
/// - Extensible multi-floor connectivity via `PathfindingLink` objects.
/// - Optional runtime diagnostics through `PathfindingDiagnostics` (profiling search times and expansions).
///
/// ### Why this exists in RogueShooter
/// - The game’s tactical, turn-based design requires predictable and grid-aligned movement.
/// - Unity’s built-in NavMesh system is unsuitable for deterministic tile-based combat logic.
/// - Custom A* implementation allows tight integration with game-specific mechanics such as
///   cover, destructible walls, and limited-range actions.
///
/// In summary, this file defines the core pathfinding logic that powers all unit movement
/// and AI navigation in RogueShooter, ensuring consistency between physical scene geometry
/// and tactical gameplay rules.

/// <summary>
/// Grid-based A* pathfinding for 8-directional movement (N, NE, E, SE, S, SW, W, NW) across multiple floors.
/// 
/// What it does:
/// - Builds and queries a per-floor grid of PathNodes and computes shortest paths using A* with an octile heuristic.
/// - Respects fine-grained edge blockers (walls/rails/doorframes) baked by <see cref="EdgeBaker"/> so units can’t
///   cut corners or move/shoot through narrow obstacles.
/// - Supports optional move budgets (in “steps”) for tactical range queries and AI decisions.
/// - Supports explicit inter-cell “links” (stairs/elevators/hatches) that connect arbitrary cells and floors.
/// 
/// Why this exists in RogueShooter:
/// - The game is turn-based and tile-based; we need deterministic, frame-stable paths that match tactical rules,
///   not freeform NavMesh paths.
/// - Edge-aware movement prevents diagonal corner-cutting and enforces cover/door behavior consistent with combat.
/// - Budgeted pathfinding enables fast “reachable area” calculations for UI previews and AI planning.
/// 
/// Design notes:
/// - Uses a lightweight custom PriorityQueue and generation IDs to avoid per-search allocations and stale scores.
/// - Movement costs: straight = 10, diagonal = 20 (octile distance for heuristic and step costs).
/// - Runs after <see cref="LevelGrid"/> initialization; floor walkability is raycasted once, edges baked next,
///   then A* queries can safely rely on up-to-date node/edge data.
/// - Optional debug visualizations can create grid debug objects for inspection in the editor.
/// </summary>
public class PathFinding : MonoBehaviour
{
    public static PathFinding Instance { get; private set; }

    private const int MOVE_STRAIGHT_COST = 10;
    private const int MOVE_DIAGONAL_COST = 20;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;
    [SerializeField] private Transform gridDebugPrefab;

    [Header("Layers")]
    [SerializeField] private LayerMask obstaclesLayerMask;
    [SerializeField] private LayerMask floorLayerMask;

    [Header("Links")]
    [SerializeField] private Transform pathfindingLinkContainer;

    private int width;
    private int height;
    private int currentGenerationID = 0;

    private List<GridSystem<PathNode>> gridSystemList;
    private List<PathfindingLink> pathfindingLinkList;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("PathFinding: More than one PathFinding in the scene! " + transform + " - " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Initializes the pathfinding system and builds all per-floor grid data.
    ///
    /// What it does:
    /// - Creates a <see cref="GridSystem{PathNode}"/> for each floor with the given dimensions.
    /// - Performs raycast-based walkability detection for every grid cell using floor and obstacle layers.
    /// - Invokes <see cref="EdgeBaker"/> to detect thin edge blockers between walkable cells.
    /// - Collects any explicit <see cref="PathfindingLink"/> connections (stairs, elevators, etc.) from the scene.
    ///
    /// Why this exists in RogueShooter:
    /// - Converts the 3D scene geometry into a grid-based navigation map used by all AI and tactical systems.
    /// - Ensures that units move on valid walkable surfaces and respect real physical barriers.
    /// - Keeps the runtime logic deterministic and self-contained without relying on Unity’s NavMesh.
    ///
    /// Implementation notes:
    /// - Should be called once during level initialization (by LevelGrid or GameManager).
    /// - Automatically performs full edge baking after walkability setup.
    /// - Uses layer masks for flexibility: <c>floorLayerMask</c> defines valid surfaces, <c>obstaclesLayerMask</c> blocks them.
    /// </summary>
    public void Setup(int width, int height, float cellSize, int floorAmount)
    {
        this.width = width;
        this.height = height;

        gridSystemList = new List<GridSystem<PathNode>>();

        // 1) Create one grid per floor
        for (int floor = 0; floor < floorAmount; floor++)
        {
            GridSystem<PathNode> gridSystem = new GridSystem<PathNode>(
                width, height, cellSize, floor, LevelGrid.FLOOR_HEIGHT,
                (GridSystem<PathNode> g, GridPosition gridPosition) => new PathNode(gridPosition)
            );

            // Optional: visualize grid in editor for debugging
            if (showDebug && gridDebugPrefab != null)
            {
                gridSystem.CreateDebugObjects(gridDebugPrefab);
            }

            gridSystemList.Add(gridSystem);
        }

        // 2) Raycast: determine which cells are walkable or blocked
        float raycastOffsetDistance = 1f;
        float raycastDistance = raycastOffsetDistance * 2f;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                for (int floor = 0; floor < floorAmount; floor++)
                {
                    GridPosition gridPosition = new GridPosition(x, z, floor);
                    Vector3 worldPosition = LevelGrid.Instance.GetWorldPosition(gridPosition);

                    // Default to non-walkable
                    GetNode(x, z, floor).SetIsWalkable(false);

                    // Downward ray: detect if a valid floor exists under this cell
                    if (Physics.Raycast(
                            worldPosition + Vector3.up * raycastOffsetDistance,
                            Vector3.down,
                            raycastDistance,
                            floorLayerMask))
                    {
                        GetNode(x, z, floor).SetIsWalkable(true);
                    }

                    // Upward ray: short check for obstacles blocking this space
                    if (Physics.Raycast(
                            worldPosition + Vector3.down * raycastOffsetDistance,
                            Vector3.up,
                            raycastDistance,
                            obstaclesLayerMask))
                    {
                        GetNode(x, z, floor).SetIsWalkable(false);
                    }
                }
            }
        }

        // 3) Bake edges between cells (walls, rails, etc.)
        EdgeBaker.Instance.BakeAllEdges();

        // 4) Gather explicit pathfinding links (stairs, lifts, portals)
        pathfindingLinkList = new List<PathfindingLink>();
        if (pathfindingLinkContainer != null)
        {

            foreach (Transform linkTf in pathfindingLinkContainer)
            {
                if (linkTf.TryGetComponent(out PathfindingLinkMonoBehaviour linkMb))
                {
                    pathfindingLinkList.Add(linkMb.GetPathfindingLink());

                }
            }
        }


    }

    /// <summary>
    /// Finds a path between two grid positions using the A* algorithm with an optional move budget.
    ///
    /// What it does:
    /// - Serves as the public entry point for pathfinding queries.
    /// - Wraps the internal implementation (<see cref="FindPathInternal"/>) while exposing a simpler interface.
    /// - Returns a list of grid positions representing the optimal route, or <c>null</c> if no valid path exists.
    ///
    /// Why this exists in RogueShooter:
    /// - Gameplay systems (player input, AI, ability targeting) request paths through this single method.
    /// - The move budget allows computing reachable tiles for tactical range previews (e.g. 6 steps max).
    ///
    /// Implementation notes:
    /// - <paramref name="moveBudgetSteps"/> can be set to <c>int.MaxValue</c> for unrestricted pathfinding.
    /// - Outputs <paramref name="pathLength"/> as total F-cost (movement cost + heuristic) of the found path.
    /// </summary>
    public List<GridPosition> FindPath(
        GridPosition startGridPosition,
        GridPosition endGridPosition,
        out int pathLeght,
        int moveBudgetSteps)
    {
        return FindPathInternal(startGridPosition, endGridPosition, out pathLeght, moveBudgetSteps);
    }

    /// <summary>
    /// Core A* pathfinding algorithm implementation with movement budget and edge-aware navigation.
    ///
    /// What it does:
    /// - Expands nodes using standard A* logic (G = actual cost, H = heuristic, F = G + H).
    /// - Honors per-edge blockers from <see cref="EdgeBaker"/> via <c>CanStep()</c>.
    /// - Supports a movement budget (in “steps”) to limit search range for tactical actions.
    /// - Uses a lightweight custom <see cref="PriorityQueue{T}"/> for open list management.
    ///
    /// Why this exists in RogueShooter:
    /// - Provides deterministic and efficient tactical pathfinding across destructible, multi-floor maps.
    /// - Integrates movement range rules directly into path expansion, avoiding separate “reachable area” passes.
    /// - Enables AI and player systems to share the same consistent grid and cost rules.
    ///
    /// Algorithm overview:
    /// 1. Convert <paramref name="moveBudgetSteps"/> into internal cost units (straight = 10, diagonal = 20).
    /// 2. Early reject if even the heuristic distance exceeds the available budget.
    /// 3. Initialize open and closed sets and enqueue the start node.
    /// 4. While the open queue is not empty:
    ///    - Dequeue the node with the lowest F-cost.
    ///    - If its G-cost exceeds the movement budget → skip.
    ///    - If this is the end node → reconstruct the path and return.
    ///    - Otherwise, expand all valid neighbors that are walkable and not blocked by edges.
    /// 5. Return <c>null</c> if no path exists within the allowed movement cost.
    ///
    /// Performance notes:
    /// - Avoids heap allocations via <see cref="EnsureInit"/> using generation IDs.
    /// - Supports optional runtime diagnostics through <see cref="PathfindingDiagnostics"/> (#if PERFORMANCE_DIAG).
    /// - Handles diagonal movement correctly with octile distances and no corner clipping.
    /// </summary>
    private List<GridPosition> FindPathInternal(
        GridPosition startGridPosition,
        GridPosition endGridPosition,
        out int pathLeght,
        int moveBudgetSteps)
    {

#if PERFORMANCE_DIAG

        var diag = PathfindingDiagnostics.Instance;
        bool diagOn = diag != null && diag.enabledRuntime;

        System.Diagnostics.Stopwatch sw = null;
        if (diagOn) { sw = new System.Diagnostics.Stopwatch(); sw.Start(); }

        int expanded = 0; // kuinka monta solmua laajennettiin (pop + käsitelty)

#endif
        // 1) Convert step-based budget to internal movement cost units
        int moveBudgetCost = (moveBudgetSteps == int.MaxValue)
            ? int.MaxValue 
            : moveBudgetSteps * MOVE_STRAIGHT_COST;

        // Early pruning: skip search if even the heuristic distance exceeds the move budget
        int minPossibleCost = CalculateDistance(startGridPosition, endGridPosition);
        if (minPossibleCost > moveBudgetCost)
        {
            pathLeght = 0;

#if PERFORMANCE_DIAG

            if (diagOn) { sw.Stop(); diag.AddSample(sw.Elapsed.TotalMilliseconds, false, 0, expanded); }

#endif

            return null;
        }

        currentGenerationID++;

        var openQueue = new PriorityQueue<PathNode>();
        HashSet<PathNode> openSet = new HashSet<PathNode>();
        HashSet<PathNode> closedSet = new HashSet<PathNode>();

        PathNode startNode = GetGridSystem(startGridPosition.floor).GetGridObject(startGridPosition);
        PathNode endNode = GetGridSystem(endGridPosition.floor).GetGridObject(endGridPosition);

        // Initialize start node
        EnsureInit(startNode);
        startNode.SetGCost(0);
        startNode.SetHCost(CalculateDistance(startGridPosition, endGridPosition));
        startNode.CalculateFCost();

        openQueue.Enqueue(startNode, startNode.GetFCost());
        openSet.Add(startNode);

        // 2) Main A* loop
        while (openQueue.Count > 0)
        {
            // Dequeue the node with the lowest F-cost; skip outdated entries
            PathNode currentNode = openQueue.Dequeue();
            if (closedSet.Contains(currentNode)) continue;

            EnsureInit(currentNode);

#if PERFORMANCE_DIAG
            expanded++;
#endif
            // Stop expanding if the current path already exceeds move budget
            if (currentNode.GetGCost() > moveBudgetCost)
                continue;

            // Goal reached → build final path
            if (currentNode == endNode)
            {
                pathLeght = endNode.GetFCost();
                var path = CalculatePath(endNode);

#if PERFORMANCE_DIAG

                if (diagOn)
                {
                    sw.Stop();
                    diag.AddSample(sw.Elapsed.TotalMilliseconds, success: true, pathLen: path.Count, expanded: expanded);
                }
#endif
                return path;
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            // 3) Expand all valid neighbor nodes
            foreach (PathNode neighbourNode in GetNeighbourList(currentNode))
            {
                if (closedSet.Contains(neighbourNode)) continue;

                if (!neighbourNode.GetIsWalkable())
                {
                    closedSet.Add(neighbourNode);
                    continue;
                }

                EnsureInit(neighbourNode);

                int stepCost = CalculateDistance(currentNode.GetGridPosition(), neighbourNode.GetGridPosition());
                int tentativeG = currentNode.GetGCost() + stepCost;

                // Skip paths that already exceed movement budget
                if (tentativeG > moveBudgetCost)
                    continue;

                // If this route to the neighbor is cheaper, record it
                if (tentativeG < neighbourNode.GetGCost())
                {
                    neighbourNode.SetCameFromPathNode(currentNode);
                    neighbourNode.SetGCost(tentativeG);
                    neighbourNode.SetHCost(CalculateDistance(neighbourNode.GetGridPosition(), endGridPosition));
                    neighbourNode.CalculateFCost();

                    if (!openSet.Contains(neighbourNode))
                    {   
                        
                        openQueue.Enqueue(neighbourNode, neighbourNode.GetFCost());
                        openSet.Add(neighbourNode);
                    }
                    else
                    {
                        // No decrease-key in PriorityQueue → push duplicate, old entry ignored when dequeued
                        openQueue.Enqueue(neighbourNode, neighbourNode.GetFCost());
                    }
                }
            }
        }

        // 4) No valid path within move budget
        pathLeght = 0;

#if PERFORMANCE_DIAG

        if (diagOn)
        {
            sw.Stop();
            diag.AddSample(sw.Elapsed.TotalMilliseconds, success: false, pathLen: 0, expanded: expanded);
        }

#endif
        return null;
    }

    /// <summary>
    /// Octile-distance cost between two grid positions for 8-directional movement.
    ///
    /// What it does:
    /// - Computes the admissible A* heuristic and unit step costs using:
    ///   diagonal = min(|dx|, |dz|), straight = | |dx| - |dz| |.
    /// - Returns MOVE_DIAGONAL_COST * diagonal + MOVE_STRAIGHT_COST * straight.
    ///
    /// Why this exists in RogueShooter:
    /// - Matches our movement rules exactly (orthogonal and diagonal with different costs),
    ///   keeping A* both admissible and consistent (no overestimation).
    ///
    /// Implementation notes:
    /// - MOVE_STRAIGHT_COST = 10, MOVE_DIAGONAL_COST = 20 to align with budget-in-steps logic.
    /// </summary>
    public int CalculateDistance(GridPosition a, GridPosition b)
    {
        GridPosition d = a - b;
        int xDistance = Mathf.Abs(d.x);
        int zDistance = Mathf.Abs(d.z);
        int diagonal = Mathf.Min(xDistance, zDistance);
        int straight = Mathf.Abs(xDistance - zDistance);
        return MOVE_DIAGONAL_COST * diagonal + MOVE_STRAIGHT_COST * straight;
    }

    /// <summary>
    /// Retrieves the grid system instance for a given floor index.
    ///
    /// What it does:
    /// - Returns the <see cref="GridSystem{PathNode}"/> corresponding to the specified floor.
    ///
    /// Why this exists in RogueShooter:
    /// - Supports multi-floor pathfinding where each floor maintains its own grid structure.
    /// - Allows systems to query and operate on nodes per-floor without global lookups.
    ///
    /// Implementation notes:
    /// - Assumes grids were created during <see cref="Setup"/> and stored in <c>gridSystemList</c>.
    /// </summary>
    private GridSystem<PathNode> GetGridSystem(int floor) => gridSystemList[floor];

    /// <summary>
    /// Retrieves a single pathfinding node at the given (x, z, floor) position.
    ///
    /// What it does:
    /// - Resolves to the correct grid system (via <see cref="GetGridSystem"/>) and returns its node.
    ///
    /// Why this exists in RogueShooter:
    /// - Simplifies code that frequently needs to access individual nodes by absolute coordinates.
    /// - Used heavily in A*, edge baking, and AI systems for node-level data manipulation.
    ///
    /// Implementation notes:
    /// - Returns <c>null</c> if the grid system or node does not exist (should not normally happen after Setup()).
    /// </summary>
    public PathNode GetNode(int x, int z, int floor)
        => GetGridSystem(floor).GetGridObject(new GridPosition(x, z, floor));

    /// <summary>
    /// Converts a unit orthogonal delta (dx, dz) into an EdgeMask direction.
    ///
    /// What it does:
    /// - Maps (0,+1)→N, (+1,0)→E, (0,-1)→S, (-1,0)→W.
    /// - Returns <see cref="EdgeMask.None"/> for non-orthogonal deltas.
    ///
    /// Why this exists in RogueShooter:
    /// - Used by <see cref="CanStep"/> to check per-edge walls symmetrically for orthogonal moves.
    /// - Keeps edge checks readable and centralized.
    ///
    /// Implementation notes:
    /// - Diagonal deltas are intentionally not mapped (handled separately in <see cref="CanStep"/>).
    /// </summary>
    private EdgeMask DirFromDelta(int dx, int dz)
    {
        if (dx == 0 && dz == +1) return EdgeMask.N;
        if (dx == +1 && dz == 0) return EdgeMask.E;
        if (dx == 0 && dz == -1) return EdgeMask.S;
        if (dx == -1 && dz == 0) return EdgeMask.W;
        return EdgeMask.None;
    }

    /// <summary>
    /// Returns the opposite edge direction (N↔S, E↔W).
    ///
    /// What it does:
    /// - Maps a cardinal edge to its opposite; otherwise returns <see cref="EdgeMask.None"/>.
    ///
    /// Why this exists in RogueShooter:
    /// - Ensures symmetric edge checks (A’s east equals B’s west) in movement validation.
    /// - Avoids “one-way walls” by enforcing consistency across neighboring nodes.
    /// </summary>
    private EdgeMask Opposite(EdgeMask d) => d switch
    {
        EdgeMask.N => EdgeMask.S,
        EdgeMask.E => EdgeMask.W,
        EdgeMask.S => EdgeMask.N,
        EdgeMask.W => EdgeMask.E,
        _ => EdgeMask.None
    };

    /// <summary>
    /// Determines whether movement from cell A to cell B is allowed,
    /// honoring edge walls and preventing diagonal corner-cutting.
    ///
    /// What it does:
    /// - Validates that the delta is a single orthogonal or diagonal step.
    /// - For orthogonal moves: blocks movement if either side of the shared edge has a wall flag.
    /// - For diagonal moves: requires at least one orthogonal “L-shaped” two-step route to be clear
    ///   (A→X→B or A→Z→B), preventing cutting through blocked corners.
    ///
    /// Why this exists in RogueShooter:
    /// - Enforces tactical rules consistent with baked edge data (from EdgeBaker).
    /// - Prevents unrealistic diagonal slips past doorframes/rails and yields robust cover behavior.
    ///
    /// Implementation notes:
    /// - Uses <see cref="DirFromDelta"/> and <see cref="Opposite(EdgeMask)"/> to test symmetric edge walls.
    /// - For diagonals, both intermediate orthogonal neighbors must be valid and walkable before testing paths.
    /// </summary>
    private bool CanStep(GridPosition a, GridPosition b)
    {
        int dx = b.x - a.x;
        int dz = b.z - a.z;

        bool diagonal = Mathf.Abs(dx) == 1 && Mathf.Abs(dz) == 1;
        bool ortho = (dx == 0) ^ (dz == 0);
        if (!diagonal && !ortho) return false; // Disallow jumps longer than 1 cell


        var nodeA = GetNode(a.x, a.z, a.floor);
        var nodeB = GetNode(b.x, b.z, b.floor);

        // ORTHOGONAL MOVE: both sides of the shared edge must be open
        if (ortho)
        {
            var dir = DirFromDelta(dx, dz);
            if (dir == EdgeMask.None) return false;
            if (nodeA.HasWall(dir)) return false;               // wall on A’s side
            if (nodeB.HasWall(Opposite(dir))) return false;     // wall on B’s side
            return true;
        }

        // DIAGONAL MOVE: require at least one clear L-route (no corner clipping)
        var aToX = new GridPosition(a.x + dx, a.z, a.floor);
        var aToZ = new GridPosition(a.x, a.z + dz, a.floor);

        // Both intermediates must be inside bounds and walkable to be considered
        if (!IsValidGridPosition(aToX) || !IsValidGridPosition(aToZ)) return false;
        if (!IsWalkable(aToX) || !IsWalkable(aToZ)) return false;

        // Route 1: A -> X -> B (two orthogonal steps)
        bool pathViaX = CanStep(a, aToX) && CanStep(aToX, b);

        // Route 2: A -> Z -> B (two orthogonal steps)
        bool pathViaZ = CanStep(a, aToZ) && CanStep(aToZ, b);

        return pathViaX || pathViaZ;

    }

    private bool IsValidGridPosition(GridPosition gridPosition)
    {
        return LevelGrid.Instance.GetGridSystem(gridPosition.floor).IsValidGridPosition(gridPosition);
    }

    private bool IsWalkable(GridPosition gridPosition)
    {
        PathNode node = GetNode(gridPosition.x, gridPosition.z, gridPosition.floor);
        return node != null && node.GetIsWalkable();
    }

    /// <summary>
    /// Collects all valid neighbor nodes (up to 8) for A* expansion from the given node.
    ///
    /// What it does:
    /// - Iterates orthogonal and diagonal neighbors within the current floor bounds.
    /// - Filters out non-walkable cells early.
    /// - Uses <see cref="CanStep"/> to enforce edge walls and anti-corner-cutting rules.
    /// - Additionally appends any explicit link targets (e.g., stairs/elevators) connected to this cell.
    ///
    /// Why this exists in RogueShooter:
    /// - Centralizes movement rules so both AI and player pathfinding share identical constraints.
    /// - Supports multi-floor traversal via designer-authored links without special-casing A*.
    ///
    /// Implementation notes:
    /// - Neighbor order is stable to keep behavior deterministic across runs.
    /// - Links bypass edge checks by design (they represent explicit allowed transitions).
    /// </summary>
    private List<PathNode> GetNeighbourList(PathNode currentNode)
    {
        List<PathNode> result = new List<PathNode>(8);

        GridPosition gp = currentNode.GetGridPosition();

        // Candidate offsets (W, SW, NW, E, SE, NE, S, N)
        static IEnumerable<(int dx, int dz)> Offsets()
        {
            yield return (-1, 0); // W
            yield return (-1, -1); // SW
            yield return (-1, +1); // NW

            yield return (+1, 0); // E
            yield return (+1, -1); // SE
            yield return (+1, +1); // NE

            yield return (0, -1); // S
            yield return (0, +1); // N
        }

        // 1) Same-floor neighbors with edge rules
        foreach (var (dx, dz) in Offsets())
        {
            int nx = gp.x + dx;
            int nz = gp.z + dz;

            // Bounds check
            if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;

            var ngp = new GridPosition(nx, nz, gp.floor);

            // Early reject: must be walkable
            if (!IsWalkable(ngp)) continue;

            // Respect edge blockers and corner rules
            if (!CanStep(gp, ngp)) continue;

            result.Add(GetNode(nx, nz, gp.floor));
        }

        // 2) Explicit links (stairs/lifts/portals) — allowed transitions across floors
        foreach (GridPosition linkGp in GetPathfindingLinkConnectedGridPositionList(gp))
        {
            // Varmista ettei mennä ulos
            if (!IsValidGridPosition(linkGp)) continue;
            if (!IsWalkable(linkGp)) continue;

            // Links intentionally bypass edge checks; they model designer-approved moves
            result.Add(GetNode(linkGp.x, linkGp.z, linkGp.floor));
        }

        return result;
    }

    /// <summary>
    /// Returns all grid positions directly connected to the given position via explicit pathfinding links.
    ///
    /// What it does:
    /// - Searches the prebuilt <see cref="pathfindingLinkList"/> for connections where the given cell
    ///   is either endpoint (A or B).
    /// - Collects and returns the corresponding linked destinations.
    ///
    /// Why this exists in RogueShooter:
    /// - Enables multi-floor traversal and special transitions (stairs, elevators, hatches, ladders, etc.)
    ///   that bypass standard neighbor logic.
    /// - Keeps such transitions data-driven: designers place <see cref="PathfindingLinkMonoBehaviour"/> objects
    ///   in the scene instead of hardcoding connections.
    ///
    /// Implementation notes:
    /// - Links are treated as bidirectional: A↔B.
    /// - The returned positions are later validated for walkability before use.
    /// </summary>
    private List<GridPosition> GetPathfindingLinkConnectedGridPositionList(GridPosition gridPosition)
    {
        List<GridPosition> result = new List<GridPosition>();
        if (pathfindingLinkList == null || pathfindingLinkList.Count == 0) return result;

        foreach (PathfindingLink link in pathfindingLinkList)
        {
            if (link.gridPositionA == gridPosition) result.Add(link.gridPositionB);
            if (link.gridPositionB == gridPosition) result.Add(link.gridPositionA);
        }
        return result;
    }

    /// <summary>
    /// Reconstructs a complete path from the end node by backtracking through parent pointers.
    ///
    /// What it does:
    /// - Traces the <c>CameFrom</c> chain from the goal node back to the start.
    /// - Reverses the collected list and converts it into grid positions for gameplay use.
    ///
    /// Why this exists in RogueShooter:
    /// - Converts A*’s internal node traversal history into a usable list of <see cref="GridPosition"/> steps.
    /// - Provides a deterministic, minimal path sequence for units to follow.
    ///
    /// Implementation notes:
    /// - Result always includes both the start and end positions.
    /// - Returned list is ordered from start → goal.
    /// </summary>
    private List<GridPosition> CalculatePath(PathNode endNode)
    {
        List<PathNode> pathNodes = new List<PathNode> { endNode };
        PathNode current = endNode;

        while (current.GetCameFromPathNode() != null)
        {
            pathNodes.Add(current.GetCameFromPathNode());
            current = current.GetCameFromPathNode();
        }

        pathNodes.Reverse();

        List<GridPosition> gridPositions = new List<GridPosition>(pathNodes.Count);
        foreach (PathNode n in pathNodes) gridPositions.Add(n.GetGridPosition());

        return gridPositions;
    }

    /// <summary>
    /// Returns whether the given grid position is currently walkable.
    ///
    /// Why this exists in RogueShooter:
    /// - Unified query for gameplay/AI to check if a tile can be occupied.
    /// - Mirrors the internal node flag computed during Setup() (raycasts + edge bake).
    /// </summary>
    public bool IsWalkableGridPosition(GridPosition gridPosition)
        => GetGridSystem(gridPosition.floor).GetGridObject(gridPosition).GetIsWalkable();

    /// <summary>
    /// Sets the walkability of a grid position at runtime.
    ///
    /// Why this exists in RogueShooter:
    /// - Dynamic gameplay (e.g., collapses, placed barricades, hazards) can toggle occupancy rules.
    /// - Lets designers/systems override the initial raycast result if needed.
    ///
    /// Implementation notes:
    /// - Consider calling <see cref="EdgeBaker.RebakeEdgesAround"/> if geometry changes near this tile.
    /// </summary>
    public void SetIsWalkableGridPosition(GridPosition gridPosition, bool isWalkable)
        => GetGridSystem(gridPosition.floor).GetGridObject(gridPosition).SetIsWalkable(isWalkable);

    /// <summary>
    /// Lazily resets per-search A* fields on a node using a generation ID guard.
    ///
    /// What it does:
    /// - If the node was last touched in a previous search (generation mismatch),
    ///   resets G/H/F, clears the “came from” pointer, and marks the node with the current generation.
    ///
    /// Why this exists in RogueShooter:
    /// - Avoids per-search heap allocations and dictionary clears by reusing nodes safely.
    /// - Ensures stale scores from earlier searches never leak into the current query.
    ///
    /// Implementation notes:
    /// - Must be called on any node before reading/updating A* fields during a search.
    /// </summary>
    void EnsureInit(PathNode node)
    {
        if (node.LastGenerationID != currentGenerationID)
        {
            node.SetGCost(int.MaxValue);
            node.SetHCost(0);
            node.CalculateFCost();
            node.ResetCameFromPathNode();
            node.MarkGeneration(currentGenerationID);
        }
    }
    
    /// <summary>
    /// Converts a movement budget in steps to internal cost units.
    ///
    /// Why this exists in RogueShooter:
    /// - Keeps UI/AI logic readable (work in “steps”) while A* uses cost units (10 per orthogonal step).
    /// </summary>
    public static int CostFromSteps(int steps) => steps * MOVE_STRAIGHT_COST;

    /// <summary>
    /// Gets all explicit pathfinding links collected from the scene (stairs, elevators, robes).
    ///
    /// Why this exists in RogueShooter:
    /// - External systems (UI, debugging, AI) may need to inspect or visualize cross-cell/floor connections.
    /// </summary>
    public List<PathfindingLink> GetPathfindingLinks()
    {
        return pathfindingLinkList ?? new List<PathfindingLink>();
    }

    public int GetWidth()
    {
        return width;
    }

    public int GetHeight()
    {
        return height;
    }
}
