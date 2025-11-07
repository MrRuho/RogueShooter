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
        public ShotTier shotTier;  // ← UUSI

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

    private int currentBurstCount;
    private int maxBurstCount;

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
                    if (targetUnit == null)
                    {
                        state = State.Cooloff;
                        stateTimer = 0.1f;
                        canShootBullet = false;
                        return;
                    }

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
                if (targetUnit == null)
                {
                    ActionComplete();
                    return;
                }

                state = State.Shooting;
                currentBurstCount = 0;
                maxBurstCount = weapon.GetRandomBurstCount();
                
                // Ilmoita UnitAnimatorille burst-koko
                var unitAnimator = GetComponent<UnitAnimator>();
                if (unitAnimator != null)
                {
                    unitAnimator.NotifyBurstStart(maxBurstCount);
                }
                
                float shootingStateTime = 0.1f;
                stateTimer = shootingStateTime;
                break;
            case State.Shooting:
                currentBurstCount++;
                
                if (targetUnit == null)
                {
                    state = State.Cooloff;
                    stateTimer = 0.1f;
                    return;
                }
                
                if (currentBurstCount < maxBurstCount)
                {
                    canShootBullet = true;
                    stateTimer = weapon.burstShotDelay;
                }
                else
                {
                    state = State.Cooloff;
                    float cooloffStateTime = 0.5f;
                    stateTimer = cooloffStateTime;
                }
                break;
            case State.Cooloff:
                ActionComplete();
                break;
        }
    }

    private void Shoot()
    {
        if (targetUnit == null)
        {
            return;
        }

        var result = ShootingResolver.Resolve(unit, targetUnit, weapon);
     //   Debug.Log($"[{unit.name}] → [{targetUnit.name}] | {result.tier} | dmg:{result.damage} | Burst: {currentBurstCount + 1}/{maxBurstCount}");

        OnAnyShoot?.Invoke(this, new OnShootEventArgs
        {
            targetUnit = targetUnit,
            shootingUnit = unit,
            shotTier = result.tier  // ← UUSI
        });

        OnShoot?.Invoke(this, new OnShootEventArgs
        {
            targetUnit = targetUnit,
            shootingUnit = unit,
            shotTier = result.tier  // ← UUSI
        });

        switch (result.tier)
        {
            case ShotTier.CritMiss:
            //    Debug.Log("Critical miss! Bullet flies off wildly.");
                return;

            case ShotTier.Close:
                ApplyHit(result.damage, result.bypassCover, result.coverOnly, targetUnit);
            //    Debug.Log("Close! Cover Damage only: " + result.damage);
                return;

            case ShotTier.Graze:
                if (GetCoverType(targetUnit) == CoverService.CoverType.None)
                {
                    MakeDamage(result.damage + weapon.NoCoverDamageBonus, targetUnit);
             //       Debug.Log("Graze! No cover damage: " + result.damage);
                    return;
                }

                ApplyHit(result.damage, result.bypassCover, result.coverOnly, targetUnit);
           //     Debug.Log("Graze! Damage: " + result.damage);
                return;

            case ShotTier.Hit:
                if (GetCoverType(targetUnit) == CoverService.CoverType.None)
                {
                    MakeDamage(result.damage + weapon.NoCoverDamageBonus, targetUnit);
               //     Debug.Log("Hit! No Cover Damage: " + result.damage);
                    return;
                }

                ApplyHit(result.damage, result.bypassCover, result.coverOnly, targetUnit);
              //  Debug.Log("Hit! Damage: " + result.damage);
                return;

            case ShotTier.Crit:
                if (GetCoverType(targetUnit) == CoverService.CoverType.None)
                {
                    MakeDamage(result.damage + weapon.NoCoverDamageBonus, targetUnit);
                 //   Debug.Log("Critical Hit! No Cover Damage: " + result.damage);
                    return;
                }
                ApplyHit(result.damage, result.bypassCover, result.coverOnly, targetUnit);
              //  Debug.Log("Critical Hit! Damage: " + result.damage);
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
   
    public List<GridPosition> GetValidActionGridPositionList(GridPosition unitGridPosition)
    {
        var res = new List<GridPosition>();
        int r = weapon.maxShootRange;

        var cfg = LoSConfig.Instance;
        foreach (var enemy in EnumerateEnemyCandidatesInRange(unitGridPosition, r))
        {
            var gp = enemy.GetGridPosition();
            if (!RaycastVisibility.HasLineOfSightRaycastHeightAware(
                unitGridPosition, gp,
                cfg.losBlockersMask, cfg.eyeHeight, cfg.samplesPerCell, cfg.insetWU))
            continue;

            res.Add(gp);
        }
        return res;
    }

    private IEnumerable<Unit> EnumerateEnemyCandidatesInRange(GridPosition origin, int range)
    {
        bool shooterIsEnemy = unit.IsEnemy();
        foreach (var u in UnitManager.Instance.GetAllUnitList())
        {
            if (u == null) continue;
            if (u.IsEnemy() == shooterIsEnemy) continue;
            var gp = u.GetGridPosition();
            if (gp.floor != origin.floor) continue;
            int cost = SircleCalculator.Sircle(gp.x - origin.x, gp.z - origin.z);
            if (cost > 10 * range) continue;
            yield return u;
        }
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

    public override List<GridPosition> GetValidGridPositionList()
    {
        GridPosition unitGridPosition = unit.GetGridPosition();
        return GetValidActionGridPositionList(unitGridPosition);
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        Unit targetUnit = LevelGrid.Instance.GetUnitAtGridPosition(gridPosition);

        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = 100 + Mathf.RoundToInt((1 - targetUnit.GetHealthNormalized()) * 100f),
        };
    }

    public int GetTargetCountAtPosition(GridPosition gridPosition)
    {
        return GetValidActionGridPositionList(gridPosition).Count;
    }
}
