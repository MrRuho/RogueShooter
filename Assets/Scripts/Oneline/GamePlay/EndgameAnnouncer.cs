using System;
using Mirror;
using UnityEngine;

public class EndgameAnnouncer : NetworkBehaviour
{
    public override void OnStartServer()
    {
        Unit.OnAnyUnitDead += OnAnyUnitDead_Server;
    }
    public override void OnStopServer()
    {
        Unit.OnAnyUnitDead -= OnAnyUnitDead_Server;
    }

    [ServerCallback]
    private void OnAnyUnitDead_Server(object sender, EventArgs e)
    {
        var um = UnitManager.Instance;
        if (um == null) return;

        int friendly = um.GetFriendlyUnitList().Count; // hostin puoli
        int enemy = um.GetEnemyUnitList().Count;    // ei-hostin puoli

        bool end = (enemy <= 0) || (friendly <= 0);
        if (!end) return;

        bool hostWon = enemy <= 0;

        // Lähetä kullekin clientille oma viesti
        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn?.identity == null) continue;

            var pc = conn.identity.GetComponent<PlayerController>();
            if (pc == null) continue;

            bool isHostConn = conn.connectionId == 0;
            bool youWon = hostWon ? isHostConn : !isHostConn;

            pc.TargetShowEnd(conn, youWon);
        }
    }
}
