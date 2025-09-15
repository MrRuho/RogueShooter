using System;
using System.Collections;
using UnityEngine;
using Utp;
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
        DontDestroyOnLoad(gameObject); // valinnainen
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
            // Co-opissa AI:n ajaa vain serveri koroutinena
            if (GameModeManager.SelectedMode == GameMode.CoOp)
                enabled = false;
        }
    }

    private void Update()
    {
        //HUOM! AI:n Update-looppi kuuluu vain yksinpeliin. Coopissa kutsu tehdään CoopTurcoordinator.cs kautta
        // joka kutsuu EnemyAI.cs funktiota RunEnemyTurnCoroutine()
        if (GameModeManager.SelectedMode != GameMode.SinglePlayer) return;
        // Odotellaan että pelaaja on päättänyt vuoronsa
        if (TurnSystem.Instance.IsPlayerTurn()) return;
        //Jos pelimoodi on singleplayer ja ei ole pelaajan vuoro, niin ajetaan Enemy AI.
        EnemyAITick(Time.deltaTime);
    }

    /// <summary>
    /// Yksi AI "frame": sisältää saman tilakoneen kuin ennen Update:ssa.
    /// Kutsutaan sekä Update:sta (SP) että koroutista (Co-op server).
    /// </summary>
    // 1) Vaihda signatuuri ja palauta valmis/ei-valmis
    private bool EnemyAITick(float dt)
    {
        switch (state)
        {
            case State.WaitingForEnemyTurn:
                return false;

            case State.TakingTurn:
                timer -= dt;
                if (timer <= 0f)
                {
                    if (TryTakeEnemyAIAction(SetStateTakingTurn))
                    {
                        state = State.Busy; // odottaa actionin callbackia
                        return false;
                    }
                    else
                    {
                        // Ei enää tekoja → AI-vuoro loppu
                        if (GameModeManager.SelectedMode == GameMode.SinglePlayer)
                        {
                            // SP: voidaan vaihtaa vuoro heti täältä
                            TurnSystem.Instance.NextTurn();
                        }
                        // Co-opissa EI kutsuta NextTurniä täältä.
                        state = State.WaitingForEnemyTurn;
                        return true; // valmis
                    }
                }
                return false;

            case State.Busy:
                // Odotetaan callback -> SetStateTakingTurn(); ei valmis vielä
                return false;
        }
        return false;
    }


    private void SetStateTakingTurn()
    {
        timer = 0.5f;
        state = State.TakingTurn;
    }

    private bool TryTakeEnemyAIAction(Action onEnemyAIActionComplete)
    {
        Debug.Log("TryTakeEnemyAIAction");
        foreach (Unit enemyUnit in UnitManager.Instance.GetEnemyUnitList())
        {
            if (enemyUnit == null)
            {
                Debug.Log("Enemy is null!");
                continue;
            }
            if (TryTakeEnemyAIAction(enemyUnit, onEnemyAIActionComplete))
            {
                Debug.Log(enemyUnit + "Enemy unit in enemy unit list make action!");
                return true;
            }
        }
        Debug.Log("Action failde!");
        return false;
    }

    private bool TryTakeEnemyAIAction(Unit enemyUnit, Action onEnemyAIActionComplete)
    {
        SpinAction spinAction = enemyUnit.GetSpinAction();
        GridPosition actionGridPosition = enemyUnit.GetGridPosition();

        if (!spinAction.IsValidGridPosition(actionGridPosition)) return false;
        if (!enemyUnit.TrySpendActionPointsToTakeAction(spinAction)) return false;
        spinAction.TakeAction(actionGridPosition, onEnemyAIActionComplete);
        Debug.Log("SpinAction and ActionGridPosition are true and make spinaction!");

        return true;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        if (!TurnSystem.Instance.IsPlayerTurn())
        {
            state = State.TakingTurn;
            timer = 2f; // pieni viive ennen ensimmäistä tekoa
        }
    }

    // 3) Co-opin koroutti: katkaise kun EnemyAITick ilmoittaa valmiiksi
    [Mirror.Server]
    public IEnumerator RunEnemyTurnCoroutine()
    {
       // if (GameModeManager.SelectedMode == GameMode.Versus) yield break;

        // Alusta vihollisvuoro kuten SP:ssä
        SetStateTakingTurn();

        // Aja kunnes AI ilmoittaa olevansa valmis
        while (true)
        {
            // Jos jostain syystä vuoro jo vaihtui (varmistus)
            if (TurnSystem.Instance.IsPlayerTurn())
                yield break;

            bool finished = EnemyAITick(Time.deltaTime);
            if (finished)
                yield break; // AI-vuoro valmis → CoopTurnCoordinator jatkaa vuoronvaihtoa

            yield return null; // odota seuraava frame
        }
    }
}
