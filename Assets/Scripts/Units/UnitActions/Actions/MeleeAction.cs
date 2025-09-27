using System;
using System.Collections.Generic;
using UnityEngine;

public class MeleeAction : BaseAction
{
    private int maxSwordDistance = 1;

    private void Update()
    {
        if (!isActive)
        {
            return;
        }

        ActionComplete();
    }

    public override string GetActionName()
    {
        return "Melee";
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = 200,
        };
    }

    public override List<GridPosition> GetValidGridPositionList()
    {
        List<GridPosition> validGridPositionList = new();

        GridPosition unitGridPosition = unit.GetGridPosition();

        for (int x = -maxSwordDistance; x <= maxSwordDistance; x++)
        {
            for (int z = -maxSwordDistance; z <= maxSwordDistance; z++)
            {
                GridPosition offsetGridPosition = new(x, z);
                GridPosition testGridPosition = unitGridPosition + offsetGridPosition;
                
                if (!LevelGrid.Instance.HasAnyUnitOnGridPosition(testGridPosition)) continue;

                Unit targetUnit = LevelGrid.Instance.GetUnitAtGridPosition(testGridPosition);
                // Make sure we don't include friendly units.
                if (targetUnit.IsEnemy() == unit.IsEnemy()) continue;
                // Check if the test grid position is within the valid range
                if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;

                validGridPositionList.Add(testGridPosition);
            }
        }

        return validGridPositionList;
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        Debug.Log("Melee action taken at " + gridPosition);
        ActionStart(onActionComplete);
    }
}
