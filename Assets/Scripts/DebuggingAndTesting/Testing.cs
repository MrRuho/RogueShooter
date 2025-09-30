using UnityEngine;

/// <summary>
/// This class is responsible for testing the grid system and unit actions in the game.
/// It provides functionality to visualize the grid positions and interact with unit actions.
/// </summary>
public class Testing : MonoBehaviour
{
    
    [SerializeField] private Unit unit;
    private void Start()
    {
     
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {

            // ScreenShake.Instance.Shake(5f);

           // ScreenShake.Instance.RecoilCameraShake();

            //Show pathfind line
            /*
            GridPosition mouseGridPosition = LevelGrid.Instance.GetGridPosition(MouseWorld.GetMouseWorldPosition());
            GridPosition startGridPosition = new GridPosition(0, 0);

            List<GridPosition> gridPositionList = PathFinding.Instance.FindPath(startGridPosition, mouseGridPosition);

            for (int i = 0; i < gridPositionList.Count - 1; i++)
            {
                Debug.DrawLine(
                    LevelGrid.Instance.GetWorldPosition(gridPositionList[i]),
                    LevelGrid.Instance.GetWorldPosition(gridPositionList[i + 1]),
                    Color.white,
                    10f
                );
            }
            */
        }

        //Resetoi pelin alkamaan alusta.
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (Mirror.NetworkServer.active) {
                ResetService.Instance.HardResetServerAuthoritative();
            } else if (Mirror.NetworkClient.active) {
                // käskytä serveriä
                ResetService.Instance.CmdRequestHardReset();
            } else {
                GameReset.HardReloadSceneKeepMode();
            }
        }

    }
}
