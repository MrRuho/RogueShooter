using System;
using System.Collections;
using UnityEngine;
using Utp;

/// <summary>
/// Control EnemyAI. Go trough all posibble actions what current enemy Unit can do and chose the best one.
/// Listen to TurnSystem and when turn OnTurnChanged, AI state switch WaitingForEnemyTurn to the TakingTurn state
/// and try to find best action to all enemy Units. All enemy Unit do this independently based on 
/// action values. 
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public static EnemyAI Instance { get; private set; }

    private enum State
    {
        WaitingForEnemyTurn,
        TakingTurn,
        Busy,
    }

    private State state;
    private float timer;

    void Awake()
    {
        state = State.WaitingForEnemyTurn;

        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        
        if (GameModeManager.SelectedMode == GameMode.SinglePlayer)
        {
            TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        }
        

        if (GameNetworkManager.Instance != null &&
        GameNetworkManager.Instance.GetNetWorkClientConnected() &&
        !GameNetworkManager.Instance.GetNetWorkServerActive())
        {
            // Coop gamemode using IEnumerator RunEnemyTurnCoroutine() trough the server. No local calls
            if (GameModeManager.SelectedMode == GameMode.CoOp)
                enabled = false;
        }
    }

    void OnDisable()
    {
        if (GameModeManager.SelectedMode == GameMode.SinglePlayer)
        {
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        }
    }

    private void Update()
    {
        //NOTE! Only solo game!
        if (GameModeManager.SelectedMode != GameMode.SinglePlayer) return;
        if (TurnSystem.Instance.IsPlayerTurn()) return;

        //If game mode is SinglePlayer and is not PlayerTurn then runs Enemy AI.
        EnemyAITick(Time.deltaTime);
    }

    /// <summary>
    /// Enemy start taking actions after small waiting time.
    /// Update call this every frame.
    /// </summary>
    private bool EnemyAITick(float dt)
    {
        switch (state)
        {
            // It is Player turn so keep waiting untill TurnSystem_OnTurnChanged switch state to TakingTurn.   
            case State.WaitingForEnemyTurn:
                return false;

            case State.TakingTurn:
                timer -= dt;
                if (timer <= 0f)
                {
                    //Return false when all Enemy Units have make they actions 
                    if (SelectEnemyUnitToTakeAction(SetStateTakingTurn))
                    {
                        state = State.Busy;
                        return false;
                    }
                    else
                    {
                        // If enemy cant make actions. Return turn back to player.
                        // NOTE! In Coop mode CoopTurnCoordinator make this.
                        if (GameModeManager.SelectedMode == GameMode.SinglePlayer)
                        {
                            TurnSystem.Instance.NextTurn();
                        }

                        // Enemy AI switch back to waiting. 
                        state = State.WaitingForEnemyTurn;
                        return true;
                    }
                }
                return false;

            case State.Busy:
                // When Enemy doing action just return.
                // Waiting c# Action call from base action and then call funktion SetStateTakingTurn()
                return false;
        }
        return false;
    }


    /// <summary>
    /// c# Action callback. SelectEnemyUnitToTakeAction use this and when action is ready. This occurs
    /// </summary>
    private void SetStateTakingTurn()
    {
        timer = 0.5f;
        state = State.TakingTurn;
    }

    /// <summary>
    /// Go through all enemy Units on EnemyUnit List and try to take action. 
    /// </summary>
    private bool SelectEnemyUnitToTakeAction(Action onEnemyAIActionComplete)
    {
        foreach (Unit enemyUnit in UnitManager.Instance.GetEnemyUnitList())
        {
            if (enemyUnit == null)
            {
                Debug.LogWarning("[EnemyAI][UnitManager]EnemyUnit list is null:" + enemyUnit);
                continue;
            }
            if (TryTakeEnemyAIAction(enemyUnit, onEnemyAIActionComplete))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Selected Unit Go through all possible actions what Enemy Unit can do 
    /// and choosing the best one based on them action value.
    /// Then make action if have enough action points.
    /// </summary>
    private bool TryTakeEnemyAIAction(Unit enemyUnit, Action onEnemyAIActionComplete)
    {
        // Contains Gridposition and action value (How good action is)
        EnemyAIAction bestEnemyAIAction = null;

        BaseAction bestBaseAction = null;

        // Choosing the best action, based on them action value.
        foreach (BaseAction baseAction in enemyUnit.GetBaseActionsArray())
        {
            //NOTE! Just for testing. AI not do this for now.
            if(baseAction.GetActionName() == "Overwatch")
            {
                Debug.Log("[Enemy AI] I am too dumd to do Overwatch action!");
                // Enemy AI Cant handle this action right now.
                continue;
            }

            if (!enemyUnit.CanSpendActionPointsToTakeAction(baseAction))
            {
                // Enemy cannot afford this action
                continue;
            }

            if (bestEnemyAIAction == null)
            {
                bestEnemyAIAction = baseAction.GetBestEnemyAIAction();
                bestBaseAction = baseAction;
            }
            else
            {
                // Go trough all actions and take the best one.
                EnemyAIAction testEnemyAIAction = baseAction.GetBestEnemyAIAction();
                if (testEnemyAIAction != null && testEnemyAIAction.actionValue > bestEnemyAIAction.actionValue)
                {
                    bestEnemyAIAction = baseAction.GetBestEnemyAIAction();
                    bestBaseAction = baseAction;
                }
            }
        }

        // Try to take action
        if (bestEnemyAIAction != null && enemyUnit.TrySpendActionPointsToTakeAction(bestBaseAction))
        {      
            bestBaseAction.TakeAction(bestEnemyAIAction.gridPosition, onEnemyAIActionComplete);
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// When turn changed. Switch state to taking turn and enemy turn start. 
    /// </summary>
    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        if (!TurnSystem.Instance.IsPlayerTurn())
        {
            state = State.TakingTurn;
            timer = 1f; // Small holding time before action.
        }
    }

    /// <summary>
    /// When playing online: (Coop mode) Server handle All AI actions.
    /// </summary>
    [Mirror.Server]
    public IEnumerator RunEnemyTurnCoroutine()
    {

        SetStateTakingTurn();

        while (true)
        {
            if (TurnSystem.Instance.IsPlayerTurn())
            {
                Debug.LogWarning("[EnemyAI] Players get turn before AI has ended own turn! This sould not be posibble");
                yield break;
            }

            bool finished = EnemyAITick(Time.deltaTime);
            if (finished)
                yield break; // AI-Turn ready. CoopTurnCoordinator continue and give turn back to players.

            yield return null; // wait one frame.
        }
    }
}
