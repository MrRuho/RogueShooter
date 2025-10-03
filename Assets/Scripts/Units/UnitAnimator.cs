using UnityEngine;
using System;
using Mirror;

[RequireComponent(typeof(MoveAction))]
public class UnitAnimator : NetworkBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator netAnim;
    [SerializeField] private GameObject bulletProjectilePrefab;
    [SerializeField] private GameObject granadeProjectilePrefab;
    [SerializeField] private Transform shootPointTransform;
    [SerializeField] private Transform rifleTransform;
    [SerializeField] private Transform GrenadeTransform;

    [SerializeField] private Transform rifleTransformOffHand;
    [SerializeField] private Transform meleeTransform;

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

    // Animation Event. ThrowGrenadeStand. When the animation reaches the event marker, this funktion tricers.
    // This event mark is set in animation. UnitAnimations -> Throw Grenade Stand
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

        // Piilota/grace-visuals kuten ennen
        GrenadeTransform.gameObject.SetActive(false);
        OnelineVisibilitySync(false, GrenadeTransform);

        // Kutsu keskitettyä synkkaa (täsmälleen kuin luodeissa)
        NetworkSync.SpawnGrenade(granadeProjectilePrefab, origin, pendingGrenadeTarget);

        // Siivous kuten ennen
        pendingGrenadeAction?.OnGrenadeBehaviourComplete();
        pendingGrenadeAction = null;
    }

    // Animation Event. PickGrenadeStand When the animation reaches the event marker, this funktion tricers.
    // This event mark is set in animation. UnitAnimations -> Throw Grenade Stand
    public void AE_PickGrenadeStand()
    {
        EguipGranade();
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

        var visibility = item.GetComponent<NetVisibility>();
        if (!visibility) return;

        if (NetworkServer.active)
        {
            visibility.ServerSetVisible(visible);
        }
    }

}
