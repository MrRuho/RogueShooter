using System.Collections;
using Mirror;
using UnityEngine.SceneManagement;

public class ResetService : NetworkBehaviour
{
    public static ResetService Instance;

    // LIPPU: ajetaan post-reset -alustus, kun uusi scene on valmis
    public static bool PendingHardReset;

    void Awake() => Instance = this;

    [Command(requiresAuthority = false)]
    public void CmdRequestHardReset()
    {
        if (!NetworkServer.active) return;
        HardResetServerAuthoritative();
    }

    [Server]
    public void HardResetServerAuthoritative()
    {
        PendingHardReset = true; // <-- vain lippu päälle
        var nm = (NetworkManager)NetworkManager.singleton;
        var scene = SceneManager.GetActiveScene().name;
        nm.ServerChangeScene(scene);
        // ÄLÄ tee mitään tähän enää
    }

    [ClientRpc]
    public void RpcPostResetClientInit(int turnNumber)
    {
        // odota 1 frame että UI-komponentit ovat ehtineet OnEnable/subscribe
        StartCoroutine(_ClientInitCo(turnNumber));
    }

    private IEnumerator _ClientInitCo(int turnNumber)
    {
        yield return null;

        // 1) Avaa paikallinen “saa toimia” -portti (triggaa LocalPlayerTurnChanged)
        PlayerLocalTurnGate.SetCanAct(true);

        // 2) Päivitä HUD (näyttää "Players turn", aktivoi End Turn -napin logiikkaasi vasten)
        TurnSystem.Instance?.SetHudFromNetwork(turnNumber, true);
    }
}
