using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

[DefaultExecutionOrder(-100)]
public class GridSystemVisual : MonoBehaviour
{
    public static GridSystemVisual Instance { get; private set; }

    [Header("Mouse-plane filter")]
    [SerializeField] private bool filterToMousePlanes = true;

    [Header("Team Vision Overlay")]
    [SerializeField] private bool teamVisionEnabled = true;
    [SerializeField] private GridVisualType teamVisionType = GridVisualType.Yellow;

    private readonly HashSet<GridPosition> _lastActionCells = new();
    private readonly List<GridPosition> _tmpList = new(256);

    [Serializable]
    public struct GridVisualTypeMaterial
    {
        public GridVisualType gridVisualType;
        public Material material;
    }

    public enum GridVisualType
    {
        white,
        Blue,
        Red,
        RedSoft,
        Yellow,
        TeamVision
    }

    [SerializeField] private Transform gridSystemVisualSinglePrefab;
    [SerializeField] private List<GridVisualTypeMaterial> gridVisualTypeMaterialList;

    private GridSystemVisualSingle[,,] gridSystemVisualSingleArray;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("More than one GridSystemVisual in the scene!" + transform + " " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        gridSystemVisualSingleArray = new GridSystemVisualSingle[
            LevelGrid.Instance.GetWidth(),
            LevelGrid.Instance.GetHeight(),
            LevelGrid.Instance.GetFloorAmount()
        ];

        for (int x = 0; x < LevelGrid.Instance.GetWidth(); x++)
        for (int z = 0; z < LevelGrid.Instance.GetHeight(); z++)
        for (int floor = 0; floor < LevelGrid.Instance.GetFloorAmount(); floor++)
        {
            GridPosition gridPosition = new(x, z, floor);
            Transform t = Instantiate(gridSystemVisualSinglePrefab, LevelGrid.Instance.GetWorldPosition(gridPosition), Quaternion.identity);
            gridSystemVisualSingleArray[x, z, floor] = t.GetComponent<GridSystemVisualSingle>();
        }

        UnitActionSystem.Instance.OnSelectedActionChanged += UnitActionSystem_OnSelectedActionChanged;
        UnitActionSystem.Instance.OnBusyChanged += UnitActionSystem_OnBusyChanged;
        LevelGrid.Instance.onAnyUnitMoveGridPosition += LevelGrid_onAnyUnitMoveGridPosition;

        if (TeamVisionService.Instance != null)
            TeamVisionService.Instance.OnTeamVisionChanged += HandleTeamVisionChanged;

        if (NetworkClient.active && TeamVisionService.Instance != null)
        {
            int myTeam = GetLocalPlayerTeamId();
            TeamVisionService.Instance.ClearTeamVision(myTeam);
        }

        UpdateGridVisuals();
    }

    private void OnDisable()
    {
        UnitActionSystem.Instance.OnSelectedActionChanged -= UnitActionSystem_OnSelectedActionChanged;
        UnitActionSystem.Instance.OnBusyChanged -= UnitActionSystem_OnBusyChanged;
        LevelGrid.Instance.onAnyUnitMoveGridPosition -= LevelGrid_onAnyUnitMoveGridPosition;

        if (TeamVisionService.Instance != null)
            TeamVisionService.Instance.OnTeamVisionChanged -= HandleTeamVisionChanged;
    }

    private bool HasMousePlaneAt(in GridPosition gp)
        => !filterToMousePlanes || (MousePlaneMap.Instance && MousePlaneMap.Instance.Has(gp));

    public int GetLocalPlayerTeamId()
    {
        GameMode mode = GameModeManager.SelectedMode;

        if (mode == GameMode.SinglePlayer || mode == GameMode.CoOp)
            return 0;

        if (mode == GameMode.Versus)
        {
            if (NetworkServer.active && !NetworkClient.active)
            {
                Debug.LogWarning("[GridSystemVisual] Running on dedicated server - no local player team");
                return 0;
            }

            if (NetworkClient.localPlayer != null)
            {
                var localPlayerUnit = NetworkClient.localPlayer.GetComponent<Unit>();
                if (localPlayerUnit != null)
                {
                    bool isHost = NetworkServer.active;
                    int teamId = isHost ? 0 : 1;
                    return teamId;
                }
            }

            bool fallbackIsHost = NetworkServer.active;
            int fallbackTeam = fallbackIsHost ? 0 : 1;
            return fallbackTeam;
        }

        return 0;
    }

    public void HideAllGridPositions()
    {
        for (int x = 0; x < LevelGrid.Instance.GetWidth(); x++)
        for (int z = 0; z < LevelGrid.Instance.GetHeight(); z++)
        for (int floor = 0; floor < LevelGrid.Instance.GetFloorAmount(); floor++)
            gridSystemVisualSingleArray[x, z, floor].Hide();
    }

    public void ShowGridPositionList(List<GridPosition> gridPositionList, GridVisualType gridVisualType)
    {
        var mat = GetGridVisualTypeMaterial(gridVisualType);
        foreach (var gp in gridPositionList)
        {
            if (filterToMousePlanes && !HasMousePlaneAt(gp)) continue;
            var cell = gridSystemVisualSingleArray[gp.x, gp.z, gp.floor];
            if (cell != null) cell.Show(mat);
        }
    }

    public void UpdateGridVisuals()
    {
        HideAllGridPositions();
        _lastActionCells.Clear();

        if (teamVisionEnabled && TeamVisionService.Instance != null)
            DrawTeamVisionOverlay();

        Unit selectedUnit = UnitActionSystem.Instance.GetSelectedUnit();
        if (selectedUnit == null) return;

        BaseAction selectedAction = UnitActionSystem.Instance.GetSelectedAction();

        GridVisualType gridVisualType;

        switch (selectedAction)
        {
            default:
            case MoveAction:
                gridVisualType = GridVisualType.white;
                break;

            case TurnTowardsAction:
                gridVisualType = GridVisualType.Blue;
                break;

            case ShootAction shoot:
            {
                gridVisualType = GridVisualType.Red;

                var origin = selectedUnit.GetGridPosition();
                int range  = shoot.GetMaxShootDistance();

                var cfg = LoSConfig.Instance;
                var visible = RaycastVisibility.ComputeVisibleTilesRaycast(
                    origin, range,
                    cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU
                );
                visible.RemoveWhere(gp => !RaycastVisibility.HasLineOfSightRaycastHeightAware(
                    origin, gp, cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU));

                _tmpList.Clear();
                _tmpList.AddRange(visible);
                ShowAndMark(_tmpList, GridVisualType.RedSoft);
                break;
            }

            case GranadeAction:
                gridVisualType = GridVisualType.Yellow;
                break;

            case MeleeAction:
                gridVisualType = GridVisualType.Red;
                ShowAndMark(BuildRangeSquare(selectedUnit.GetGridPosition(), 1), GridVisualType.RedSoft);
                break;

            case InteractAction:
                gridVisualType = GridVisualType.Blue;
                break;
        }

        ShowAndMark(selectedAction.GetValidGridPositionList(), gridVisualType);
    }

    private void UnitActionSystem_OnSelectedActionChanged(object sender, EventArgs e)
        => UpdateGridVisuals();

    private void LevelGrid_onAnyUnitMoveGridPosition(object sender, EventArgs e)
        => UpdateGridVisuals();

    private void UnitActionSystem_OnBusyChanged(object sender, bool e)
        => UpdateGridVisuals();

    private Material GetGridVisualTypeMaterial(GridVisualType gridVisualType)
    {
        foreach (GridVisualTypeMaterial m in gridVisualTypeMaterialList)
            if (m.gridVisualType == gridVisualType)
                return m.material;

        Debug.LogError("Could not find GridVisualTypeMaterial for GridVisualType " + gridVisualType);
        return null;
    }

    private void HandleTeamVisionChanged(int teamId)
    {
        if (!teamVisionEnabled) return;

        int myTeam = GetLocalPlayerTeamId();
        if (teamId != myTeam)
        {
            return;
        }

        UpdateGridVisuals();
    }

    private void DrawTeamVisionOverlay()
    {
        if (TeamVisionService.Instance == null) return;

        int myTeam = GetLocalPlayerTeamId();
        var snap = TeamVisionService.Instance.GetVisibleTilesSnapshot(myTeam);
        if (snap == null) return;

        _tmpList.Clear();
        foreach (var gp in snap)
            if (!_lastActionCells.Contains(gp) && (!filterToMousePlanes || HasMousePlaneAt(gp)))
                _tmpList.Add(gp);

        ShowGridPositionList(_tmpList, teamVisionType);
    }

    private void ShowAndMark(IEnumerable<GridPosition> cells, GridVisualType type)
    {
        var mat = GetGridVisualTypeMaterial(type);
        foreach (var gp in cells)
        {
            if (filterToMousePlanes && !HasMousePlaneAt(gp)) continue;
            var cell = gridSystemVisualSingleArray[gp.x, gp.z, gp.floor];
            if (cell != null) cell.Show(mat);
            _lastActionCells.Add(gp);
        }
    }

    private List<GridPosition> BuildRangeSquare(GridPosition center, int range)
    {
        var list = new List<GridPosition>();
        for (int x = -range; x <= range; x++)
        for (int z = -range; z <= range; z++)
        {
            var gp = center + new GridPosition(x, z, 0);
            if (LevelGrid.Instance.IsValidGridPosition(gp))
                list.Add(gp);
        }
        return list;
    }
}
