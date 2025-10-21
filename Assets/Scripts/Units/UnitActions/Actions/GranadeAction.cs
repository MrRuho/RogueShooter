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
        int range = unit.archetype.throwingRange;
        for (int x = -range; x <= range; x++)
        {
            for (int z = -range; z <= range; z++)
            {
                GridPosition offsetGridPosition = new(x, z, 0);
                GridPosition testGridPosition = unitGridPosition + offsetGridPosition;

                // Check if the test grid position is within the valid range
                if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
 
                int cost = SircleCalculator.Sircle(x, z);
                if (cost > 10 * range) continue;

                validGridPositionList.Add(testGridPosition);
            }
        }

        return validGridPositionList;
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        GetUnit().UseGrenade();
        ActionStart(onActionComplete);
        TargetWorld = LevelGrid.Instance.GetWorldPosition(gridPosition);
        StartCoroutine(TurnAndThrow(.5f, TargetWorld));
    }

    private IEnumerator TurnAndThrow(float delay, Vector3 targetWorld)
    {
        // Odotetaan kunnes RotateTowards palaa true
        float waitAfterAligned = 0.1f; // pienen odotuksen verran
        float alignedTime = 0f;
        
        while (true)
        {
            bool aligned = RotateTowards(targetWorld);

            if (aligned)
            {
                alignedTime += Time.deltaTime;
                if (alignedTime >= waitAfterAligned)
                    break; // ollaan kohdistettu ja odotettu tarpeeksi
            }
            else
            {
                alignedTime = 0f; // resetoi jos ei vielä kohdallaan
            }

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
