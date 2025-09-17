using Mirror;

public class UnitUIBroadcaster : NetworkBehaviour
{
    public static UnitUIBroadcaster Instance { get; private set; }
    void Awake() { if (Instance == null) Instance = this; }

    // Tätä saa kutsua vain serveri (hostin serveripuoli)
    [Server]
    public void BroadcastUnitWorldUIVisibility(bool allready)
    {
        if (!NetworkServer.active) return;

        // käy kaikki serverillä tunnetut unitit läpi
        foreach (var kvp in NetworkServer.spawned)
        {
            var unit = kvp.Value.GetComponent<Unit>();
            if (!unit) continue;

            // serveri voi laskea logiikan: pitääkö tämän unitin AP näkyä
            bool visible = ShouldBeVisible(unit, allready);

            // lähetä client-puolelle että tämän unitin UI asetetaan
            RpcSetUnitUIVisibility(unit.netId, visible);
        }
    }

    // Tätä kutsuu serveri, suoritetaan kaikilla clienteillä
    [ClientRpc]
    private void RpcSetUnitUIVisibility(uint unitId, bool visible)
    {
        if (NetworkClient.spawned.TryGetValue(unitId, out var ni) && ni != null)
        {
            var ui = ni.GetComponentInChildren<UnitWorldUI>();
            if (ui != null) ui.SetVisible(visible);
        }
    }

    // serverilogiikka omistajan perusteella
    [Server]
    private bool ShouldBeVisible(Unit unit, bool allready)
    {
        // Kaikki pelaajat ovat valmiina joten näytetään vain vihollisen AP pisteeet.
        if (allready)
        {
            return unit.IsEnemy();
        }

        // Co-Op
        bool playersPhase = TurnSystem.Instance.IsPlayerTurn();

        bool ownerEnded = false;
        if (unit.OwnerId != 0 &&
            NetworkServer.spawned.TryGetValue(unit.OwnerId, out var ownerIdentity) &&
            ownerIdentity != null)
        {
            var pc = ownerIdentity.GetComponent<PlayerController>();
            if (pc != null) ownerEnded = pc.hasEndedThisTurn;
        }

        // 2) Päätä näkyvyys
        if (playersPhase)
        {
            // Pelaajavaihe: näytä kaikki ei-viholliset, joiden omistaja EI ole lopettanut
            return !unit.IsEnemy() && !ownerEnded;
        }
        else
        {
            // Vihollisvaihe: näytä vain viholliset
            return unit.IsEnemy();
        }   
    }
}
