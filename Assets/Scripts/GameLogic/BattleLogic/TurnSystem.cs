using System;
using System.Collections.Generic;
using Mirror;
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

        StartCoroutine(Co_DeferredFirstTurnKick());
       // OnTurnStarted?.Invoke(CurrentTeam, TurnId);
       // PlayerLocalTurnGate.Set(isPlayerTurn);
       // OnTurnChanged?.Invoke(this, EventArgs.Empty);
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

    private System.Collections.IEnumerator Co_DeferredFirstTurnKick()
    {
        // odota 1–2 framea että UnitManager, UnitActionSystem, StatusCoordinator ym. ovat varmasti ylhäällä
        yield return null;
        yield return null;

        OnTurnStarted?.Invoke(CurrentTeam, TurnId);
        PlayerLocalTurnGate.Set(isPlayerTurn);
        OnTurnChanged?.Invoke(this, EventArgs.Empty);
    }

    private void turnSystem_OnTurnStarted(Team startTurnTeam, int turnId)
    {
        if (NetMode.IsRemoteClient) return;

        StartCoroutine(Co_SafeTurnStart(startTurnTeam, turnId));
    }

    /*
    private System.Collections.IEnumerator Co_SafeTurnStart(Team startTurnTeam, int turnId)
    {
        const int MAX_WAIT_FRAMES = 180;
        int waitedFrames = 0;

        while (BaseAction.AnyActionActive() && waitedFrames < MAX_WAIT_FRAMES)
        {
            waitedFrames++;
            yield return null;
        }

        if (waitedFrames > 0)
        {
            Debug.Log($"[TurnSystem] Odotettiin {waitedFrames} framea että actionit päättyivät");
        }

        if (BaseAction.AnyActionActive())
        {
            Debug.LogWarning("[TurnSystem] Actionit ei päättyneet ajoissa, pakkolopetetaan!");
            BaseAction.ForceCompleteAllActiveActions();
            yield return null;
        }

        
        UnitActionSystem.Instance.UnlockInput();

        List<Unit> units = new();

        foreach (Unit unit in UnitManager.Instance.GetAllUnitList())
        {
            if (unit.Team != startTurnTeam) continue;
            if (unit != null)
            {
                units.Add(unit);
                unit.GetComponent<BaseAction>().ResetChostActions();
            }
            else
            {
                Debug.LogWarning("[TurnSystem] NULL unit in UnitManager AllUnitList!");
            }
        }

        var teamId = TeamsID.CurrentTurnTeamId();
        NetworkSyncAgent.Local.ServerPushTeamVision(teamId, endPhase:false);
        StatusCoordinator.Instance.UnitTurnStartStatus(units);
    }
*/
    private System.Collections.IEnumerator Co_SafeTurnStart(Team startTurnTeam, int turnId)
    {
        const int MAX_WAIT_FRAMES = 180;
        int waitedFrames = 0;

        // 1) Odota, että mikään action ei ole aktiivinen
        while (BaseAction.AnyActionActive() && waitedFrames < MAX_WAIT_FRAMES)
        {
            waitedFrames++;
            yield return null;
        }

        if (BaseAction.AnyActionActive())
        {
            Debug.LogWarning("[TurnSystem] Actionit ei päättyneet ajoissa, pakkolopetetaan!");
            BaseAction.ForceCompleteAllActiveActions();
            yield return null;
        }

        // 2) Odota kriittiset singletonit
        while ((UnitManager.Instance == null || UnitActionSystem.Instance == null || StatusCoordinator.Instance == null)
            && waitedFrames < MAX_WAIT_FRAMES)
        {
            waitedFrames++;
            yield return null;
        }

        // 3) Jos ollaan verkossa serverinä/hostina ja käytetään vision-RPC:tä, odota agentti
        if (Mirror.NetworkServer.active && !NetworkSync.IsOffline)
        {
            int w = 0;
            while (NetworkSyncAgent.Local == null && w < 60) { w++; yield return null; }
        }

        // 4) Avaa input
        UnitActionSystem.Instance?.UnlockInput();

        // 5) Rebuild team vision (start phase): 
        //    - offline: tee lokaalisti
        //    - host/server: RPC koko tiimille
        int teamId = (startTurnTeam == Team.Player) ? 0 : 1;  // yksiselitteinen mappi tähän
        if (NetworkSync.IsOffline)
        {
            TeamVisionService.Instance.RebuildTeamVisionLocal(teamId, true);
           // RebuildTeamVisionLocal(teamId, endPhase: false);
        }
        else if (Mirror.NetworkServer.active && NetworkSyncAgent.Local != null)
        {
            NetworkSyncAgent.Local.ServerPushTeamVision(teamId, endPhase: false);
        }

        // 6) Kerää vuoron aloittavat unitit
        List<Unit> units = new();
        var all = UnitManager.Instance?.GetAllUnitList();
        if (all != null)
        {
            foreach (Unit unit in all)
            {
                if (!unit) continue;
                if (unit.Team != startTurnTeam) continue;

                units.Add(unit);

                // Voi puuttua rootista — tee null-guard:
                var ba = unit.GetComponent<BaseAction>();
                if (ba != null) ba.ResetChostActions();
            }
        }

        // 7) Ilmoita statusit
        StatusCoordinator.Instance.UnitTurnStartStatus(units);
    }

    /*
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

        var teamId = TeamsID.CurrentTurnTeamId();
        NetworkSyncAgent.Local.ServerPushTeamVision(teamId, endPhase: true);
        StatusCoordinator.Instance.UnitTurnEndStatus(units);
    }
*/
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
 
    private void turnSystem_OnTurnEnded(Team endTurnTeam, int turnId)
    {
        if (NetMode.IsRemoteClient) return;

        // rebuild end-phase vision: kartio
        int teamId = (endTurnTeam == Team.Player) ? 0 : 1;

        if (NetworkSync.IsOffline)
            TeamVisionService.Instance.RebuildTeamVisionLocal(teamId, true);
           // RebuildTeamVisionLocal(teamId, endPhase: true);
        else if (Mirror.NetworkServer.active && NetworkSyncAgent.Local != null)
            NetworkSyncAgent.Local.ServerPushTeamVision(teamId, endPhase: true);

        // lista vuoron lopettavista
        List<Unit> units = new();
        var all = UnitManager.Instance?.GetAllUnitList();
        if (all != null)
        {
            foreach (Unit unit in all)
            {
                if (!unit) continue;
                if (unit.Team != endTurnTeam) continue;
                units.Add(unit);
            }
        }

        Debug.Log($"[TurnSystem] Calling UnitTurnEndStatus, ServerActive={Mirror.NetworkServer.active}");
        StatusCoordinator.Instance.UnitTurnEndStatus(units);
    }

    private static void RebuildTeamVisionLocal(int teamId, bool endPhase)
    {
        var list = UnitManager.Instance?.GetAllUnitList();
        if (list == null) return;

        foreach (var unit in list)
        {
            if (!unit) continue;
            if (unit.GetTeamID() != teamId) continue;
            if (unit.IsDead() || unit.IsDying()) continue;

            var vision = unit.GetComponent<UnitVision>();
            if (vision == null || !vision.IsInitialized) continue;

            // Aina päivitä tuore 360° cache ensin
            vision.UpdateVisionNow();
            int actionpoints = unit.GetActionPoints();
            Vector3 facing = unit.transform.forward;
            float angle = endPhase ? vision.GetDynamicConeAngle(actionpoints, 80f) : 360f;

            if (endPhase && unit.TryGetComponent<OverwatchAction>(out var ow) && ow.IsOverwatch())
            {
                angle = vision.GetDynamicConeAngle(0, 80f);
                var dir = ow.TargetWorld - unit.transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f) facing = dir.normalized;
                angle = 80f;
            }

            vision.ApplyAndPublishDirectionalVision(facing, angle);
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
        /*
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
        */
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
