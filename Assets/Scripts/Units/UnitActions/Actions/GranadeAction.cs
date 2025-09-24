using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GranadeAction : BaseAction
{
    public event EventHandler ThrowGranade;

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
                GridPosition offsetGridPosition = new(x, z);
                GridPosition testGridPosition = unitGridPosition + offsetGridPosition;

                // Check if the test grid position is within the valid range and not occupied by another unit
                if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                int testDistance = Mathf.Abs(x) + Mathf.Abs(z);
                if (testDistance > maxThrowDistance) continue;
                //  if (!LevelGrid.Instance.HasAnyUnitOnGridPosition(testGridPosition)) continue;

                validGridPositionList.Add(testGridPosition);
            }

        }

        return validGridPositionList;
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        /*
            Transform grenadeProjectileTransform = Instantiate(grenadeProjectilePrefab, unit.GetWorldPosition(), Quaternion.identity);
            GrenadeProjectile grenadeProjectile = grenadeProjectileTransform.GetComponent<GrenadeProjectile>();
            grenadeProjectile.Setup(gridPosition, OnGrenadeBehaviourComplete);

            ActionStart(onActionComplete);
        */
        ActionStart(onActionComplete);

        TargetWorld = LevelGrid.Instance.GetWorldPosition(gridPosition);
        // Pyydä UnitAnimatoria hoitamaan visuaalit ja spawni
        ThrowGranade?.Invoke(this, EventArgs.Empty);

    }
    
    public void OnGrenadeBehaviourComplete()
    {
        ActionComplete();
    }
}
