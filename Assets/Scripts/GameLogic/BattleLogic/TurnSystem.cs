using System;
using System.Collections.Generic;
using UnityEngine;

public class TurnSystem : MonoBehaviour
{
    public static TurnSystem Instance { get; private set; }
    public Team CurrentTeam { get; set; } = Team.Player;
    public int TurnId { get; set; } = 0;

    public event Action<Team,int> OnTurnStarted;
    public event Action<Team,int> OnTurnEnded;

    public event EventHandler OnTurnChanged;
    private int turnNumber = 1;
    private bool isPlayerTurn = true;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError(" More than one TurnSystem in the scene!" + transform + " " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        OnTurnStarted += turnSystem_OnTurnStarted;
        OnTurnEnded += turnSystem_OnTurnEnded;

        OnTurnChanged += turnSystem_OnTurnChanged;

        OnTurnStarted?.Invoke(CurrentTeam, TurnId);
        PlayerLocalTurnGate.Set(isPlayerTurn);
        OnTurnChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDisable()
    {
        OnTurnChanged -= turnSystem_OnTurnChanged;
        OnTurnStarted -= turnSystem_OnTurnStarted;
        OnTurnEnded -= turnSystem_OnTurnEnded;
    }

    private void turnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        UnitActionSystem.Instance.ResetSelectedAction();
        UnitActionSystem.Instance.ResetSelectedUnit();
    }


    private void turnSystem_OnTurnStarted(Team startTurnTeam, int turnId)
    {

        if (NetMode.IsRemoteClient) return; //NetworkClient.active && !NetworkServer.active
        List<Unit> units = new();

        //Muodostetaan lista niistä uniteista jotka lopettavat vuoron.
        foreach (Unit unit in UnitManager.Instance.GetAllUnitList())
        {
            if (unit.Team != startTurnTeam) continue;
            units.Add(unit);
        }
        
        StatusCoordinator.Instance.UnitTurnStartStatus(units);

    }

    private void turnSystem_OnTurnEnded(Team endTurnTeam, int turnId)
    {

        if (NetMode.IsRemoteClient) return; // NetworkClient.active && !NetworkServer.active
        List<Unit> units = new();

        //Muodostetaan lista niistä uniteista jotka lopettavat vuoron.
        foreach (Unit unit in UnitManager.Instance.GetAllUnitList())
        {
            if (unit.Team != endTurnTeam) continue;
            units.Add(unit);
        }

        StatusCoordinator.Instance.UnitTurnEndStatus(units);    
    }

    public void NextTurn()
    {

        if (GameModeManager.SelectedMode != GameMode.SinglePlayer && !NetMode.IsOnline)
        {
            Debug.LogWarning("Client yritti kääntää vuoroa lokaalisti, ignoroidaan.");
            return;
        }
      
        OnTurnEnded?.Invoke(CurrentTeam, TurnId);
        CurrentTeam = (CurrentTeam == Team.Player) ? Team.Enemy : Team.Player;
        TurnId++;
        OnTurnStarted?.Invoke(CurrentTeam, TurnId);

        if (GameModeManager.SelectedMode == GameMode.SinglePlayer)
        {
            turnNumber++;
            isPlayerTurn = !isPlayerTurn;
            OnTurnChanged?.Invoke(this, EventArgs.Empty);
            PlayerLocalTurnGate.Set(isPlayerTurn);
        }
        else if (GameModeManager.SelectedMode == GameMode.CoOp)
        {

        }
        else if (GameModeManager.SelectedMode == GameMode.Versus)
        {

        }
    }
 
    public void ForcePhase(bool isPlayerTurn, bool incrementTurnNumber)
    {
        if (incrementTurnNumber) turnNumber++;
        
        if (NetMode.IsOnline && isPlayerTurn)
        {
            ConvertUnusedActionPointsToCoverPoints();
        }
        
        this.isPlayerTurn = isPlayerTurn;
        OnTurnChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetHudFromNetwork(int newTurnNumber, bool isPlayersPhase)
    {
        turnNumber = newTurnNumber;
        isPlayerTurn = isPlayersPhase;
        OnTurnChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ConvertUnusedActionPointsToCoverPoints()
    {
        Debug.Log("Konvertoidaan käyttämättömät pisteet coveriksi");
        List<Unit> ownUnits = UnitManager.Instance.GetFriendlyUnitList();
            for (int i = 0; i < ownUnits.Count; i++)
            {
                Unit u = ownUnits[i];
                int ap = u.GetActionPoints();
                if (ap <= 0) continue; 
                int per = u.GetCoverRegenPerUnusedAP();
                u.RegenCoverBy(ap * per);
            }
    }

    public int GetTurnNumber()
    {
        return turnNumber;
    }

    public void ResetTurnNumber()
    {
        turnNumber = 1;
    }

     public void ResetTurnId()
    {
        TurnId = 0;
    }

    public bool IsPlayerTurn()
    {
        return isPlayerTurn;
    }

    public bool IsUnitsTurn(Unit unit) => unit.Team == CurrentTeam;

    public void ResetAndBegin(bool resetTurnNumber = true, bool playersPhase = true)
    {
        if (GameModeManager.SelectedMode != GameMode.SinglePlayer && Mirror.NetworkServer.active)
        {
            Debug.LogWarning("[TurnSystem] ResetAndBegin() on offline/SP-apu. Verkossa käytä NetTurnManager.ServerResetAndBegin().");
        }

        if (resetTurnNumber) turnNumber = 1;

        CurrentTeam = playersPhase ? Team.Player : Team.Enemy;
        TurnId = 0;
        var wasPlayerTurn = IsPlayerTurn();

        ForcePhase(isPlayerTurn: playersPhase, incrementTurnNumber: false);
        PlayerLocalTurnGate.Set(playersPhase);

        OnTurnStarted?.Invoke(CurrentTeam, TurnId);
    }

    public void BeginPlayersTurn(bool incrementTurnId)
    {
        if (incrementTurnId) TurnId++;
        CurrentTeam = Team.Player;
        OnTurnStarted?.Invoke(CurrentTeam, TurnId);
        ForcePhase(isPlayerTurn: true, incrementTurnNumber: false);
    }

    public void BeginEnemyTurn(bool incrementTurnId)
    {
        if (incrementTurnId) TurnId++;
        CurrentTeam = Team.Enemy;
        OnTurnStarted?.Invoke(CurrentTeam, TurnId);
        ForcePhase(isPlayerTurn: false, incrementTurnNumber: false);
    }   
}
