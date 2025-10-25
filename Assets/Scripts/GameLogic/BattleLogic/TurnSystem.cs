using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class TurnSystem : MonoBehaviour
{
    public static TurnSystem Instance { get; private set; }
    public Team CurrentTeam { get; private set; } = Team.Player;
    public int TurnId { get; private set; } = 0;



    public event Action<Team,int> OnTurnStarted;
    public event Action<Team,int> OnTurnEnded;

    public event EventHandler OnTurnChanged;
    private int turnNumber = 1;
    private bool isPlayerTurn = true;

    private void Awake()
    {

        // Ensure that there is only one instance in the scene
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
        OnTurnChanged += turnSystem_OnTurnChanged;
        // Ensimmäinen vuoro.
        OnTurnStarted?.Invoke(CurrentTeam, TurnId);
        // Varmista, että alkutila lähetetään kaikille UI:lle
        PlayerLocalTurnGate.Set(isPlayerTurn); // true = Player turn alussa
        OnTurnChanged?.Invoke(this, EventArgs.Empty); // jos haluat myös muut UI:t liikkeelle
    }

    private void OnDisable()
    {
        OnTurnChanged -= turnSystem_OnTurnChanged;
    }

    private void turnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        GridSystemVisual.Instance.HideAllGridPositions();
        UnitActionSystem.Instance.ResetSelectedAction();
        UnitActionSystem.Instance.ResetSelectedUnit();
    }

    public void NextTurn()
    {
        
        Debug.Log($"[TurnSystem] NextTurn(): end={CurrentTeam}, id={TurnId}");
        if (GameModeManager.SelectedMode != GameMode.SinglePlayer && !NetworkServer.active)
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
            Debug.Log("Co-Op mode: Proceeding to the next turn.");
        }
        else if (GameModeManager.SelectedMode == GameMode.Versus)
        {
            Debug.Log("Versus mode: Proceeding to the next turn.");
        }
    }

    
    public void ForcePhase(bool isPlayerTurn, bool incrementTurnNumber)
    {
        if (incrementTurnNumber) turnNumber++;
        
        if (NetworkServer.active && isPlayerTurn)
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

    public bool IsUnitsTurn(Unit u) => u.Team == CurrentTeam;

    /// <summary>
    /// Offline/SP: nollaa paikallisen vuorotilan ja aloittaa alusta.
    /// Kutsu tätä heti, kun yksiköt on spawnattu uudelleen level-reloadin jälkeen.
    /// </summary>
    /// <param name="resetTurnNumber">Asetetaanko turnNumber takaisin 1:een.</param>
    /// <param name="playersPhase">Aloitetaanko Players-vaiheesta (yleensä true).</param>
    public void ResetAndBegin(bool resetTurnNumber = true, bool playersPhase = true)
    {
        // Online-tilassa varoitetaan: online-reset hoidetaan NetTurnManagerin kautta
        if (GameModeManager.SelectedMode != GameMode.SinglePlayer && Mirror.NetworkServer.active)
        {
            Debug.LogWarning("[TurnSystem] ResetAndBegin() on offline/SP-apu. Verkossa käytä NetTurnManager.ServerResetAndBegin().");
        }

        // Nollaa paikalliset laskurit/tila
        if (resetTurnNumber) turnNumber = 1;

        // UI-/SP-luupin peruskentät
        CurrentTeam = playersPhase ? Team.Player : Team.Enemy;
        TurnId = 0;                 // sisäinen vaihtolaskuri, alkaa alusta
        var wasPlayerTurn = IsPlayerTurn();

        // Päivitä “onko pelaajan vuoro” -portti ja kerro UI:lle
        ForcePhase(isPlayerTurn: playersPhase, incrementTurnNumber: false); // kutsuu OnTurnChanged, käyttää nykyistä logiikkaasi
        PlayerLocalTurnGate.Set(playersPhase);                               // HUD/input-portti heti oikein

        // Ilmoita uuden vuoron alkamisesta niille, jotka kuuntelevat OnTurnStarted
        OnTurnStarted?.Invoke(CurrentTeam, TurnId);

        // Jos haluat täydellisen synkan HUDissa, voit vielä varmistaa:
        // SetHudFromNetwork(turnNumber, playersPhase);
    }

}
