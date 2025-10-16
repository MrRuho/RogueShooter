using System;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Unit))]
public class CoverSkill : NetworkBehaviour
{
    [SerializeField] private int newCoverBonusHalf = 15;
    [SerializeField] private int newCoverBonusFull = 25;

    private int personalCover;
    private int personalCoverMax;
    private int thisTurnStartingPersonalCover;

    private int currentTurnCoverBonus;

   [SyncVar] private bool hasMoved = false;

    protected Unit unit;

    private int _lastProcessedTurnId = -1;

    public event Action<int, int> OnCoverPoolChanged;

    protected virtual void Awake()
    {
        unit = GetComponent<Unit>();
    }

    private void Start()
    {
        if (unit.archetype != null)
        {
            personalCoverMax = unit.archetype.personalCoverMax;
        }

        personalCover = personalCoverMax;
        thisTurnStartingPersonalCover = personalCover;
        currentTurnCoverBonus = 0;
        hasMoved = false;

        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
    }

    private void OnEnable()
    {
        TurnSystem.Instance.OnTurnStarted += OnTurnStarted_HandleTurnStarted;
        TurnSystem.Instance.OnTurnEnded += OnTurnEnded_HandleTurnEnded;
    }

    private void OnDisable()
    {
        TurnSystem.Instance.OnTurnStarted -= OnTurnStarted_HandleTurnStarted;
        TurnSystem.Instance.OnTurnEnded -= OnTurnEnded_HandleTurnEnded;
    }

    public int GetPersonalCover()
    {
        return personalCover;
    }

    public void SetPersonalCover(int value)
    {
        if (!NetworkServer.active && !NetworkClient.active)
        {
            ApplyCoverLocal(value);
            return;
        }

        if (NetworkServer.active)
        {
            ApplyCoverServer(value);
            return;
        }

        var ni = GetComponent<NetworkIdentity>();
        if (NetworkClient.active && NetworkSyncAgent.Local != null && ni != null)
        {
            NetworkSyncAgent.Local.CmdSetUnitCover(ni.netId, value);
        }
    }

    public void SetCoverBonus()
    {
        // CLIENT: pyydä serveriä tekemään
        if (NetworkClient.active && !NetworkServer.active)
        {
            var ni = GetComponent<NetworkIdentity>();
            NetworkSyncAgent.Local?.CmdApplyCoverBonus(ni.netId);
            return;
        }

        // SERVER / OFFLINE: varsinainen työ
        ServerApplyCoverBonus();
    }
    
    //[Server]
    public void ServerApplyCoverBonus()
    {
        // Vain Server TAI Offline saa suorittaa
        if (NetworkClient.active && !NetworkServer.active) return;
        
        // TÄRKEÄ: Jos on jo liikuttu tällä vuorolla, resetoi bonus ENSIN
        if (hasMoved && currentTurnCoverBonus != 0)
        {
            int newCover = personalCover - currentTurnCoverBonus;
            if (newCover < thisTurnStartingPersonalCover)
                newCover = thisTurnStartingPersonalCover;
            
            personalCover = newCover;
            currentTurnCoverBonus = 0;
        }
        hasMoved = true;
        if (unit.IsUnderFire)
        {
            personalCover = thisTurnStartingPersonalCover;
            OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
            NetworkSync.UpdateCoverUI(unit);
            return;
        }

        var gp = unit.GetGridPosition();
        var pf = PathFinding.Instance;
        if (pf == null) return;

        var node = pf.GetNode(gp.x, gp.z, gp.floor);
        if (node == null) return;

        var t = CoverService.GetNodeAnyCover(node);
        int bonus = t == CoverService.CoverType.High ? newCoverBonusFull :
                    t == CoverService.CoverType.Low  ? newCoverBonusHalf : 0;

        currentTurnCoverBonus = bonus;
         if (bonus > 0)
        {
            if (unit.IsUnderFire)
            {
                SetPersonalCover(thisTurnStartingPersonalCover);
            }
            else
            {
                SetPersonalCover(personalCover + bonus);
            }
            
        }
        else
        {
            NetworkSync.UpdateCoverUI(unit);
        }
    }

    private void ApplyCoverLocal(int value)
    {
        personalCover = Mathf.Clamp(value, 0, personalCoverMax);
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
    }

    //[Server]
    private void ApplyCoverServer(int value)
    {
        // Vain Server TAI Offline saa suorittaa
        if (NetworkClient.active && !NetworkServer.active) return;
        
        personalCover = Mathf.Clamp(value, 0, personalCoverMax);
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
        NetworkSync.UpdateCoverUI(unit);
    }

    public void RegenCoverOnMove(int distance)
    {
        if (NetworkClient.active && !NetworkServer.active)
        {
            var ni = GetComponent<NetworkIdentity>();
            NetworkSyncAgent.Local?.CmdRegenCoverOnMove(ni.netId, distance);
            return;
        }

        ServerRegenCoverOnMove(distance);
    }

    //[Server]
    public void ServerRegenCoverOnMove(int distance)
    {
        // Vain Server TAI Offline saa suorittaa
        if (NetworkClient.active && !NetworkServer.active) return;

        int regenPerTile = unit.archetype != null ? unit.archetype.coverRegenOnMove : 5;
        int tileDelta = distance / 10;
        int coverChange = regenPerTile * tileDelta;
        int newCover = personalCover + coverChange;

        if (newCover <= thisTurnStartingPersonalCover)
        {
            newCover = thisTurnStartingPersonalCover;
        }

        SetPersonalCover(Mathf.Clamp(newCover, 0, personalCoverMax));
    }

    public void RegenCoverBy(int amount)
    {
        if (amount == 0) return;
        SetPersonalCover(personalCover + amount);
    }

    public int GetCoverRegenPerUnusedAP()
    {
        if (!unit.IsUnderFire)
        {
            return unit.archetype != null ? unit.archetype.coverRegenPerUnusedAP : 0;
        }
        return 0;
    }

    public int GetPersonalCoverMax() => personalCoverMax;

    public float GetCoverNormalized()
    {
        return (float)personalCover / personalCoverMax;
    }

    public void ApplyNetworkCover(int current, int max)
    {
        personalCoverMax = max;
        personalCover = Mathf.Clamp(current, 0, max);
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
    }

    public int GetCurrentCoverBonus()
    {
        return currentTurnCoverBonus;
    }

    public void ResetCurrentCoverBonus()
    {
        if (NetworkClient.active && !NetworkServer.active)
        {
            var ni = GetComponent<NetworkIdentity>();
            NetworkSyncAgent.Local?.CmdResetCurrentCoverBonus(ni.netId);
            return;
        }

        ServerResetCurrentCoverBonus();
    }


    
   // [Server]
    public void ServerResetCurrentCoverBonus()
    {
        // Vain Server TAI Offline saa suorittaa
        if (NetworkClient.active && !NetworkServer.active) return;
        
        if (currentTurnCoverBonus != 0)
        {
            int newCover = personalCover - currentTurnCoverBonus;
            if (newCover < thisTurnStartingPersonalCover)
                newCover = thisTurnStartingPersonalCover; // vuoron aloitus on minimi

            SetPersonalCover(newCover);
            currentTurnCoverBonus = 0; // estää tuplavähennykset
        }
    }

    public bool HasMoved()
    {
        return hasMoved;
    }

    private void OnTurnStarted_HandleTurnStarted(Team startTurnTeam, int turnId)
    {
        if (NetworkClient.active && !NetworkServer.active) return;
        // Vain oman puolen vuorolla
        if (unit.Team != startTurnTeam) return;

        // (Valinnainen) suoja duplikaateilta, jos eventti laukeaa useammin samassa vuorossa
        if (_lastProcessedTurnId == turnId) return;
        _lastProcessedTurnId = turnId;

        // Resetit vain omalle puolelle:
        thisTurnStartingPersonalCover = personalCover;
        currentTurnCoverBonus = 0;
        hasMoved = false;
    }
    
    private void OnTurnEnded_HandleTurnEnded(Team endTurnTeam, int turnId)
    {
        if (NetworkClient.active && !NetworkServer.active) return;
        // Vain kun toisen vuoro alkaa. Unitit eivät ole enää tulen alla.
        if (unit.Team != endTurnTeam) return;
        unit.SetUnderFire(false);
    }
}
