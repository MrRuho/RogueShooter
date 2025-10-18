using System;
using System.Collections.Generic;
using UnityEngine;

public class MeleeAction : BaseAction
{
    public static event EventHandler OnAnyMeleeActionHit;

    public event EventHandler OnMeleeActionStarted;
    public event EventHandler OnMeleeActionCompleted;
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
                if (targetUnit != null)
                {
                    if (RotateTowards(targetUnit.GetWorldPosition()))
                    {
                        stateTimer = Mathf.Min(stateTimer, 0.4f);
                    }
                }
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
                float afterHitStateTime = 1f;
                stateTimer = afterHitStateTime;
                ApplyHit(damage, targetUnit, true);
                OnAnyMeleeActionHit?.Invoke(this, EventArgs.Empty);
                break;
            case State.MeleeActionAfterHit:
                OnMeleeActionCompleted?.Invoke(this, EventArgs.Empty);
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
        var valid = new List<GridPosition>();
        GridPosition origin = unit.GetGridPosition();

        for (int dx = -maxMeleedDistance; dx <= maxMeleedDistance; dx++)
        {
            for (int dz = -maxMeleedDistance; dz <= maxMeleedDistance; dz++)
            {
                if (dx == 0 && dz == 0) continue; // ei itseään

                var gp = origin + new GridPosition(dx, dz, 0);

                // 1) RAJAT ENSIN -> estää out-of-range -virheen
                if (!LevelGrid.Instance.IsValidGridPosition(gp)) continue;

                // Manhattan -> sulkee diagonaalit
                // if (Mathf.Abs(dx) + Mathf.Abs(dz) > maxMeleedDistance) continue;

                // Chebyshev -> sallii diagonaalit
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) > maxMeleedDistance) continue;

                // 2) onko ruudussa ketään?
                if (!LevelGrid.Instance.HasAnyUnitOnGridPosition(gp)) continue;

                var target = LevelGrid.Instance.GetUnitAtGridPosition(gp);
                if (target == null || target == unit) continue;           // varmistus
                if (target.IsEnemy() == unit.IsEnemy()) continue;         // ei omia

                valid.Add(gp);
            }
        }
        return valid;
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        targetUnit = LevelGrid.Instance.GetUnitAtGridPosition(gridPosition);

        state = State.MeleeActionBeforeHit;
        float beforeHitStateTime = 0.7f;
        stateTimer = beforeHitStateTime;
        OnMeleeActionStarted?.Invoke(this, EventArgs.Empty);
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
