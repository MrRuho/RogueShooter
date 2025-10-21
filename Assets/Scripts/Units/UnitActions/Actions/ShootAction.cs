using System;
using System.Collections.Generic;
using UnityEngine;

public class ShootAction : BaseAction
{

    public static event EventHandler<OnShootEventArgs> OnAnyShoot;
    
    public event EventHandler<OnShootEventArgs> OnShoot;

    public class OnShootEventArgs : EventArgs
    {
        public Unit targetUnit;
        public Unit shootingUnit;
    }

    private enum State
    {
        Aiming,
        Shooting,
        Cooloff
    }

    [SerializeField] private LayerMask obstaclesLayerMask;
    private State state;
    [SerializeField] private WeaponDefinition weapon;

    private float stateTimer;
    private Unit targetUnit;
    private bool canShootBullet;

    // Update is called once per frame
    void Update()
    {
        if (!isActive) return;

        stateTimer -= Time.deltaTime;
        switch (state)
        {
            case State.Aiming:
                if (targetUnit != null)
                {
                    if (RotateTowards(targetUnit.GetWorldPosition()))
                    {
                        stateTimer = Mathf.Min(stateTimer, 0.4f);
                    }
                 }
                break;
            case State.Shooting:
                if (canShootBullet)
                {
                    Shoot();
                    canShootBullet = false;

                }
                break;
            case State.Cooloff:
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
            case State.Aiming:
                state = State.Shooting;
                float shootingStateTime = 0.1f;
                stateTimer = shootingStateTime;
                break;
            case State.Shooting:
                state = State.Cooloff;
                float cooloffStateTime = 0.5f;
                stateTimer = cooloffStateTime;
                break;
            case State.Cooloff:
                ActionComplete();
                break;
        }
    }

    private void Shoot()
    {
        OnAnyShoot?.Invoke(this, new OnShootEventArgs
        {
            targetUnit = targetUnit,
            shootingUnit = unit
        });

        OnShoot?.Invoke(this, new OnShootEventArgs
        {
            targetUnit = targetUnit,
            shootingUnit = unit
        });

        // Laske tulos
        var result = ShootingResolver.Resolve(unit, targetUnit, weapon);

        // Debug: näe mihin kategoriaan osui
        Debug.Log($"[{unit.name}] → [{targetUnit.name}] | {result.tier} | dmg:{result.damage}");

        switch (result.tier)
        {
            case ShotTier.CritMiss:
                // Täysi huti – ei vaikutusta
                Debug.Log("Critical miss! Bullet flies off wildly.");
                return;

            case ShotTier.Miss:
                // Luo painetta ja vähentää henkilökohtaista suojaa mutta ei voi kuitenkaan osua.
                ApplyHit(result.damage, targetUnit, false);
                return;

            case ShotTier.Graze:
                // Luo painetta ja vähentää henkilökohtaista suojaa ja voi aiheuttaa
                // pientävahinkoa jos suoja on kulunut loppuun.
                // Jos suojaa ei ole ollenkaan niin tämäkin lasketaan suoraksi osumaksi.
                if (GetCoverType(targetUnit) == CoverService.CoverType.None)
                {
                    MakeDamage(result.damage + weapon.NoCoverDamageBonus, targetUnit);
                    return;
                }
                
                ApplyHit(result.damage, targetUnit, false);
                return;

            case ShotTier.Hit:
                // Vähentää ensin suojaa ja sitten osuu kokovahingolla.
                // Myös suojasta jäljelle jäänyt vahinko menee vahinkoon.
                if (GetCoverType(targetUnit) == CoverService.CoverType.None)
                {
                    MakeDamage(result.damage + weapon.NoCoverDamageBonus, targetUnit);
                    return;
                }

                ApplyHit(result.damage, targetUnit, false);
                return;

            case ShotTier.Crit:
                // Kriittinen osuma vähentää suojaa sekä menee suoraan läpi tehden vahinkoa.
                if (GetCoverType(targetUnit) == CoverService.CoverType.None)
                {
                    ApplyHit(result.damage + weapon.NoCoverDamageBonus, targetUnit, false);
                    return;
                }
                Debug.Log("Critical hit!");
                ApplyHit(result.damage, targetUnit, false);
                return;
        }

    }

    public override int GetActionPointsCost()
    {
        return 1;
    }

    public override string GetActionName()
    {
        return "Shoot";
    }
 
    public  List<GridPosition> GetValidActionGridPositionList(GridPosition unitGridPosition)
    {
        List<GridPosition> validGridPositionList = new();

        for (int x = -weapon.maxShootRange; x <= weapon.maxShootRange; x++)
        {
            for (int z = -weapon.maxShootRange; z <= weapon.maxShootRange; z++)
            {
                for (int floor = -weapon.maxShootRange; floor <= weapon.maxShootRange; floor++)
                {
                    GridPosition offsetGridPosition = new(x, z, floor);
                    GridPosition testGridPosition = unitGridPosition + offsetGridPosition;

                    // Check if the test grid position is within the valid range and not occupied by another unit
                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
             
                    int cost = SircleCalculator.Sircle(x, z);

                    if (cost > 10 * weapon.maxShootRange) continue;
                    if (!LevelGrid.Instance.HasAnyUnitOnGridPosition(testGridPosition)) continue;

                    Unit targetUnit = LevelGrid.Instance.GetUnitAtGridPosition(testGridPosition);
                    if (targetUnit == null) continue;
                    // Make sure we don't include friendly units.
                    if (targetUnit.IsEnemy() == unit.IsEnemy()) continue;

                    Vector3 unitWorldPosition = LevelGrid.Instance.GetWorldPosition(unitGridPosition);
                    Vector3 shootDir = (targetUnit.GetWorldPosition() - unitWorldPosition).normalized;
                    float unitShoulderHeight = 2.5f;
                    if (Physics.Raycast(
                        unitWorldPosition + Vector3.up * unitShoulderHeight,
                        shootDir,
                        Vector3.Distance(unitWorldPosition, targetUnit.GetWorldPosition()),
                        obstaclesLayerMask))
                    {
                        //Target Unit is Blocked by an Obstacle
                        continue;
                    }                    
                    validGridPositionList.Add(testGridPosition);
                }
            }
        }

        return validGridPositionList;
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {

        targetUnit = LevelGrid.Instance.GetUnitAtGridPosition(gridPosition);

        state = State.Aiming;
        float aimingStateTime = 1f;
        stateTimer = aimingStateTime;

        canShootBullet = true;

        ActionStart(onActionComplete);
    }

    public Unit GetTargetUnit()
    {
        return targetUnit;
    }

    public int GetMaxShootDistance()
    {
        return weapon.maxShootRange;
    }

    /// ---------------- AI ----------------
    /// <summary>
    /// ENEMY AI: Make a list about Player Units what Enemy Unit can shoot. 
    /// </summary>
    public override List<GridPosition> GetValidGridPositionList()
    {
        GridPosition unitGridPosition = unit.GetGridPosition();
        return GetValidActionGridPositionList(unitGridPosition);
    }

    /// <summary>
    /// ENEMY AI: How "good" target is. Target who have a lowest health, gets a higher actionvalue
    /// </summary>
    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        Unit targetUnit = LevelGrid.Instance.GetUnitAtGridPosition(gridPosition);

        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = 100 + Mathf.RoundToInt((1 - targetUnit.GetHealthNormalized()) * 100f), //Take at target who have a lowest health.
        };
    }

    public int GetTargetCountAtPosition(GridPosition gridPosition)
    {
        return GetValidActionGridPositionList(gridPosition).Count;
    }
}
