using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;




/// <summary>
///     This class is responsible for spinning a unit around its Y-axis.
/// </summary>
/// remarks>
///     Change to turn towards the direction the mouse is pointing
/// </remarks>

public class TurnTowardsAction : BaseAction
{
    private enum State
    {
        StartTurning,
        EndTurning,
    }
     private State state;
    public Vector3 TargetWorld { get; private set; }

    private float stateTimer;
    GridPosition gridPosition;

    private void Update()
    {
        if (!isActive)
        {
            return;
        }
        stateTimer -= Time.deltaTime;
        switch (state)
        {
            case State.StartTurning:
                TargetWorld = LevelGrid.Instance.GetWorldPosition(gridPosition);
                RotateTowards(TargetWorld);
                break;
            case State.EndTurning:
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
            case State.StartTurning:
                state = State.EndTurning;
                float afterTurnStateTime = 0.5f;
                stateTimer = afterTurnStateTime;

                break;
            case State.EndTurning:
                ActionComplete();
                break;
        }
    }
    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        this.gridPosition = gridPosition;        
        state = State.StartTurning;
        float beforeTurnStateTime = 0.7f;
        stateTimer = beforeTurnStateTime;
        ActionStart(onActionComplete);
    }
  
    public override string GetActionName()
    {
        return "Turn";
    }

    public override List<GridPosition> GetValidGridPositionList()
    {
        List<GridPosition> validGridPositionList = new();

        GridPosition unitGridPosition = unit.GetGridPosition();

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                GridPosition offsetGridPosition = new(x, z, 0);
                GridPosition testGridPosition = unitGridPosition + offsetGridPosition;
                validGridPositionList.Add(testGridPosition);
            }
        }

        return validGridPositionList;
    }

    public override int GetActionPointsCost()
    {
        return 100;
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
