using System;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Unit))]
public class CoverSkill : MonoBehaviour
{
    [SerializeField] private int newCoverBonusHalf = 15;
    [SerializeField] private int newCoverBonusFull = 25;

    private int personalCover;
    private int personalCoverMax;
    private int thisTurnStartingCover;

    protected Unit unit;

    public event Action<int, int> OnCoverPoolChanged;


    protected virtual void Awake()
    {
        unit = GetComponent<Unit>();
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        if (unit.archetype != null)
        {
            personalCoverMax = unit.archetype.personalCoverMax;
        }

        personalCover = personalCoverMax;
        personalCover = personalCoverMax;
        thisTurnStartingCover = personalCover;

        // kerro UI:lle heti
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
    }

    private void OnDisable()
    {
        TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
    }


    ///****** CoverSystem!************

    public int GetPersonalCover()
    {
        return personalCover;
    }

    public void SetPersonalCover(int value)
    {
        // OFFLINE: ei Mirroria → päivitä suoraan paikallisesti
        if (!NetworkServer.active && !NetworkClient.active)
        {
            ApplyCoverLocal(value);
            return;
        }

        // ONLINE SERVER/HOST: päivitä totuusarvo ja broadcastaa
        if (NetworkServer.active)
        {
            ApplyCoverServer(value);
            return;
        }

        // ONLINE CLIENT: pyydä serveriä asettamaan (EI paikallista asettamista → ei "välähdystä")
        var ni = GetComponent<NetworkIdentity>();
        if (NetworkClient.active && NetworkSyncAgent.Local != null && ni != null)
        {
            NetworkSyncAgent.Local.CmdSetUnitCover(ni.netId, value);
        }
        // ei paikallista muutosta täällä
    }

    public void SetCoverBonus()
    {
        if (unit.IsUnderFire) return;

        var gp = unit.GetGridPosition();
        var pf = PathFinding.Instance;
        if (pf == null) return;

        var node = pf.GetNode(gp.x, gp.z, gp.floor);
        if (node == null) return;

        var t = CoverService.GetNodeAnyCover(node);
        int bonus = t == CoverService.CoverType.High ? newCoverBonusFull :
                    t == CoverService.CoverType.Low  ? newCoverBonusHalf : 0;

        if (bonus > 0) AddPersonalCover(bonus);
    }

    private void ApplyCoverLocal(int value)
    {
        personalCover = Mathf.Clamp(value, 0, personalCoverMax);
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax); // UI päivittyy heti
    }

    [Server] // kutsutaan vain serverillä
    private void ApplyCoverServer(int value)
    {
        personalCover = Mathf.Clamp(value, 0, personalCoverMax);
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
        NetworkSync.UpdateCoverUI(unit); // server → Rpc → kaikkien UI:t
    }

    public void RegenCoverOnMove(int distance)
    {
        int regenPerTile = unit.archetype != null ? unit.archetype.coverRegenOnMove : 5;
        int tileDelta = distance / 10;
        int coverChange = regenPerTile * tileDelta;
        int newCover = personalCover + coverChange;

        if (newCover <= thisTurnStartingCover)
        {
            newCover = thisTurnStartingCover;
        }

        personalCover = Mathf.Clamp(newCover, 0, personalCoverMax);

        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
    }

    public void RegenCoverBy(int amount)
    {
        int before = personalCover;
        personalCover = Mathf.Clamp(personalCover + amount, 0, personalCoverMax);


        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
    }

    public int GetCoverRegenPerUnusedAP()
    {
        if (!unit.IsUnderFire)
        {
            return unit.archetype != null ? unit.archetype.coverRegenPerUnusedAP : 1;
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

    private void AddPersonalCover(int delta)
    {
        if (delta == 0) return;
        personalCover = Mathf.Clamp(personalCover + delta, 0, personalCoverMax);
        OnCoverPoolChanged?.Invoke(personalCover, personalCoverMax);
        NetworkSync.UpdateCoverUI(unit); // jos verkossa
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        thisTurnStartingCover = personalCover;
    }
}
