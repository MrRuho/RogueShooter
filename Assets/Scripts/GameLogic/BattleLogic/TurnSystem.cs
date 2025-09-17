using System;
using UnityEngine;

public class TurnSystem : MonoBehaviour
{
    public static TurnSystem Instance { get; private set; }

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
        // Varmista, että alkutila lähetetään kaikille UI:lle
        PlayerLocalTurnGate.Set(isPlayerTurn); // true = Player turn alussa
        OnTurnChanged?.Invoke(this, EventArgs.Empty); // jos haluat myös muut UI:t liikkeelle
    }

    public void NextTurn()
    {
        // Tarkista pelimoodi
        if (GameModeManager.SelectedMode == GameMode.SinglePlayer)
        {
            Debug.Log("SinglePlayer NextTurn");
            turnNumber++;
            isPlayerTurn = !isPlayerTurn;

            OnTurnChanged?.Invoke(this, EventArgs.Empty);

            //Set Unit UI visibility
            PlayerLocalTurnGate.Set(isPlayerTurn);
        }
        else if (GameModeManager.SelectedMode == GameMode.CoOp)
        {
            Debug.Log("Co-Op mode: Proceeding to the next turn.");
            // Tee jotain erityistä CoOp-tilassa
        }
        else if (GameModeManager.SelectedMode == GameMode.Versus)
        {
            Debug.Log("Versus mode: Proceeding to the next turn.");
            // Tee jotain erityistä Versus-tilassa
        }


    }

    public int GetTurnNumber()
    {
        return turnNumber;
    }

    public bool IsPlayerTurn()
    {
        return isPlayerTurn;
    }

    // ForcePhase on serverin kutsuma. Päivittää vuoron ja kutsuu OnTurnChanged
    public void ForcePhase(bool isPlayerTurn, bool incrementTurnNumber)
    {
        if (incrementTurnNumber) turnNumber++;
        this.isPlayerTurn = isPlayerTurn;
        OnTurnChanged?.Invoke(this, EventArgs.Empty);
    }

    // Päivitä HUD verkon kautta (co-op)
    public void SetHudFromNetwork(int newTurnNumber, bool isPlayersPhase)
    {
        turnNumber = newTurnNumber;
        isPlayerTurn = isPlayersPhase;
        OnTurnChanged?.Invoke(this, EventArgs.Empty); // <- päivitää HUDin kuten SP:ssä
    }  
}
