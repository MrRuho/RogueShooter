using Mirror;
using UnityEngine;

[RequireComponent(typeof(GrenadeBeaconEffect))]
public class GrenadeBeaconSync : NetworkBehaviour
{
    [SerializeField] private GrenadeBeaconEffect effect;

    [SyncVar(hook = nameof(OnRemainingChanged))]
    private int remaining;

    private bool baselineSet; // asetetaan kerran, ettei skaala nollaannu joka tikillä

    void Awake()
    {
        if (!effect) effect = GetComponent<GrenadeBeaconEffect>();
    }

    public override void OnStartClient()
    {
        // Kun client liittyy, varmista että baseline >= remaining
        if (remaining > 0 && !baselineSet) {
            effect.SetTurnsUntilExplosion(remaining);
            baselineSet = true;
        }
        effect.SetRemainingDirect(remaining);
        if (remaining <= 0) effect.TriggerFinalCountdown();
    }

    void OnRemainingChanged(int oldV, int newV)
    {
        // Aseta baseline kerran (alkuarvoon), ettei pulssi ole heti maksimi
        if (newV > 0 && !baselineSet) {
            effect.SetTurnsUntilExplosion(newV);
            baselineSet = true;
        }

        effect.SetRemainingDirect(newV);

        // Beep yhdesti per tikki
        if (newV < oldV) effect.PlayBeepOnce();

        // Nollassa kiihdytetään loppu
        if (newV <= 0) effect.TriggerFinalCountdown();
    }

    // ---------- SERVER ----------
    [Server]
    public void ServerInit(int start)
    {
        baselineSet = false;      // annetaan hookin asettaa baseline ensimmäisestä arvosta
        remaining   = Mathf.Max(0, start);
    }

    [Server]
    public void ServerOnTurnAdvanced()
    {
        if (remaining > 0) remaining--;
        // kun remaining osuu nollaan: hook tekee kiihdytyksen kaikilla
    }

    [Server]
    public void ServerArmNow()
    {
        // Aseta nollaan → OnRemainingChanged hoitaa TriggerFinalCountdownin
        int old = remaining;
        remaining = 0;          // hook -> TriggerFinalCountdown()
        // (Beep ei toistu jos old == 0, koska vertaan newV < oldV hookissa)
    }

    [Server]
    public void ServerSetRemaining(int value)
    {
        remaining = Mathf.Max(0, value);  // hook hoitaa beeping + finalin
    }
}
