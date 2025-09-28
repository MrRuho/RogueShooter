using System;
using System.Collections.Generic;
using UnityEngine;

public class MeleeAction : BaseAction
{
    [SerializeField] private int damage = 100;

    private enum State
    {
        MeleeActionBeforeHit,
        MeleeActionAfterHit,
    }
    private int maxMeleedDistance = 1;
    private State state;
    private float stateTimer;
    private Unit targetUnit;

    private void Update()
    {
        if (!isActive)
        {
            return;
        }
        stateTimer -= Time.deltaTime;
        switch (state)
        {
            case State.MeleeActionBeforeHit:

                RotateTowards(targetUnit.GetWorldPosition());
                break;
            case State.MeleeActionAfterHit:
                break;
        }

        if (stateTimer <= 0f)
        {
            NextState();
        }
    }

    private void NextState()
    {
        switch (state)
        {
            case State.MeleeActionBeforeHit:
                state = State.MeleeActionAfterHit;
                float afterHitStateTime = 0.5f;
                stateTimer = afterHitStateTime;
                MakeDamage(damage, targetUnit);
                break;
            case State.MeleeActionAfterHit:
                ActionComplete();
                break;
        }
    }

    public override string GetActionName()
    {
        return "Melee";
    }

    public override List<GridPosition> GetValidGridPositionList()
    {
        List<GridPosition> validGridPositionList = new();

        GridPosition unitGridPosition = unit.GetGridPosition();

        for (int x = -maxMeleedDistance; x <= maxMeleedDistance; x++)
        {
            for (int z = -maxMeleedDistance; z <= maxMeleedDistance; z++)
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
        targetUnit = LevelGrid.Instance.GetUnitAtGridPosition(gridPosition);

        state = State.MeleeActionBeforeHit;
        float beforeHitStateTime = 0.7f;
        stateTimer = beforeHitStateTime;
        ActionStart(onActionComplete);
    }

    //-------------- ENEMY AI ACTIONS -------------
    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = 200,
        };
    }
}
