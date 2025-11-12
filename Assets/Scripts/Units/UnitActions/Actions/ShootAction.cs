using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ShootAction : BaseAction
{
    public static event EventHandler<OnShootEventArgs> OnAnyShoot;
    public event EventHandler<OnShootEventArgs> OnShoot;

    [SyncVar] private bool isOverwatchShot;
    private Vector3 lastOverwatchFacing;

    public class OnShootEventArgs : EventArgs
    {
        public Unit targetUnit;
        public Unit shootingUnit;
        public ShotTier shotTier;
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

    private float CurrentTurnSpeed        => isOverwatchShot ? weapon.overwatch.turnSpeed        : weapon.normalShooting.turnSpeed;
    private float CurrentMinAimTime       => isOverwatchShot ? weapon.overwatch.minAimTime       : weapon.normalShooting.minAimTime;
    private float CurrentAimingStateTime  => isOverwatchShot ? weapon.overwatch.aimingStateTime  : weapon.normalShooting.aimingStateTime;
    private float CurrentCooloffStateTime => isOverwatchShot ? weapon.overwatch.cooloffStateTime : weapon.normalShooting.cooloffStateTime;

    public WeaponDefinition GetWeapon() => weapon;

    void Update()
    {
        if (!isActive) return;

        stateTimer -= Time.deltaTime;
        switch (state)
        {
            case State.Aiming:
                if (targetUnit != null)
                {
                    if (isOverwatchShot && !IsTargetStillValid())
                    {
                        CancelShot();
                        return;
                    }

                    if (RotateTowards(targetUnit.GetWorldPosition(), CurrentTurnSpeed))
                    {
                        stateTimer = Mathf.Min(stateTimer, CurrentMinAimTime);
                    }

                    if (isOverwatchShot)
                    {
                        UpdateOverwatchConeIfRotated();
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

    private void UpdateOverwatchConeIfRotated()
    {
        Vector3 currentFacing = unit.transform.forward;
        currentFacing.y = 0f;
        if (currentFacing.sqrMagnitude < 1e-6f) return;
        currentFacing.Normalize();

        if (lastOverwatchFacing == Vector3.zero)
        {
            lastOverwatchFacing = currentFacing;
            return;
        }

        float dot = Vector3.Dot(lastOverwatchFacing, currentFacing);

        if (dot < 0.999f)
        {
            lastOverwatchFacing = currentFacing;
            OverwatchVisionUpdater.UpdateVision(unit, currentFacing, weapon.overwatch.coneAngleDeg);
        }
    }

    // Estää ampumasta läpi seinien jos kohde liikkuu samaan aikaan kun tätä yritetään ampua.
    private bool IsTargetStillValid()
    {
        if (targetUnit == null || targetUnit.IsDead() || targetUnit.IsDying())
            return false;

        if (!unit.TryGetComponent<UnitVision>(out var vision) || !vision.IsInitialized)
            return false;

        var targetPos = targetUnit.GetGridPosition();
        var personalVision = vision.GetUnitVisionGrids();
        if (personalVision == null || !personalVision.Contains(targetPos))
            return false;

        if (unit.TryGetComponent<UnitStatusController>(out var status) &&
            status.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var payload))
        {
            Vector3 facingWorld = payload.facingWorld;
            float coneDeg = payload.coneAngleDeg;

            if (!vision.IsTileInCone(facingWorld, coneDeg, targetPos))
                return false;
        }

        return true;
    }

    private void CancelShot()
    {
        targetUnit = null;
        state = State.Cooloff;
        stateTimer = 0.1f;
        canShootBullet = false;
        ActionComplete();
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

                if (isOverwatchShot && !IsTargetStillValid())
                {
                    CancelShot();
                    return;
                }

                state = State.Shooting;
                currentBurstCount = 0;
                maxBurstCount = weapon.GetRandomBurstCount();

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
                    stateTimer = CurrentCooloffStateTime;
                }
                break;
            case State.Cooloff:
                ActionComplete();
                break;
        }
    }

    public void MarkAsOverwatchShot(bool overwatchShot)
    {

        isOverwatchShot = overwatchShot;
        if (isOverwatchShot)
        {
            lastOverwatchFacing = Vector3.zero;
        }
    }

    private void Shoot()
    {
        Debug.Log("IsOverwatchShot: " + isOverwatchShot);
        if (targetUnit == null)
        {
            return;
        }

        var result = ShootingResolver.Resolve(unit, targetUnit, weapon, isOverwatchShot);

        OnAnyShoot?.Invoke(this, new OnShootEventArgs
        {
            targetUnit = targetUnit,
            shootingUnit = unit,
            shotTier = result.tier
        });

        OnShoot?.Invoke(this, new OnShootEventArgs
        {
            targetUnit = targetUnit,
            shootingUnit = unit,
            shotTier = result.tier
        });

        switch (result.tier)
        {
            case ShotTier.CritMiss:
                return;

            case ShotTier.Close:
                ApplyHit(result.damage, result.bypassCover, result.coverOnly, targetUnit);
                return;

            case ShotTier.Graze:
                if (GetCoverType(targetUnit) == CoverService.CoverType.None)
                {
                    MakeDamage(result.damage + weapon.NoCoverDamageBonus, targetUnit);
                    return;
                }

                ApplyHit(result.damage, result.bypassCover, result.coverOnly, targetUnit);
                return;

            case ShotTier.Hit:
                if (GetCoverType(targetUnit) == CoverService.CoverType.None)
                {
                    MakeDamage(result.damage + weapon.NoCoverDamageBonus, targetUnit);
                    return;
                }

                ApplyHit(result.damage, result.bypassCover, result.coverOnly, targetUnit);
                return;

            case ShotTier.Crit:
                if (GetCoverType(targetUnit) == CoverService.CoverType.None)
                {
                    MakeDamage(result.damage + weapon.NoCoverDamageBonus, targetUnit);
                    return;
                }
                ApplyHit(result.damage, result.bypassCover, result.coverOnly, targetUnit);
                return;
        }
    }

    public void ResetOverwatchShotState()
    {
        isOverwatchShot = false;
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

            // UUSI: tulilinja (blokkaa myös Units-layerin)
            if (!HasLineOfFireTo(enemy))
                continue;

            res.Add(gp);

        }
        return res;
    }

    private IEnumerable<Unit> EnumerateEnemyCandidatesInRange(GridPosition origin, int range)
    {
        bool shooterIsEnemy = unit.IsEnemy();
        foreach (var unit in UnitManager.Instance.GetAllUnitList())
        {
            if (unit == null) continue;
            if (unit.IsEnemy() == shooterIsEnemy) continue;

            // ÄLÄ ehdota kohteeksi jos on kuollut/piilotettu/dying
            if (unit.IsDead() || unit.IsHidden() || unit.IsDying()) continue;

            var gp = unit.GetGridPosition();
            if (gp.floor != origin.floor) continue;
            int cost = SircleCalculator.Sircle(gp.x - origin.x, gp.z - origin.z);
            if (cost > 10 * range) continue;
            yield return unit;
        }
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {

        targetUnit = LevelGrid.Instance.GetUnitAtGridPosition(gridPosition);

        state = State.Aiming;
        stateTimer = CurrentAimingStateTime;

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

    private bool HasLineOfFireTo(Unit target)
    {
        var lg  = LevelGrid.Instance;
        var cfg = LoSConfig.Instance;

        // Silmien/aseen lähtöpiste
        Vector3 eyeW = lg.GetWorldPosition(unit.GetGridPosition()) + Vector3.up * cfg.eyeHeight;

        // Mihin tähdätään (voit käyttää omaa target.GetAimWorldPosition())
        Vector3 aimW = target.transform.position + Vector3.up * cfg.eyeHeight;

        Vector3 dir  = aimW - eyeW;
        float   dist = dir.magnitude;
        if (dist <= 0.001f) return true;
        dir /= dist;

        // LoF = seinät/esteet + Units blokkaavat
        int shootMask = (cfg.losBlockersMask | LayerMask.GetMask("Units"))
                & ~LayerMask.GetMask("Ragdoll");   // ⟵ ei blokkaa

        // Pieni lähtösiirto ettei osuta omaan kapseliin heti nollamatkalla
        const float EPS = 0.02f;
        Vector3 start = eyeW + dir * EPS;
        float   maxD  = Mathf.Max(0f, dist - EPS);

        // Käytetään RaycastAll ja luetaan N*ensimmäinen* merkityksellinen osuma
        var hits = Physics.RaycastAll(start, dir, maxD, shootMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return true; // tyhjää → saa ampua

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            // Ohita ampujan omat osumat
            if (h.collider && h.collider.transform.root == unit.transform) continue;

            // Jos osuma on Unit, tarkista onko se target
            var u = h.collider.GetComponentInParent<Unit>();
            if (u != null)
            {
                // ÄLÄ ehdota kohteeksi jos on kuollut/piilotettu/dying
                if (u.IsDead() || u.IsHidden() || u.IsDying()) continue;
                return u == target;
            }

            // Muuten: seinä/este → blokki
            return false;
        }

        return true;
    }
}
