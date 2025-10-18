using UnityEngine;
using System;
using Mirror;

[RequireComponent(typeof(MoveAction))]
public class UnitAnimator : NetworkBehaviour
{

    [Header("UnitWeaponVisibilitySync")]
    [SerializeField] private WeaponVisibilitySync weaponVis;

    [Header("Animators")]
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator netAnim;

    [Header("Projectiles")]
    [SerializeField] private GameObject bulletProjectilePrefab;
    [SerializeField] private GameObject granadeProjectilePrefab;

    [Header("Spawnpoints")]
    [SerializeField] private Transform shootPointTransform;
    [SerializeField] private Transform rightHandTransform;

    private static bool IsNetworkActive() => NetworkClient.active || NetworkServer.active;

    private MoveAction _move;
    private ShootAction _shoot;
    private GranadeAction _grenade;
    private MeleeAction _melee;

    private bool useNetwork;

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!netAnim)  netAnim  = GetComponent<NetworkAnimator>();
       // if (!netId)    netId    = GetComponent<NetworkIdentity>();

        useNetwork = NetMode.IsOnline
             && netAnim != null
             && (isServer || isOwned);   // NetworkBehaviourin omat propertyt
        
        TryGetComponent(out _move);
        TryGetComponent(out _shoot);
        TryGetComponent(out _grenade);
        TryGetComponent(out _melee);
    }

    private void OnEnable()
    {
        // Varmuus: poista ensin, tilaa sitten -> estää tuplat vaikka OnEnable ajettaisiin useasti
        if (_move)
        {
            _move.OnStartMoving -= MoveAction_OnStartMoving;
            _move.OnStopMoving -= MoveAction_OnStopMoving;
            _move.OnStartMoving += MoveAction_OnStartMoving;
            _move.OnStopMoving += MoveAction_OnStopMoving;
        }

        if (_shoot)
        {
            _shoot.OnShoot -= ShootAction_OnShoot;
            _shoot.OnShoot += ShootAction_OnShoot;
        }

        if (_grenade)
        {
            _grenade.ThrowGranade -= GrenadeAction_ThrowGranade;
            _grenade.ThrowReady -= GrenadeAction_ThrowReady;
            _grenade.ThrowGranade += GrenadeAction_ThrowGranade;
            _grenade.ThrowReady += GrenadeAction_ThrowReady;
        }

        if (_melee)
        {
            _melee.OnMeleeActionStarted -= MeleeAction_OnMeleeActionStarted;
            _melee.OnMeleeActionCompleted -= MeleeAction_OnMeleeActionCompleted;
            _melee.OnMeleeActionStarted += MeleeAction_OnMeleeActionStarted;
            _melee.OnMeleeActionCompleted += MeleeAction_OnMeleeActionCompleted;
        }
    }
    
    private void OnDisable()
    {
        if (_move)
        {
            _move.OnStartMoving -= MoveAction_OnStartMoving;
            _move.OnStopMoving  -= MoveAction_OnStopMoving;
        }
        if (_shoot)
        {
            _shoot.OnShoot -= ShootAction_OnShoot;
        }
        if (_grenade)
        {
            _grenade.ThrowGranade -= GrenadeAction_ThrowGranade;
            _grenade.ThrowReady   -= GrenadeAction_ThrowReady;
        }
        if (_melee)
        {
            _melee.OnMeleeActionStarted   -= MeleeAction_OnMeleeActionStarted;
            _melee.OnMeleeActionCompleted -= MeleeAction_OnMeleeActionCompleted;
        }
    }

    private void Start()
    {
        EquipRifle();
    }
    
    // Valitsee automaattisesti oikean verkko/offline animaation.
    public void SetTrigger(string name)
    {
        if (useNetwork) netAnim.SetTrigger(name);
        else            animator.SetTrigger(name);
    }

    private void MoveAction_OnStartMoving(object sender, EventArgs e)
    {
        animator.SetBool("IsRunning", true);
    }
    private void MoveAction_OnStopMoving(object sender, EventArgs e)
    {
        animator.SetBool("IsRunning", false);
    }

    public Transform ShootPoint => shootPointTransform;
    public GameObject BulletPrefab => bulletProjectilePrefab;

    private void ShootAction_OnShoot(object sender, ShootAction.OnShootEventArgs e)
    {
        SetTrigger("Shoot");

        Vector3 target = e.targetUnit.GetWorldPosition();
        float unitShoulderHeight = 2.5f;
        target.y += unitShoulderHeight;

        if (NetMode.IsOnline)
        {

            NetworkSync.SpawnBullet(bulletProjectilePrefab, shootPointTransform.position, target, this.GetActorId());

        }
        else
        {
            OfflineGameSimulator.SpawnBullet(bulletProjectilePrefab, shootPointTransform.position, target);
        }
    }

    private void MeleeAction_OnMeleeActionStarted(object sender, EventArgs e)
    {
        EquipMelee();
        SetTrigger("Melee");

        /*
        if (!IsNetworkActive())
        {
            animator.SetTrigger("Melee");
        }
        else
        {
            netAnim.SetTrigger("Melee");
        }
        */
        
    }
    private void MeleeAction_OnMeleeActionCompleted(object sender, EventArgs e)
    {
        EquipRifle();
    }

    private void GranadeActionStart()
    {
        weaponVis.OwnerRequestSet(rifleRight: false, rifleLeft: true, meleeLeft: false, grenade: false);
    }
    private Vector3 pendingGrenadeTarget;
    private GranadeAction pendingGrenadeAction; 
    private void GrenadeAction_ThrowGranade(object sender, EventArgs e)
    {
        pendingGrenadeAction = (GranadeAction)sender;
        pendingGrenadeTarget = pendingGrenadeAction.TargetWorld;
        GranadeActionStart();
        SetTrigger("ThrowGrenade");
   
    }

    // --------- START Grenade Animation events START -----------------------
    // Event marks is set in animation. UnitAnimations -> Throw Grenade Stand
    public void AE_PickGrenadeStand()
    {
        EguipGranade();
    }

    public Transform ThrowPoint => rightHandTransform;
    public GameObject GrenadePrefab => granadeProjectilePrefab;

    public void AE_ThrowGrenadeStandRelease()
    {
        // --- GUARD: jos pending on jo käytetty, älä tee mitään (estää tuplan samalta koneelta)
        if (pendingGrenadeAction == null) return;

        // --- GATE: onlinessa vain omistaja-client saa jatkaa (server ja ei-ownerit return)
        if (NetworkClient.active || NetworkServer.active)
        {
            var ni = GetComponentInParent<NetworkIdentity>();
            if (!(isClient && ni && ni.isOwned)) return;
        }

        // Mistä kranaatti lähtee (sama logiikka kuin luodeilla)
        Vector3 origin = rightHandTransform.position;

        // Kutsu keskitettyä synkkaa (täsmälleen kuin luodeissa)
        if (NetMode.IsOnline)
            NetworkSync.SpawnGrenade(granadeProjectilePrefab, origin, pendingGrenadeTarget, this.GetActorId());
        else
            OfflineGameSimulator.SpawnGrenade(granadeProjectilePrefab, origin, pendingGrenadeTarget);

        // Siivous kuten ennen
        pendingGrenadeAction?.OnGrenadeBehaviourComplete();
        pendingGrenadeAction = null;
    }
    
    public void AE_OnGrenadeThrowStandFinished()
    {
        EquipRifle();
    }
    //--------------- END Grenade Animation events END ---------------
    private void GrenadeAction_ThrowReady(object sender, EventArgs e)
    {
       weaponVis.OwnerRequestSet(rifleRight: false, rifleLeft: true, meleeLeft: false, grenade: false);
    }
     
    private void EquipRifle()
    {
        weaponVis.OwnerRequestSet(rifleRight: true, rifleLeft: false, meleeLeft: false, grenade: false);
    }
    private void EquipMelee()
    {
        weaponVis.OwnerRequestSet(rifleRight: true, rifleLeft: false, meleeLeft: true, grenade: false);
    }
    private void EguipGranade()
    {
        weaponVis.OwnerRequestSet(rifleRight: false, rifleLeft: true, meleeLeft: false, grenade: true);
    }
}
