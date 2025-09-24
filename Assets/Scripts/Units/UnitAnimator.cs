using UnityEngine;
using System;

[RequireComponent(typeof(MoveAction))]
public class UnitAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject bulletProjectilePrefab;
    [SerializeField] private GameObject granadeProjectilePrefab;
    [SerializeField] private Transform shootPointTransform;

    GranadeAction granadeAction;

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
    }

    /*
    void OnEnable()
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
    }
    */

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

        Vector3 origin = shootPointTransform.position;
        Vector3 target = action.TargetWorld; // GranadeAction asettaa tämän TakeActionissa
        target.y = origin.y;                 // sama taso kuin luodeissa

        //DoDo
        // animator.SetTrigger("ThrowGranande");
        // Testing
        StartCoroutine(NotifyAfterDelay(action, 2f));
        // Sama kuvio kuin bulleteissa:
        NetworkSync.SpawnGrenade(granadeProjectilePrefab, origin, target);

    }
    private System.Collections.IEnumerator NotifyAfterDelay(GranadeAction action, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        action.OnGrenadeBehaviourComplete();
    }

}
