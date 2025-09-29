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
    [SerializeField] private Transform meleeTransform;

    //GranadeAction granadeAction;

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
            granadeAction.ThrowGranade += granadeAction_ThrowGranade;
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
            granadeAction.ThrowGranade -= granadeAction_ThrowGranade;
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
        target.y = shootPointTransform.position.y;
        NetworkSync.SpawnBullet(bulletProjectilePrefab, shootPointTransform.position, target);
    }

    private void granadeAction_ThrowGranade(object sender, EventArgs e)
    {

        var action = (GranadeAction)sender;

        //DoDo
        // animator.SetTrigger("ThrowGranande");
        // Testing
        StartCoroutine(NotifyAfterDelay(action, 2f));
        // -----------------------------------------

        Vector3 origin = shootPointTransform.position;
        Vector3 target = action.TargetWorld;
        NetworkSync.SpawnGrenade(granadeProjectilePrefab, origin, target);

    }
    private System.Collections.IEnumerator NotifyAfterDelay(GranadeAction action, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        action.OnGrenadeBehaviourComplete();
    }

    private void MeleeAction_OnMeleeActionStarted(object sender, EventArgs e)
    {
        EquipMelee();
        animator.SetTrigger("Melee");
    }

    private void MeleeAction_OnMeleeActionCompleted(object sender, EventArgs e)
    {
        EquipRifle();
        //animator.SetTrigger("Idle");
    }

    private void EquipRifle()
    {
        rifleTransform.gameObject.SetActive(true);
        meleeTransform.gameObject.SetActive(false);
        OnelineVisibilitySync(true, rifleTransform);
        OnelineVisibilitySync(false, meleeTransform);
      
    }

    private void EquipMelee()
    {
        rifleTransform.gameObject.SetActive(true);
        meleeTransform.gameObject.SetActive(true);
        OnelineVisibilitySync(true, rifleTransform);
        OnelineVisibilitySync(true, meleeTransform);
        
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
