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

    [SerializeField] private GameObject bulletProjectilePrefab;
    [SerializeField] private GameObject granadeProjectilePrefab;
    [SerializeField] private Transform shootPointTransform;
    [SerializeField] private Transform rifleTransform;

    private static bool IsNetworkActive() => NetworkClient.active || NetworkServer.active;

    private void Awake()
    {
        if (TryGetComponent<MoveAction>(out MoveAction moveAction))
        {
            moveAction.OnStartMoving += MoveAction_OnStartMoving;
            moveAction.OnStopMoving += MoveAction_OnStopMoving;
        }

        if (TryGetComponent<ShootAction>(out ShootAction shootAction))
        {
            shootAction.OnShoot += ShootAction_OnShoot;
        }

        if (TryGetComponent<GranadeAction>(out GranadeAction granadeAction))
        {
            granadeAction.ThrowGranade += GrenadeAction_ThrowGranade;
            granadeAction.ThrowReady += GrenadeAction_ThrowReady;
        }

        if (TryGetComponent<MeleeAction>(out MeleeAction meleeAction))
        {
            meleeAction.OnMeleeActionStarted += MeleeAction_OnMeleeActionStarted;
            meleeAction.OnMeleeActionCompleted += MeleeAction_OnMeleeActionCompleted;
        }
    }

    private void Start()
    {
        EquipRifle();
    }

    void OnDisable()
    {
        if (TryGetComponent<MoveAction>(out MoveAction moveAction))
        {
            moveAction.OnStartMoving -= MoveAction_OnStartMoving;
            moveAction.OnStopMoving -= MoveAction_OnStopMoving;
        }

        if (TryGetComponent<ShootAction>(out ShootAction shootAction))
        {
            shootAction.OnShoot -= ShootAction_OnShoot;
        }

        if (TryGetComponent<GranadeAction>(out GranadeAction granadeAction))
        {
            granadeAction.ThrowGranade -= GrenadeAction_ThrowGranade;
            granadeAction.ThrowReady -= GrenadeAction_ThrowReady;
        }

        if (TryGetComponent<MeleeAction>(out MeleeAction meleeAction))
        {
            meleeAction.OnMeleeActionStarted -= MeleeAction_OnMeleeActionStarted;
            meleeAction.OnMeleeActionCompleted -= MeleeAction_OnMeleeActionCompleted;
        }
    }

    private void MoveAction_OnStartMoving(object sender, EventArgs e)
    {
        animator.SetBool("IsRunning", true);
    }
    private void MoveAction_OnStopMoving(object sender, EventArgs e)
    {
        animator.SetBool("IsRunning", false);
    }

    private void ShootAction_OnShoot(object sender, ShootAction.OnShootEventArgs e)
    {
        if (!IsNetworkActive())
        {
            animator.SetTrigger("Shoot");
        }
        else
        {
            netAnim.SetTrigger("Shoot");
        }

        Vector3 target = e.targetUnit.GetWorldPosition();

        float unitShoulderHeight = 2.5f;
        target.y += unitShoulderHeight;
        NetworkSync.SpawnBullet(bulletProjectilePrefab, shootPointTransform.position, target);
    }

    private void MeleeAction_OnMeleeActionStarted(object sender, EventArgs e)
    {
        EquipMelee();
        if (!IsNetworkActive())
        {
            animator.SetTrigger("Melee");
        }
        else
        {
            netAnim.SetTrigger("Melee");
        }
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
        if (!IsNetworkActive())
        {
            animator.SetTrigger("ThrowGrenade");
        }
        else
        { 
            netAnim.SetTrigger("ThrowGrenade");
        }
    }

    // --------- START Grenade Animation events START -----------------------
    // Event marks is set in animation. UnitAnimations -> Throw Grenade Stand
    public void AE_PickGrenadeStand()
    {
        EguipGranade();
    }
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
        Vector3 origin = rifleTransform.position;

        // Kutsu keskitettyä synkkaa (täsmälleen kuin luodeissa)
        NetworkSync.SpawnGrenade(granadeProjectilePrefab, origin, pendingGrenadeTarget);

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
