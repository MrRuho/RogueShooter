using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// Client-puolen apu, joka merkitsee yksiköt vihollisiksi oman näkökulman mukaan.
/// Oletus:
/// - Jokaisella Unitilla on NetworkIdentity
/// - Omistus tarkistetaan: unit.netIdentity.hasAuthority (tai sinulla on oma OwnerNetId-kenttä)
/// - Unitissa on public bool isEnemy (tai SetEnemy(bool)) jota käytetään lyöntilogiikassa
/// </summary>
public class PvpPerception : MonoBehaviour
{
     public static void ApplyEnemyFlagsLocally()
    {
        // Hae kaikki Unitit skenestä
        var units = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        foreach (var u in units)
        {
            var ni = u.GetComponent<NetworkIdentity>();
            if (ni == null)
                continue;

            bool unitIsMine = ni.isOwned; // jos käytätte omaa owner-kenttää, korvaa tämä tarkistus
            bool enemy = !unitIsMine;          // vastustajan yksiköt ovat aina enemy

            // Jos haluat pakottaa myös, että "ei oma vuoro" -> omat yksiköt eivät toimi, tee se UI/ohjauksessa.
            // Täällä vain vihollisuus-näkymä:
            SetUnitEnemyFlag(u, enemy);
        }
    }

    static void SetUnitEnemyFlag(Unit u, bool enemy)
    {
        // Muokkaa nämä nimet sinun Unit-skriptisi mukaan:
        // 1) property/field
        var field = typeof(Unit).GetField("isEnemy");
        if (field != null)
        {
            field.SetValue(u, enemy);
            return;
        }

        // 2) Setteri-metodi
        var m = typeof(Unit).GetMethod("SetEnemy", new[] { typeof(bool) });
        if (m != null)
        {
            m.Invoke(u, new object[] { enemy });
            return;
        }

        Debug.LogWarning("[PvP] Unitilta puuttuu isEnemy/SetEnemy(bool). Lisää jompikumpi.");
    }
}
