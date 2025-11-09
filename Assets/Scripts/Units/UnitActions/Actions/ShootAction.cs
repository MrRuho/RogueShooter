using System;
using System.Collections.Generic;
using UnityEngine;

public class ShootAction : BaseAction
{
    public static event EventHandler<OnShootEventArgs> OnAnyShoot;
    public event EventHandler<OnShootEventArgs> OnShoot;

    [Header("Normal Shooting Settings")]
    [SerializeField]
    private ShootingSettings normalSettings = new ShootingSettings
    {
        aimTurnSpeed = 10f,
        minAimTime = 0.40f,
        aimingStateTime = 1f,
        cooloffStateTime = 0.5f
    };
    
    [Header("Overwatch Shooting Settings")]
    [SerializeField] private ShootingSettings overwatchSettings = new ShootingSettings
    {
        aimTurnSpeed = 20f,
        minAimTime = 0.15f,
        aimingStateTime = 0.5f,
        cooloffStateTime = 0.3f
    };

    private bool isOverwatchShot;
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

    private ShootingSettings CurrentSettings => isOverwatchShot ? overwatchSettings : normalSettings;

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

                    if (RotateTowards(targetUnit.GetWorldPosition(), CurrentSettings.aimTurnSpeed))
                    {
                        stateTimer = Mathf.Min(stateTimer, CurrentSettings.minAimTime);
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

            // 1) Päivitä payload
            if (unit.TryGetComponent<UnitStatusController>(out var status))
            {
                if (status.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var payload))
                {
                    payload.facingWorld = currentFacing;
                    payload.coneAngleDeg = 80f;
                    status.AddOrUpdate(UnitStatusType.Overwatch, payload);
                }
            }

            // 2) Päivitä paikallinen overlay (vain visuaali)
            if (unit.TryGetComponent<UnitVision>(out var vision))
            {
                vision.ShowUnitOverWachVision(currentFacing, 80f);
            }

            // 3) Päivitä VAIN tämän unitin vision cache ja TeamVision
            if (NetworkSync.IsOffline)
            {
                if (unit.TryGetComponent<UnitVision>(out var v) && v.IsInitialized)
                {
                    // Päivitä VAIN tämän unitin 360° cache
                    v.UpdateVisionNow();
                    // Julkaise uusi kartio TeamVisioniin
                    v.ApplyAndPublishDirectionalVision(currentFacing, 80f);
                }
            }
            else if (Mirror.NetworkServer.active && NetworkSyncAgent.Local != null)
            {
                // Server: päivitä tämän unitin cache ja lähetä RPC
                if (unit.TryGetComponent<UnitVision>(out var v) && v.IsInitialized)
                {
                    v.UpdateVisionNow();
                    v.ApplyAndPublishDirectionalVision(currentFacing, 80f);
                }
                
                // Lähetä RPC clienteille
                var ni = unit.GetComponent<Mirror.NetworkIdentity>();
                if (ni != null)
                {
                    NetworkSyncAgent.Local.RpcUpdateSingleUnitVision(ni.netId, currentFacing, 80f);
                }
            }
        }
    }

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
            Vector3 facingWorld = new Vector3(payload.facingWorld.x, 0f, payload.facingWorld.z).normalized;
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
                    stateTimer = CurrentSettings.cooloffStateTime;
                }
                break;
            case State.Cooloff:
                ActionComplete();
                break;
        }
    }

    public void MarkAsOverwatchShot(bool v)
    {
        isOverwatchShot = v;
        if (v)
        {
            lastOverwatchFacing = Vector3.zero;
        }
    }

    private void Shoot()
    {
        if (targetUnit == null)
        {
            return;
        }

        var result = ShootingResolver.Resolve(unit, targetUnit, weapon);

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
        stateTimer = CurrentSettings.aimingStateTime;

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
