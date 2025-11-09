using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class OverwatchAction : BaseAction
{
    private enum State
    {
        BeginOverwatchIntent,
        OverwatchIntentReady,
    }

    private State state;
    public Vector3 TargetWorld { get; private set; }

    private float stateTimer;

    [SyncVar] public bool Overwatch = false;

    private void Update()
    {
        if (!isActive)
        {
            return;
        }
        stateTimer -= Time.deltaTime;
        switch (state)
        {
            case State.BeginOverwatchIntent:
                if (RotateTowards(TargetWorld))
                {
                    stateTimer = 0;
                }
                break;
            case State.OverwatchIntentReady:
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
            case State.BeginOverwatchIntent:
                state = State.OverwatchIntentReady;
                stateTimer = 0.5f;
                break;

            case State.OverwatchIntentReady:
                ActionComplete();

                // Laske suunta samaan tapaan kuin UI-visualisaatiossa
                Vector3 dir = TargetWorld - unit.transform.position; dir.y = 0f;
                Vector3 facingWorld = (dir.sqrMagnitude > 1e-4f) ? dir.normalized : unit.transform.forward;

                // Näytä pelaajalle (entinen logiikka)
                unit.GetComponent<UnitVision>().ShowUnitOverWachVision(facingWorld, 80f);

                if (NetworkSync.IsOffline || NetworkServer.active)
                {
                    Overwatch = true;
                    // Päivitä serverin päässä myös suunta payloadiksi
                    PushOverwatchFacingServerLocal(facingWorld);
                }
                else if (NetworkClient.active && NetworkSyncAgent.Local != null)
                {
                    var ni = unit.GetComponent<NetworkIdentity>();
                    if (ni != null)
                    {
                        // Lähetä suunta (x,z) samalla kun asetat Overwatchin
                        NetworkSyncAgent.Local.CmdSetOverwatch(ni.netId, true, facingWorld.x, facingWorld.z);
                    }
                }
                break;
        }
    }
    
    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        TargetWorld = LevelGrid.Instance.GetWorldPosition(gridPosition);
        state = State.BeginOverwatchIntent;
        stateTimer = 0.7f;
        ActionStart(onActionComplete);
    }
  
    public override string GetActionName()
    {
        return "Overwatch";
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
        return 0;
    }

    public bool IsOverwatch()
    {
        return Overwatch;
    }

    public void CancelOverwatchIntent()
    {
        GridSystemVisual.Instance.RemovePersistentOverwatch(unit);
        
        if (NetworkSync.IsOffline || NetworkServer.active)
        {
            Overwatch = false;
        }
        else if (NetworkClient.active && NetworkSyncAgent.Local != null)
        {
            var ni = unit.GetComponent<NetworkIdentity>();
            if (ni != null)
            {
                Vector3 dir = TargetWorld - unit.transform.position; dir.y = 0f;
                Vector3 facingWorld = (dir.sqrMagnitude > 1e-4f) ? dir.normalized : unit.transform.forward;
                NetworkSyncAgent.Local.CmdSetOverwatch(ni.netId, false, facingWorld.x, facingWorld.z);
            }
        }
    }

    public void FacingDir()
    {
        Vector3 dir = TargetWorld - unit.transform.position;
        dir.y = 0f;
        Vector3 facingWorld = (dir.sqrMagnitude > 0.0001f) ? dir.normalized : unit.transform.forward;

        unit.GetComponent<UnitVision>().ShowUnitOverWachVision(facingWorld, 80f);
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = 0,
        };
    }

    private void PushOverwatchFacingServerLocal(Vector3 facingWorld)
    {
        if (NetworkServer.active || NetworkSync.IsOffline)
        {
            if (TryGetComponent<UnitStatusController>(out var status))
            {
                status.AddOrUpdate(UnitStatusType.Overwatch, new OverwatchPayload
                {
                    facingWorld = new Vector3(facingWorld.x, 0f, facingWorld.z),
                    coneAngleDeg = 80f,
                    rangeTiles = 8
                });
            }
        }
    }

    [Server]
    public void ServerApplyOverwatch(bool enabled, Vector2 facingXZ)
    {
        var dir = new Vector3(facingXZ.x, 0f, facingXZ.y);
        if (dir.sqrMagnitude > 1e-6f) dir.Normalize();
        else dir = transform.forward;

        // early-out jos ei muutosta
        bool sameState = Overwatch == enabled;
        bool sameDir   = false;

        if (TryGetComponent<UnitStatusController>(out var s) &&
            s.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var oldP))
        {
            sameDir = Vector3.Dot(oldP.facingWorld, dir) > 0.999f;
        }

        if (sameState && sameDir) return;

        Overwatch = enabled;

        if (enabled)
            s.AddOrUpdate(UnitStatusType.Overwatch, new OverwatchPayload{ facingWorld = dir, coneAngleDeg = 80f, rangeTiles = 8 });
        else
            s.Remove(UnitStatusType.Overwatch);
    }
}
