using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* -polunetsintä 8-suuntaisella liikkeellä (N, NE, E, SE, S, SW, W, NW).
/// Huom: Script Execution Order asetettu ajamaan LevelGrid.cs:n jälkeen (korkea prioriteetti).
/// </summary>
public class PathFinding : MonoBehaviour
{
    public static PathFinding Instance { get; private set; }

    private const int MOVE_STRAIGHT_COST = 10;
    private const int MOVE_DIAGONAL_COST = 20;//14;

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

    public void Setup(int width, int height, float cellSize, int floorAmount)
    {
        this.width = width;
        this.height = height;

        gridSystemList = new List<GridSystem<PathNode>>();

        // Luo gridit per kerros
        for (int floor = 0; floor < floorAmount; floor++)
        {
            GridSystem<PathNode> gridSystem = new GridSystem<PathNode>(
                width, height, cellSize, floor, LevelGrid.FLOOR_HEIGHT,
                (GridSystem<PathNode> g, GridPosition gridPosition) => new PathNode(gridPosition)
            );

            if (showDebug && gridDebugPrefab != null)
            {
                gridSystem.CreateDebugObjects(gridDebugPrefab);
            }

            gridSystemList.Add(gridSystem);
        }

        // Alusta walkable-tieto raycasteilla (lyhyet, yhdenmukaistetut)
        float raycastOffsetDistance = 1f;   // start offset
        float raycastDistance = raycastOffsetDistance * 2f; // 2f

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                for (int floor = 0; floor < floorAmount; floor++)
                {
                    GridPosition gridPosition = new GridPosition(x, z, floor);
                    Vector3 worldPosition = LevelGrid.Instance.GetWorldPosition(gridPosition);

                    // Oletuksena ei-käveltävä
                    GetNode(x, z, floor).SetIsWalkable(false);

                    // Lattiatesti (down)
                    if (Physics.Raycast(
                            worldPosition + Vector3.up * raycastOffsetDistance,
                            Vector3.down,
                            raycastDistance,
                            floorLayerMask))
                    {
                        GetNode(x, z, floor).SetIsWalkable(true);
                    }

                    // Esteetesti (up) — lyhyt, jotta kattoa yms. ei skannata turhaan
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

        EdgeBaker.Instance.BakeAllEdges();

        // Rakenna linkit VAIN kerran silmukoiden ulkopuolella
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

    // 2) UUSI API – liikebudjetilla (askelina), esim. 6
    public List<GridPosition> FindPath(
        GridPosition startGridPosition,
        GridPosition endGridPosition,
        out int pathLeght,
        int moveBudgetSteps)
    {
        return FindPathInternal(startGridPosition, endGridPosition, out pathLeght, moveBudgetSteps);
    }

    /// <summary>
    /// A* polku alusta loppuun. Palauttaa ruutulistan tai null jos ei polkua.
    /// </summary>
    /// 
    // 3) VARSINAINEN TOTEUTUS – siirretty sisäiseksi ja lisätty budjetti
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
        // --- BUDJETTI: muutetaan askeleet kustannukseksi (suora 10, diag 20) ---
        int moveBudgetCost = (moveBudgetSteps == int.MaxValue) ? int.MaxValue : moveBudgetSteps * MOVE_STRAIGHT_COST;

        // VARHAINEN KARSINTA: jos edes heuristiikka ylittää budjetin → ei yritetä
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

        EnsureInit(startNode);
        startNode.SetGCost(0);
        startNode.SetHCost(CalculateDistance(startGridPosition, endGridPosition));
        startNode.CalculateFCost();

        openQueue.Enqueue(startNode, startNode.GetFCost());
        openSet.Add(startNode);

        while (openQueue.Count > 0)
        {
            // Popataan kunnes saadaan solmu, jota ei ole jo suljettu (duplikaattien varalta)
            PathNode currentNode = openQueue.Dequeue();
            if (closedSet.Contains(currentNode)) continue;

            EnsureInit(currentNode);

#if PERFORMANCE_DIAG
            expanded++;
#endif
            // Suojavyö: jos nykyinen g jo yli budjetin, ei jatketa tästä haarasta
            if (currentNode.GetGCost() > moveBudgetCost)
                continue;

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

                // BUDJETTIRAJAUS: älä työnnä yli-budjetin solmuja eteenpäin
                if (tentativeG > moveBudgetCost)
                    continue;

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
                        // Ei decrease-key:tä → työnnä uusi; vanhat ohitetaan dequeue-vaiheessa
                        openQueue.Enqueue(neighbourNode, neighbourNode.GetFCost());
                    }
                }
            }
        }

        // Ei polkua budjetin sisällä
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
    /// Octile-distance (8-suuntaisen liikkeen heuristiikka/kustannus).
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

    private GridSystem<PathNode> GetGridSystem(int floor) => gridSystemList[floor];

    public PathNode GetNode(int x, int z, int floor)
        => GetGridSystem(floor).GetGridObject(new GridPosition(x, z, floor));


    // Suunnan tulkinta kahden ruudun välille (vain orto)
    private EdgeMask DirFromDelta(int dx, int dz)
    {
        if (dx == 0 && dz == +1) return EdgeMask.N;
        if (dx == +1 && dz == 0) return EdgeMask.E;
        if (dx == 0 && dz == -1) return EdgeMask.S;
        if (dx == -1 && dz == 0) return EdgeMask.W;
        return EdgeMask.None;
    }

    private EdgeMask Opposite(EdgeMask d) => d switch
    {
        EdgeMask.N => EdgeMask.S,
        EdgeMask.E => EdgeMask.W,
        EdgeMask.S => EdgeMask.N,
        EdgeMask.W => EdgeMask.E,
        _ => EdgeMask.None
    };

    // Palauttaa: voiko siirtyä A -> B huomioiden reunaesteet ja kulmanleikkauksen
    private bool CanStep(GridPosition a, GridPosition b)
    {
        int dx = b.x - a.x;
        int dz = b.z - a.z;

        bool diagonal = Mathf.Abs(dx) == 1 && Mathf.Abs(dz) == 1;
        bool ortho = (dx == 0) ^ (dz == 0);
        if (!diagonal && !ortho) return false; // ei tueta hyppyjä >1

        var nodeA = GetNode(a.x, a.z, a.floor);
        var nodeB = GetNode(b.x, b.z, b.floor);

        // Orto-liike: tarkista A:n reuna ja B:n vastareuna
        if (ortho)
        {
            var dir = DirFromDelta(dx, dz);
            if (dir == EdgeMask.None) return false;
            if (nodeA.HasWall(dir)) return false;
            if (nodeB.HasWall(Opposite(dir))) return false;
            return true;
        }

        // Diagonaali: vaadi molemmat ortopolut vapaiksi (ei kulman läpi oikoamista)
        var aToX = new GridPosition(a.x + dx, a.z, a.floor);
        var aToZ = new GridPosition(a.x, a.z + dz, a.floor);

        if (!IsValidGridPosition(aToX) || !IsValidGridPosition(aToZ)) return false;
        if (!IsWalkable(aToX) || !IsWalkable(aToZ)) return false;

        bool pathViaX = CanStep(a, aToX) && CanStep(aToX, b);
        bool pathViaZ = CanStep(a, aToZ) && CanStep(aToZ, b);

        return pathViaX || pathViaZ;

    }

    private bool IsValidGridPosition(GridPosition gridPosition)
    {
        // Kysytään LevelGridiltä (tai siltä, missä sun GridSystem on käytössä)
        return LevelGrid.Instance.GetGridSystem(gridPosition.floor).IsValidGridPosition(gridPosition);
    }

    private bool IsWalkable(GridPosition gridPosition)
    {
        PathNode node = GetNode(gridPosition.x, gridPosition.z, gridPosition.floor);
        return node != null && node.GetIsWalkable();
    }

    private List<PathNode> GetNeighbourList(PathNode currentNode)
    {
        List<PathNode> result = new List<PathNode>(8);

        GridPosition gp = currentNode.GetGridPosition();

        // 8-suuntaa (NE, E, SE, S, SW, W, NW, N)
        // Käytetään samaa järjestystä kuin aiemmin, mutta checkataan CanStep
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

        foreach (var (dx, dz) in Offsets())
        {
            int nx = gp.x + dx;
            int nz = gp.z + dz;

            if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;

            var ngp = new GridPosition(nx, nz, gp.floor);
            if (!IsWalkable(ngp)) continue;

            // Reunaeste- ja kulma-esto
            if (!CanStep(gp, ngp)) continue;

            result.Add(GetNode(nx, nz, gp.floor));
        }

        // Linkit (esim. portaat / hissit kerrosten välillä)
        // Yleensä linkit ohittavat N/E/S/W -reunat, joten sallitaan suoraan.
        foreach (GridPosition linkGp in GetPathfindingLinkConnectedGridPositionList(gp))
        {
            // Varmista ettei mennä ulos
            if (!IsValidGridPosition(linkGp)) continue;
            if (!IsWalkable(linkGp)) continue;

            result.Add(GetNode(linkGp.x, linkGp.z, linkGp.floor));
        }

        return result;
    }

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

    public bool IsWalkableGridPosition(GridPosition gridPosition)
        => GetGridSystem(gridPosition.floor).GetGridObject(gridPosition).GetIsWalkable();

    public void SetIsWalkableGridPosition(GridPosition gridPosition, bool isWalkable)
        => GetGridSystem(gridPosition.floor).GetGridObject(gridPosition).SetIsWalkable(isWalkable);

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

    public static int CostFromSteps(int steps) => steps * MOVE_STRAIGHT_COST;

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
