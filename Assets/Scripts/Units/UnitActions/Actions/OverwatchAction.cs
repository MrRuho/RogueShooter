using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  Jos pelaaja on jättänyt Unitin Overwatch = true, ja päättää vuoronsa, niin Unit siirtyy Overwach tilaan
///  ja ampuu kaikkia näkemiään liikkuvia kohteita.
/// </summary>
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

    public bool Overwatch = false;

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
                float afterTurnStateTime = 0.5f;
                stateTimer = afterTurnStateTime;
                break;

            case State.OverwatchIntentReady:
                ActionComplete();
                Overwatch = true;
                break;
        }
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        TargetWorld = LevelGrid.Instance.GetWorldPosition(gridPosition);  
        state = State.BeginOverwatchIntent;
        float beforeTurnStateTime = 0.7f;
        stateTimer = beforeTurnStateTime;
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

    // Lopullinen tila tarkistetaan kun pelaaja päättää vuoronsa.
    public bool IsOverwatch()
    {
        return Overwatch;
    }

    // Tila peruuntuu heti jos pelaaja, liikkuu, ampuu yms. 
    public void CancelOverwatchIntent()
    {
        Overwatch = false;
    }

    /// <summary>
    /// ENEMY AI: 
    /// NOTE! Currently this action has no value. Just testing!
    /// </summary>
    /// DODO AI käyttäytymis idea. Jos AI ei pysty tuottamaan helposti vahinkoa pelaajaan se pyrkii luomaan. 
    /// 1. Mahdollisimman kattava vaara alue. (AI ei tiedä missä pelaajan Unitit on)
    /// 2. Keskitetty vaaraalue. Jos AI tietää pelaajan Unittien mahdollisen tulosuunnan ( Viimeksi nähty paikka + simulaatio siitä mihin pelaaja
    /// Yrittää siirtää omia unittejaan) 
    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = 0,
        };
    } 
}
