# **Rogue Shooter verkkoarkkitehtuuri suunnitelma**

Ehdotuksia verkkoarkkitehtuurin rakenteelle
Olen tutkinut projektisi NetworkSync ja NetworkSyncAgent -luokkia sekä nykyistä Mirror-pohjaista arkkitehtuuriasi. Lähestymistapasi on oikeaan suuntaan menevä, mutta tässä on useita tärkeitä parannusehdotuksia ja huomioita tulevaa dedikoidun serverin siirtymää varten.

**1. Nykyisen rakenteen arviointi**
Vahvuudet:

Keskitetty NetworkSync static-luokka toimii hyvänä abstraktiona pelikoodin ja verkkokerroksen välillä
NetworkSyncAgent hoitaa Command/RPC-viestinnän
Server-authoritative periaate on jo osittain käytössä (esim. damage-laskenta)
Heikkoudet:

Offline-logiikka on vielä sekoittunut verkkologiikkaan (NetworkSync.cs sisältää offline-fallbackit)
Client voi edelleen lähettää mielivaltaisia arvoja (esim. damage amount)
SyncVar-pohjainen synkronointi ei skaalaudu hyvin dedikoidulle serverille


**2. Suositellut arkkitehtuurimuutokset**

A) Selkeä kerrosarkkitehtuuri


GameLogic (Units, Actions, HealthSystem)
    ↓
NetworkAdapter (nykyinen NetworkSync)
    ↓
NetworkBackend (NetworkSyncAgent + uusi Server-logiikka)


**Käytännön toteutus**

public static class NetworkSync
{
    // Vain interface-metodit, ei offline-logiikkaa!
    public static void SpawnBullet(GameObject bulletPrefab, Vector3 spawnPos, Vector3 targetPos, uint actorNetId)
    {
        if (NetworkServer.active)
        {
            ServerSpawnBullet(bulletPrefab, spawnPos, targetPos, actorNetId);
            return;
        }

        if (NetworkClient.active)
        {
            ClientRequestSpawnBullet(spawnPos, targetPos, actorNetId);
            return;
        }
        
        // Offline-tila siirretään erilliseen luokkaan
        OfflineGameSimulator.SpawnBullet(bulletPrefab, spawnPos, targetPos);
    }
}

**B) Erillinen offline-simulaattori**
Luo uusi luokka /Assets/Scripts/Oneline/OfflineGameSimulator.cs:

public static class OfflineGameSimulator
{
    public static void SpawnBullet(GameObject bulletPrefab, Vector3 spawnPos, Vector3 targetPos)
    {
        var bullet = Object.Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        if (bullet.TryGetComponent<BulletProjectile>(out var bp))
            bp.Setup(targetPos);
    }

    public static void ApplyDamage(Unit target, int amount, Vector3 hitPos)
    {
        target.GetComponent<HealthSystem>()?.Damage(amount, hitPos);
    }
}

Hyöty: Kun siirrät dedikoidulle serverille, voit poistaa OfflineGameSimulator-luokan kokonaan ilman, että se vaikuttaa verkko-osaan.

***3. Clientin manipuloinnin estäminen***
Tämä on kriittinen turvallisuuskysymys. Nykyinen toteutuksesi on haavoittuvainen.

***Esimerkki Ongelma 1: Client määrittää damage-määrän***

// VÄÄRIN! Client voi lähettää mitä tahansa damage-arvoa
NetworkSyncAgent.Local.CmdApplyDamage(actorNetId, ni.netId, amount, hitPosition);

Ratkaisu: Server laskee kaiken
// UnitAnimator.cs tai ShootAction.cs
// CLIENT lähettää VAIN hyökkäystyypin, ei damage-määrää
public void ExecuteShoot(Unit target)
{
    Vector3 hitPos = target.GetWorldPosition();
    NetworkSync.RequestAttack(actorUnit.netId, target.netId, AttackType.RangedShot, hitPos);
}

// NetworkSyncAgent.cs
[Command(requiresAuthority = true)]
public void CmdRequestAttack(uint actorNetId, uint targetNetId, AttackType attackType, Vector3 hitPos)
{
    if (!RightOwner(actorNetId)) return;
    
    // SERVER laskee damage-määrän
    var actor = GetUnitByNetId(actorNetId);
    var target = GetUnitByNetId(targetNetId);
    
    if (actor == null || target == null) return;
    
    // SERVER tekee validoinnin
    if (!CanAttack(actor, target, attackType)) return;
    
    // SERVER laskee damage-määrän
    int damage = CalculateDamage(actor, target, attackType);
    
    // SERVER soveltaa damage-määrän
    target.GetComponent<HealthSystem>().Damage(damage, hitPos);
    
    // Broadcast UI-päivitys
    ServerBroadcastHp(target, target.GetComponent<HealthSystem>().GetHealth(), ...);
}

private int CalculateDamage(Unit attacker, Unit target, AttackType type)
{
    var weapon = attacker.GetCurrentWeapon();
    int baseDmg = weapon.baseDamage;
    
    // Lisää hit/crit/graze -logiikka tähän
    // Lisää cover-bonus vähennyksét
    // Lisää range-vaikutukset
    
    return Mathf.Max(0, baseDmg);
}

private bool CanAttack(Unit attacker, Unit target, AttackType type)
{
    // Tarkista: onko attacker range:ssa?
    // Tarkista: onko attacker vuorossa?
    // Tarkista: onko attacker riittävästi AP?
    // Tarkista: onko Line of Sight?
    return true;
}

**Esimerkki Ongelma 2: Private kentät eivät ole turvallisia**
// HealthSystem.cs
[SerializeField] private int health = 100;  // Client voi muuttaa muistissa!

Ratkaisu: Server-side authoritative data

public class HealthSystem : NetworkBehaviour
{
    // Vain server kirjoittaa, clientit lukevat
    [SyncVar(hook = nameof(OnHealthChanged))]
    private int health = 100;
    
    [SyncVar]
    private int healthMax = 100;
    
    // Vain serveri voi kutsua tätä metodia
    [Server]
    public void Damage(int amount, Vector3 hitPos)
    {
        if (!NetworkServer.active) return;  // Varmista että vain server
        
        health = Mathf.Max(0, health - amount);
        
        if (health <= 0)
            Die();
    }
    
    // Clientit voivat lukea, mutta eivät kirjoittaa
    public int GetHealth() => health;
    
    void OnHealthChanged(int oldVal, int newVal)
    {
        OnDamaged?.Invoke(this, EventArgs.Empty);
    }
}

**Esimerkki Ongelma 3: UnderFire-arvon suojaus**
// Unit.cs - NYKYINEN (huono)
[SyncVar] private bool underFire = false;

Ongelma: Client voi muuttaa SyncVar-arvoa lokaalisti ennen synkronointia.

Ratkaisu: Hook-funktio ja server-validointi

// Unit.cs
[SyncVar(hook = nameof(OnUnderFireChanged))]
private bool underFire = false;

[Server]
public void SetUnderFireServer(bool value)
{
    if (!NetworkServer.active) return;
    underFire = value;  // Vain server voi asettaa
}

void OnUnderFireChanged(bool oldVal, bool newVal)
{
    // Client saa päivityksen vasta kun server on validoinut
}

**4. Dedikoidun serverin siirtymä**
Valmistelut nyt

1. Poista kaikki offline-logiikka verkkokerroksesta:
/Assets/Scripts/
├── Oneline/
│   ├── Sync/
│   │   ├── NetworkSync.cs          (vain interface)
│   │   ├── NetworkSyncAgent.cs     (Commands/RPCs)
│   │   └── ServerGameLogic.cs      (uusi: server-puolen validointi)
│   └── Offline/
│       └── OfflineGameSimulator.cs (yksinpeli-logiikka)

2. Luo erillinen ServerGameLogic.cs:
[Server]
public class ServerGameLogic : NetworkBehaviour
{
    public static ServerGameLogic Instance { get; private set; }
    
    void Awake()
    {
        if (NetworkServer.active)
            Instance = this;
    }
    
    // Kaikki server-puolen validointi ja laskenta tänne
    public bool ValidateAction(Unit actor, BaseAction action)
    {
        if (!TurnSystem.Instance.IsPlayerTurn(actor.Team)) return false;
        if (actor.GetActionPoints() < action.GetActionPointsCost()) return false;
        return true;
    }
    
    public int CalculateDamage(Unit attacker, Unit target, AttackType type)
    {
        // Kaikki damage-laskenta serverillä
    }
    
    public bool ValidateMovement(Unit unit, GridPosition targetPos)
    {
        // Tarkista että liike on laillinen
    }
}

3. Headless build -valmistelut:

Lisää build-script /Assets/Editor/ServerBuilder.cs:
using UnityEditor;

public class ServerBuilder
{
    [MenuItem("Build/Build Dedicated Server")]
    public static void BuildServer()
    {
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new[] { "Assets/Scenes/ServerScene.unity" };
        options.locationPathName = "Builds/Server/RogueShooter_Server.exe";
        options.target = BuildTarget.StandaloneWindows64;
        options.subtarget = (int)StandaloneBuildSubtarget.Server;
        
        BuildPipeline.BuildPlayer(options);
    }
}

**Siirtymä dedikoidulle serverille (tulevaisuudessa)**
1. Luo erillinen server-scene:

Poista kaikki UI-elementit
Poista client-side visualisoinnit
Säilytä vain gameplay-logiikka

public class GameNetworkManager : NetworkManager
{
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        if (mode == NetworkManagerMode.ServerOnly)
        {
            // Dedikoidun serverin alustus
            ServerGameLogic.Instance.Initialize();
        }
    }
}

3. Siirrä NetworkSync ja ServerGameLogic dedikoidulle serverille:

Nämä luokat siirretään server-buildin mukana
OfflineGameSimulator jätetään pois server-buildista
Client-build sisältää vain UI:n ja visualisoinnin
