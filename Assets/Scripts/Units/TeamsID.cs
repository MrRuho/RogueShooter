using UnityEngine;

public class TeamsID : MonoBehaviour
{
    public static int CurrentTurnTeamId()
    {
        if (GameModeManager.SelectedMode == GameMode.Versus)
        {
            // Muunna PVP:n currentOwnerNetId -> teamId
            uint ownerNetId = PvPTurnCoordinator.Instance.currentOwnerNetId;
            return NetworkSync.IsOwnerHost(ownerNetId) ? 0 : 1; // host=0, client=1
        }
        // SP/Coop: TurnSystem pitää tiimin valmiina
        return (int)TurnSystem.Instance.CurrentTeam;
    }
    
}
