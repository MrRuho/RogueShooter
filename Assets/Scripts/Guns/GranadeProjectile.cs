using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class GrenadeProjectile : NetworkBehaviour
{

    [SerializeField] private float damageRadius = 4f;
    [SerializeField] private int damage = 30;
    [SerializeField] private float moveSpeed = 15f;

    [SyncVar] private Vector3 targetPosition;

    //private Action onGrenadeBehaviourComplete;



    public override void OnStartClient()
    {
        base.OnStartClient();
    }

    public void Setup(Vector3 targetWorld) // kutsutaan ennen Spawnia
    {
        targetPosition = targetWorld;
    }

    private void Update()
    {
        Vector3 moveDir = (targetPosition - transform.position).normalized;

        transform.position += moveSpeed * Time.deltaTime * moveDir;

        float reachedTargetDistance = .2f;
        if (Vector3.Distance(transform.position, targetPosition) < reachedTargetDistance)
        {

            Collider[] colliderArray = Physics.OverlapSphere(targetPosition, damageRadius);

            foreach (Collider collider in colliderArray)
            {
                if (collider.TryGetComponent<Unit>(out Unit targetUnit))
                {

                    NetworkSync.ApplyDamage(targetUnit, damage);
                }
            }

            // Network-aware destruction
            if (isServer) NetworkServer.Destroy(gameObject);
            else Destroy(gameObject);

           // onGrenadeBehaviourComplete();
        }
    }

/*
    public void Setup(GridPosition targetGridPosition, Action onGrenadeBehaviourComplete)
    {
        this.onGrenadeBehaviourComplete = onGrenadeBehaviourComplete;
        targetPosition = LevelGrid.Instance.GetWorldPosition(targetGridPosition);
    }
*/
}
