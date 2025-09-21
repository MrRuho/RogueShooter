using System;
using System.Collections.Generic;
using UnityEngine;



/// <summary>
///     This class is responsible for spinning a unit around its Y-axis.
/// </summary>
/// remarks>
///     Change to turn towards the direction the mouse is pointing
/// </remarks>

public class SpinAction : BaseAction
{

    private float totalSpinAmount = 0f;
    private void Update()
    {
        if (!isActive) return;

        // Aja paikallisesti vain SinglePlayerissa tai jos tämä instanssi on serveri (host)
        bool driveHere = GameModeManager.SelectedMode == GameMode.SinglePlayer || isServer;
        if (!driveHere) return;

        float spinAddAmmount = 360f * Time.deltaTime;
        transform.eulerAngles += new Vector3(0, spinAddAmmount, 0);

        totalSpinAmount += spinAddAmmount;
        if (totalSpinAmount >= 360f)
        {
            ActionComplete();
        }

    }
    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        totalSpinAmount = 0f;
        ActionStart(onActionComplete);
    }

    public override string GetActionName()
    {
        return "Spin";
    }

    public override List<GridPosition> GetValidGridPositionList()
    {

        GridPosition unitGridPosition = unit.GetGridPosition();

        return new List<GridPosition>()
        {
            unitGridPosition
        };
    }

    public override int GetActionPointsCost()
    {
        return 1;
    }

    /// <summary>
    /// ENEMY AI: 
    /// Currently this action has no value. Just testing!
    /// </summary>
    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = 0,

        };
    }
}
