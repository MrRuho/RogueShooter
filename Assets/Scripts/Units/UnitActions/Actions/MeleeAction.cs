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
                    if (RotateTowards(targetUnit.GetWorldPosition(), 750))
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
                ApplyHit(damage, false, false, targetUnit, true);
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

    public int GetMeleeDistance()
    {
        return maxMeleedDistance;
    }

    public override List<GridPosition> GetValidGridPositionList()
    {
        var valid = new List<GridPosition>();
        var lg = LevelGrid.Instance;
        GridPosition origin = unit.GetGridPosition();

        var cfg = LoSConfig.Instance; // losBlockersMask, eyeHeight, samplesPerCell, insetWU

        for (int dx = -maxMeleedDistance; dx <= maxMeleedDistance; dx++)
        {
            for (int dz = -maxMeleedDistance; dz <= maxMeleedDistance; dz++)
            {
                if (dx == 0 && dz == 0) continue;

                var gp = origin + new GridPosition(dx, dz, 0);
                if (!lg.IsValidGridPosition(gp)) continue;

                // Chebyshev (diagonaalit sallittu) – pidä tämä jos haluat lyönnin myös vinottain
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) > maxMeleedDistance) continue;

                if (!lg.HasAnyUnitOnGridPosition(gp)) continue;

                var target = lg.GetUnitAtGridPosition(gp);
                if (target == null || target == unit) continue;
                if (target.IsEnemy() == unit.IsEnemy()) continue;

                // UUSI: LoS tarkistus – käyttää samaa maskia ja eyeHeightia kuin Shoot
                bool clear = RaycastVisibility.HasLineOfSightRaycast(
                    origin, gp, cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU
                );

                if (!clear) continue;   // korkea seinä välissä => ei kelpaa

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
