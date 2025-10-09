using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// The MoveAction class is responsible for handling the movement of a unit in the game.
/// It allows the unit to move to a target position, and it calculates valid move grid positions based on the unit's current position.
/// </summary>
public class MoveAction : BaseAction
{

    public event EventHandler OnStartMoving;
    public event EventHandler OnStopMoving;

    GridPosition thisTurnStartingGridPosition;
    GridPosition thisTurnEndridPosition;

    [SerializeField] private int maxMoveDistance = 4;

    private int distance;

    private List<Vector3> positionList;
    private int currentPositionIndex;

    private bool isChangingFloors;
    private float differentFloorsTeleportTimer;
    private float differentFloorsTeleportTimerMax = .5f;

    private void Start()
    {
        distance = 0;
        thisTurnStartingGridPosition = unit.GetGridPosition();

        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        thisTurnStartingGridPosition = unit.GetGridPosition();
        distance = 0;
    }

    private void Update()
    {
        if (!isActive) return;

        Vector3 targetPosition = positionList[currentPositionIndex];

        if (isChangingFloors)
        {
            Vector3 targetSameFloorPosition = targetPosition;
            targetSameFloorPosition.y = transform.position.y;
            Vector3 rotateDirection = (targetSameFloorPosition - transform.position).normalized;

            float rotationSpeed = 10f;
            transform.forward = Vector3.Slerp(transform.forward, rotateDirection, Time.deltaTime * rotationSpeed);
            differentFloorsTeleportTimer -= Time.deltaTime;
            if (differentFloorsTeleportTimer < 0f)
            {
                isChangingFloors = false;
                transform.position = targetPosition;
            }
        }
        else
        {

            Vector3 moveDirection = (targetPosition - transform.position).normalized;

            // Rotate towards the target position
            float rotationSpeed = 10f;
            transform.forward = Vector3.Slerp(transform.forward, moveDirection, Time.deltaTime * rotationSpeed);

            // Move towards the target position
            float moveSpeed = 6f;
            transform.position += moveSpeed * Time.deltaTime * moveDirection;
        }


        float stoppingDistance = 0.2f;
        if (Vector3.Distance(transform.position, targetPosition) < stoppingDistance)
        {
            thisTurnEndridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
            DistanceFromStartingPoint();

            currentPositionIndex++;
            if (currentPositionIndex >= positionList.Count)
            {
                
                OnStopMoving?.Invoke(this, EventArgs.Empty);
                ActionComplete();
            }
            else
            {
                targetPosition = positionList[currentPositionIndex];
                GridPosition targetGridPosition = LevelGrid.Instance.GetGridPosition(targetPosition);
                GridPosition unitGridPosition = LevelGrid.Instance.GetGridPosition(transform.position);

                if (targetGridPosition.floor != unitGridPosition.floor)
                {
                    //Different floors
                    isChangingFloors = true;
                    differentFloorsTeleportTimer = differentFloorsTeleportTimerMax;
                }
            }
        }
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        List<GridPosition> pathGridPositionsList = PathFinding.Instance.FindPath(unit.GetGridPosition(), gridPosition, out int pathLeght, maxMoveDistance);

        currentPositionIndex = 0;
        positionList = new List<Vector3>();

        foreach (GridPosition pathGridPosition in pathGridPositionsList)
        {
            positionList.Add(LevelGrid.Instance.GetWorldPosition(pathGridPosition));

        }

        OnStartMoving?.Invoke(this, EventArgs.Empty);
        ActionStart(onActionComplete);
    }


    private void DistanceFromStartingPoint()
    {
        int newDistance = PathFinding.Instance.CalculateDistance(thisTurnStartingGridPosition, thisTurnEndridPosition);

        int delta = newDistance - distance;
        if (Mathf.Abs(delta) < 10) return;
        if (delta != 0)
        {
            Debug.Log($"Net distance delta: {delta / 10} tiles");
            unit.RegenCoverOnMove(delta);
        }

        distance = newDistance;
    }
 
    public override List<GridPosition> GetValidGridPositionList()
    {
        var valid = new List<GridPosition>();
        var candidates = new HashSet<GridPosition>(); // estää duplikaatit

        GridPosition unitPos = unit.GetGridPosition();
        int startFloor = unitPos.floor;

        // Jos maxMoveDistance on RUUTUJA, kustannusbudjetti on *10 per ruutu*
        const int COST_PER_TILE = 10;
        int moveBudgetCost = maxMoveDistance * COST_PER_TILE;

        // --- 1) Nykyisen kerroksen ruudut (perus-offsetit) ---
        for (int dx = -maxMoveDistance; dx <= maxMoveDistance; dx++)
        {
            for (int dz = -maxMoveDistance; dz <= maxMoveDistance; dz++)
            {
                var test = new GridPosition(unitPos.x + dx, unitPos.z + dz, startFloor);
                candidates.Add(test);
            }
        }

        // --- 2) Linkkien kautta saavutettavat kerrokset (hybridi) ---
        var links = PathFinding.Instance.GetPathfindingLinks();
        if (links != null && links.Count > 0)
        {
            foreach (var link in links)
            {
                // A -> B
                if (link.gridPositionA.floor == startFloor)
                {
                    int lbToA = PathFinding.Instance.CalculateDistance(unitPos, link.gridPositionA);
                    if (lbToA <= moveBudgetCost)
                    {
                        int remaining = moveBudgetCost - lbToA;
                        int radiusTiles = Mathf.Max(0, remaining / COST_PER_TILE);

                        for (int dx = -radiusTiles; dx <= radiusTiles; dx++)
                        {
                            for (int dz = -radiusTiles; dz <= radiusTiles; dz++)
                            {
                                var aroundB = new GridPosition(
                                    link.gridPositionB.x + dx,
                                    link.gridPositionB.z + dz,
                                    link.gridPositionB.floor
                                );
                                candidates.Add(aroundB);
                            }
                        }
                    }
                }

                // B -> A
                if (link.gridPositionB.floor == startFloor)
                {
                    int lbToB = PathFinding.Instance.CalculateDistance(unitPos, link.gridPositionB);
                    if (lbToB <= moveBudgetCost)
                    {
                        int remaining = moveBudgetCost - lbToB;
                        int radiusTiles = Mathf.Max(0, remaining / COST_PER_TILE);

                        for (int dx = -radiusTiles; dx <= radiusTiles; dx++)
                        {
                            for (int dz = -radiusTiles; dz <= radiusTiles; dz++)
                            {
                                var aroundA = new GridPosition(
                                    link.gridPositionA.x + dx,
                                    link.gridPositionA.z + dz,
                                    link.gridPositionA.floor
                                );
                                candidates.Add(aroundA);
                            }
                        }
                    }
                }
            }
        }

        // --- 3) Suodata & tee vain yksi A* per kandidaatti (välimuistilla) ---
        foreach (var test in candidates)
        {
            // Perusvalidoinnit
            if (!LevelGrid.Instance.IsValidGridPosition(test)) continue;
            if (test == unitPos) continue;
            if (LevelGrid.Instance.HasAnyUnitOnGridPosition(test)) continue;
            if (!PathFinding.Instance.IsWalkableGridPosition(test)) continue;

            // Heuristiikkakarsinta (Manhattan*10): jos edes optimistinen kustannus > budjetti, skip
            int lowerBound = PathFinding.Instance.CalculateDistance(unitPos, test);
            if (lowerBound > moveBudgetCost) continue;

            // *** VAIN YKSI A* per ruutu (mutta nyt cachetettuna saman framen sisällä) ***
            if (!TryGetPathCostCached(unitPos, test, out int pathCost)) continue; // ei polkua
            if (pathCost > moveBudgetCost) continue;

            valid.Add(test);
        }

        return valid;
    }

    public override string GetActionName()
    {
        return "Move";
    }

    // --- Per-frame pathfinding cache ---
    private struct PathQuery : IEquatable<PathQuery> {
        public GridPosition start;
        public GridPosition end;
        public bool Equals(PathQuery other) => start == other.start && end == other.end;
        public override bool Equals(object obj) => obj is PathQuery pq && Equals(pq);
        public override int GetHashCode() => (start.GetHashCode() * 397) ^ end.GetHashCode();
    }

    private struct PathCacheEntry {
        public bool exists;
        public int cost;
        // Jos joskus haluat itse polun, voit lisätä: public List<GridPosition> path;
    }

    // Yhteinen cache tälle actionille (voisi olla myös static jos haluat jakaa yli instanssien)
    private Dictionary<PathQuery, PathCacheEntry> _pathCache = new Dictionary<PathQuery, PathCacheEntry>(256);
    private int _cacheFrame = -1;

    private bool TryGetPathCostCached(GridPosition start, GridPosition end, out int cost)
    {
        // Nollaa cache kerran per frame
        int frame = Time.frameCount;
        if (_cacheFrame != frame) {
            _pathCache.Clear();
            _cacheFrame = frame;
        }

        var key = new PathQuery { start = start, end = end };
        if (_pathCache.TryGetValue(key, out var entry)) {
            cost = entry.cost;
            return entry.exists;
        }

        // Ei ollut välimuistissa -> laske kerran
        var path = PathFinding.Instance.FindPath(start, end, out int pathCost, maxMoveDistance);
        bool exists = path != null;
        _pathCache[key] = new PathCacheEntry { exists = exists, cost = pathCost };

        cost = pathCost;
        return exists;
    }

    public int GetMaxMoveDistance()
    {
        return maxMoveDistance;
    }

    /// <summary>
    /// ENEMY AI: 
    /// Move toward to Player unit to make shoot action.
    /// </summary>
    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        int targetCountAtGridPosition = unit.GetAction<ShootAction>().GetTargetCountAtPosition(gridPosition);

        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = targetCountAtGridPosition * 10,
        };
    }
}
