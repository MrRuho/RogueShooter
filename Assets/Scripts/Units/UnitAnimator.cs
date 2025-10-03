using UnityEngine;
using System;
using Mirror;

[RequireComponent(typeof(MoveAction))]
public class UnitAnimator : NetworkBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject bulletProjectilePrefab;
    [SerializeField] private GameObject granadeProjectilePrefab;
    [SerializeField] private Transform shootPointTransform;
    [SerializeField] private Transform rifleTransform;
    [SerializeField] private Transform GrenadeTransform;

    [SerializeField] private Transform rifleTransformOffHand;
    [SerializeField] private Transform meleeTransform;

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
        animator.SetTrigger("Shoot");
        Vector3 target = e.targetUnit.GetWorldPosition();
        float unitShoulderHeight = 2.5f;
        target.y += unitShoulderHeight;
        NetworkSync.SpawnBullet(bulletProjectilePrefab, shootPointTransform.position, target);
    }
    
    private Vector3 pendingGrenadeTarget;
    private GranadeAction pendingGrenadeAction;

    private void GrenadeAction_ThrowGranade(object sender, EventArgs e)
    {
        pendingGrenadeAction = (GranadeAction)sender;
        pendingGrenadeTarget = pendingGrenadeAction.TargetWorld;
        GranadeActionStart();
        animator.SetTrigger("ThrowGrenade");
    }

    // Animation Event. ThrowGrenadeStand. When the animation reaches the event marker, this funktion tricers.
    // This event mark is set in animation. UnitAnimations -> Throw Grenade Stand
    public void AE_ThrowGrenadeStandRelease()
    {
        Debug.Log("[AE_ThrowGrenadeStandRelease] Throw grenade!");
        Vector3 origin = shootPointTransform.position;

        GrenadeTransform.gameObject.SetActive(false);
        OnelineVisibilitySync(false, GrenadeTransform);

        NetworkSync.SpawnGrenade(granadeProjectilePrefab, origin, pendingGrenadeTarget);
        pendingGrenadeAction?.OnGrenadeBehaviourComplete(); // nyt vasta päätetään action
        pendingGrenadeAction = null;
    }

    // Animation Event. PickGrenadeStand When the animation reaches the event marker, this funktion tricers.
    // This event mark is set in animation. UnitAnimations -> Throw Grenade Stand
    public void  AE_PickGrenadeStand()
    {
        EguipGranade();
    }

    private void MeleeAction_OnMeleeActionStarted(object sender, EventArgs e)
    {
        EquipMelee();
        animator.SetTrigger("Melee");
    }

    private void MeleeAction_OnMeleeActionCompleted(object sender, EventArgs e)
    {
        EquipRifle();
    }
    
    private void GrenadeAction_ThrowReady(object sender, EventArgs e)
    {
        EquipRifle();
    }

    private void EquipRifle()
    {
        rifleTransform.gameObject.SetActive(true);
        rifleTransformOffHand.gameObject.SetActive(false);
        meleeTransform.gameObject.SetActive(false);
        GrenadeTransform.gameObject.SetActive(false);

        OnelineVisibilitySync(true, rifleTransform);
        OnelineVisibilitySync(false, rifleTransformOffHand);
        OnelineVisibilitySync(false, meleeTransform);
        OnelineVisibilitySync(false, GrenadeTransform);

    }

    private void EquipMelee()
    {
        rifleTransform.gameObject.SetActive(true);
        meleeTransform.gameObject.SetActive(true);
        OnelineVisibilitySync(true, rifleTransform);
        OnelineVisibilitySync(true, meleeTransform);
        
    }

    private void GranadeActionStart()
    {
        rifleTransform.gameObject.SetActive(false);
        rifleTransformOffHand.gameObject.SetActive(true);
        
        OnelineVisibilitySync(false, rifleTransform);
        OnelineVisibilitySync(true, rifleTransformOffHand);
    }

    private void EguipGranade()
    {
        GrenadeTransform.gameObject.SetActive(true);
        OnelineVisibilitySync(true, GrenadeTransform);
    }

    private void OnelineVisibilitySync(bool visible, Transform item)
    {
        if (item == null)
        {
            Debug.LogWarning("Item transform is null.");
            return;
        }
        if (NetworkClient.active || NetworkServer.active)
        {
            var visibility = item.GetComponent<NetVisibility>();
            if (visibility != null)
            {
                visibility.SetVisibleAny(visible);
            }
        }
    }

}
