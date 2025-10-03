using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GranadeAction : BaseAction
{
    public event EventHandler ThrowGranade;

    public event EventHandler ThrowReady;

    public Vector3 TargetWorld { get; private set; }

    [SerializeField] private Transform grenadeProjectilePrefab;

    private int maxThrowDistance = 7;

    private void Update()
    {
        if (!isActive)
        {
            return;
        }
    }

    public override string GetActionName()
    {
        return "Granade";
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = 0,

        };
    }

    public override List<GridPosition> GetValidGridPositionList()
    {

        List<GridPosition> validGridPositionList = new();

        GridPosition unitGridPosition = unit.GetGridPosition();

        for (int x = -maxThrowDistance; x <= maxThrowDistance; x++)
        {
            for (int z = -maxThrowDistance; z <= maxThrowDistance; z++)
            {
                GridPosition offsetGridPosition = new(x, z, 0);
                GridPosition testGridPosition = unitGridPosition + offsetGridPosition;

                // Check if the test grid position is within the valid range
                if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                int testDistance = Mathf.Abs(x) + Mathf.Abs(z);
                if (testDistance > maxThrowDistance) continue;

                validGridPositionList.Add(testGridPosition);
            }

        }

        return validGridPositionList;
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {

        ActionStart(onActionComplete);
        TargetWorld = LevelGrid.Instance.GetWorldPosition(gridPosition);
        StartCoroutine(TurnAndThrow(1f, TargetWorld));
    

    }

    private IEnumerator TurnAndThrow(float delay, Vector3 targetWorld)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            // Käänny kohti targettia koko viiveen ajan
            RotateTowards(targetWorld);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ThrowGranade?.Invoke(this, EventArgs.Empty);
    }

    public void OnGrenadeBehaviourComplete()
    {
        ThrowReady?.Invoke(this, EventArgs.Empty);
        ActionComplete();
    }
}
