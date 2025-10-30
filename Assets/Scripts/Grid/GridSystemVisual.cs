
using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

[DefaultExecutionOrder(-100)]
public class GridSystemVisual : MonoBehaviour
{
    public static GridSystemVisual Instance { get; private set; }

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
        {
            for (int z = 0; z < LevelGrid.Instance.GetHeight(); z++)
            {
                for (int floor = 0; floor < LevelGrid.Instance.GetFloorAmount(); floor++)
                {
                    GridPosition gridPosition = new(x, z, floor);
                    Transform gridSystemVisualSingleTransform = Instantiate(gridSystemVisualSinglePrefab, LevelGrid.Instance.GetWorldPosition(gridPosition), Quaternion.identity);
                    gridSystemVisualSingleArray[x, z, floor] = gridSystemVisualSingleTransform.GetComponent<GridSystemVisualSingle>();
                }
            }
        }

        UnitActionSystem.Instance.OnSelectedActionChanged += UnitActionSystem_OnSelectedActionChanged;
        UnitActionSystem.Instance.OnBusyChanged += UnitActionSystem_OnBusyChanged;
        LevelGrid.Instance.onAnyUnitMoveGridPosition += LevelGrid_onAnyUnitMoveGridPosition;

        if (TeamVisionService.Instance != null)
            TeamVisionService.Instance.OnTeamVisionChanged += HandleTeamVisionChanged;

        // Tyhjennä vision alussa verkkopelit varten - estää toisen tiimin visionit jäämästä näkyviin
        if (Mirror.NetworkClient.active && TeamVisionService.Instance != null)
        {
            int myTeam = GetLocalPlayerTeamId();
            TeamVisionService.Instance.ClearTeamVision(myTeam);
        }

        UpdateGridVisuals();

    }
    
    void OnDisable()
    {
        UnitActionSystem.Instance.OnSelectedActionChanged -= UnitActionSystem_OnSelectedActionChanged;
        UnitActionSystem.Instance.OnBusyChanged -= UnitActionSystem_OnBusyChanged;
        LevelGrid.Instance.onAnyUnitMoveGridPosition -= LevelGrid_onAnyUnitMoveGridPosition;

        if (TeamVisionService.Instance != null)
            TeamVisionService.Instance.OnTeamVisionChanged -= HandleTeamVisionChanged; 
    }
    
    private int GetLocalPlayerTeamId()
    {
        GameMode mode = GameModeManager.SelectedMode;
        
        if (mode == GameMode.SinglePlayer || mode == GameMode.CoOp)
        {
            return 0;
        }
        
        if (mode == GameMode.Versus)
        {
            if (Mirror.NetworkServer.active && !Mirror.NetworkClient.active)
            {
                Debug.LogWarning("[GridSystemVisual] Running on dedicated server - no local player team");
                return 0;
            }
            
            if (Mirror.NetworkClient.localPlayer != null)
            {
                var localPlayerUnit = Mirror.NetworkClient.localPlayer.GetComponent<Unit>();
                if (localPlayerUnit != null)
                {
                    bool isHost = Mirror.NetworkServer.active;
                    int teamId = isHost ? 0 : 1;
                    Debug.Log($"[GridSystemVisual] Versus mode - Local player is {(isHost ? "Host" : "Client")}, Team ID: {teamId}");
                    return teamId;
                }
            }
            
            bool fallbackIsHost = Mirror.NetworkServer.active;
            int fallbackTeam = fallbackIsHost ? 0 : 1;
            Debug.Log($"[GridSystemVisual] Versus mode fallback - IsHost: {fallbackIsHost}, Team ID: {fallbackTeam}");
            return fallbackTeam;
        }
        
        return 0;
    }

    public void HideAllGridPositions()
    {
        for (int x = 0; x < LevelGrid.Instance.GetWidth(); x++)
        {
            for (int z = 0; z < LevelGrid.Instance.GetHeight(); z++)
            {
                for (int floor = 0; floor < LevelGrid.Instance.GetFloorAmount(); floor++)
                {
                    gridSystemVisualSingleArray[x, z, floor].Hide();
                }
            }
        }
    }

    public void ShowGridPositionList(List<GridPosition> gridPositionList, GridVisualType gridVisualType)
    {
        foreach (GridPosition gridPosition in gridPositionList)
        {
            if(gridSystemVisualSingleArray[gridPosition.x, gridPosition.z, gridPosition.floor] != null)
            {
                gridSystemVisualSingleArray[gridPosition.x, gridPosition.z, gridPosition.floor].
                Show(GetGridVisualTypeMaterial(gridVisualType));
            }
        }
    }

    private void UpdateGridVisuals()
    {
        HideAllGridPositions();
        _lastActionCells.Clear(); // <-- tärkeä: nollaa action-ruudut jokaisessa päivityksessä

        Unit selectedUnit = UnitActionSystem.Instance.GetSelectedUnit();
        if (selectedUnit == null) return;

        BaseAction selectedAction = UnitActionSystem.Instance.GetSelectedAction();

        GridVisualType gridVisualType;

        switch (selectedAction)
        {
            default:
            case MoveAction moveAction:
                gridVisualType = GridVisualType.white;
                break;

            case TurnTowardsAction _:
                gridVisualType = GridVisualType.Blue;
                break;

            case ShootAction shoot:
            {
                gridVisualType = GridSystemVisual.GridVisualType.Red;

                var origin = selectedUnit.GetGridPosition();
                int range  = shoot.GetMaxShootDistance();

                var cfg = LoSConfig.Instance;
                var visible = RaycastVisibility.ComputeVisibleTilesRaycast(
                    origin, range,
                    cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU
                );
                visible.RemoveWhere(gp => !RaycastVisibility.HasLineOfSightRaycastHeightAware(
                    origin, gp, cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU));

                // Ammunnan lisä-overlay (pehmeä punainen) lasketaan action-ruuduiksi
                _tmpList.Clear();
                _tmpList.AddRange(visible);
                ShowAndMark(_tmpList, GridVisualType.RedSoft);
                break;
            }

            case GranadeAction _:
                gridVisualType = GridVisualType.Yellow;
                break;

            case MeleeAction _:
                gridVisualType = GridVisualType.Red;
                // 1 ruudun pehmennys ympärille on myös action-overlay
                ShowAndMark(BuildRangeSquare(selectedUnit.GetGridPosition(), 1), GridVisualType.RedSoft);
                break;

            case InteractAction _:
                gridVisualType = GridVisualType.Blue;
                break;
        }

        // Päälista: valitun actionin validit kohderuudut → aina action-ruutuja
        ShowAndMark(selectedAction.GetValidGridPositionList(), gridVisualType);

        // Team-vision: piirrä vain niihin ruutuihin, joissa EI ole action-overlayta
        if (teamVisionEnabled && TeamVisionService.Instance != null)
        {
            DrawTeamVisionOverlayExcludingAction();
        }
    }

    private void UnitActionSystem_OnSelectedActionChanged(object sender, EventArgs e)
    {
        UpdateGridVisuals();
    }

    private void LevelGrid_onAnyUnitMoveGridPosition(object sender, EventArgs e)
    {
        UpdateGridVisuals();
    }

    private void UnitActionSystem_OnBusyChanged(object sender, bool e)
    {
        UpdateGridVisuals();
    }

    private Material GetGridVisualTypeMaterial(GridVisualType gridVisualType)
    {
        foreach (GridVisualTypeMaterial gridVisualTypeMaterial in gridVisualTypeMaterialList)
        {
            if (gridVisualTypeMaterial.gridVisualType == gridVisualType)
            {
                return gridVisualTypeMaterial.material;
            }
        }
        Debug.LogError("Cloud not find GridVisualTypeMaterial for GridVisualType" + gridVisualType);
        return null;
    }

    private void HandleTeamVisionChanged(int teamId)
    {
        if (!teamVisionEnabled) return;
        
        int myTeam = GetLocalPlayerTeamId();
        if (teamId != myTeam)
        {
            Debug.Log($"[GridSystemVisual] Ignoring vision update for team {teamId} (not my team {myTeam})");
            return;
        }
        
        UpdateGridVisuals();
    }

    private void DrawTeamVisionOverlayExcludingAction()
    {
        if (TeamVisionService.Instance == null) return;

        int myTeam = GetLocalPlayerTeamId();
        var snap = TeamVisionService.Instance.GetVisibleTilesSnapshot(myTeam);
        if (snap == null) return;

        _tmpList.Clear();
        foreach (var gp in snap)
            if (!_lastActionCells.Contains(gp))
                _tmpList.Add(gp);

        ShowGridPositionList(_tmpList, teamVisionType);
    }

    // Näytä ja merkitse ruudut "action-ruuduiksi" jotta vision ei piirry niiden päälle
    private void ShowAndMark(IEnumerable<GridPosition> cells, GridVisualType type)
    {
        var mat = GetGridVisualTypeMaterial(type);
        foreach (var gp in cells)
        {
            if(gridSystemVisualSingleArray[gp.x, gp.z, gp.floor] != null)
            {
                gridSystemVisualSingleArray[gp.x, gp.z, gp.floor].Show(mat); 
            }

            _lastActionCells.Add(gp);
        }
    }

    // Tarvitaan esim. lähietäisyyden "pehmennykseen" (melee)
    private List<GridPosition> BuildRangeSquare(GridPosition center, int range)
    {
        var list = new List<GridPosition>();
        for (int x = -range; x <= range; x++)
        {
            for (int z = -range; z <= range; z++)
            {
                var gp = center + new GridPosition(x, z, 0);
                if (LevelGrid.Instance.IsValidGridPosition(gp))
                    list.Add(gp);
            }
        }
        return list;
    }
}
