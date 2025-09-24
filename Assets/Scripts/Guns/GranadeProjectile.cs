using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.UIElements;

public class GrenadeProjectile : NetworkBehaviour
{
    public static event EventHandler OnAnyGranadeExploded;

    [SerializeField] private Transform granadeExplodeVFXPrefab;

    [SerializeField] private float damageRadius = 4f;
    [SerializeField] private int damage = 30;
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private LayerMask groundMask = ~0; // säädä omiin layereihin
    [SerializeField] private float rayStartHeight = 20f;
    [SerializeField] private float rayDepth = 200f;

    [SerializeField] private AnimationCurve arcYAnimationCurve;

    [SyncVar] private Vector3 targetPosition;

    private float totalDistance;
    private Vector3 positionXZ;

    //private Action onGrenadeBehaviourComplete;



    public override void OnStartClient()
    {
        base.OnStartClient();
    }

    public void Setup(Vector3 targetWorld) // kutsutaan ennen Spawnia
    {
        targetPosition = SnapToGround(targetWorld);
        totalDistance = Vector3.Distance(transform.position, targetPosition);

        positionXZ = transform.position;
        positionXZ.y = 0;
        totalDistance = Vector3.Distance(positionXZ, targetPosition);
      
    }
    
    private Vector3 SnapToGround(Vector3 worldXZ)
    {
        /*
        // Ray alas, haku maasta
        var from = worldXZ + Vector3.up * rayStartHeight;
        if (Physics.Raycast(from, Vector3.down, out var hit, rayStartHeight + rayDepth, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        // fallback: pidä XZ, laita y=0 (tai scene-maan oletuskorkeus)
        */
        totalDistance = Vector3.Distance(transform.position, targetPosition);

        positionXZ = transform.position;
        positionXZ.y = 0;
        totalDistance = Vector3.Distance(positionXZ, targetPosition);

        return new Vector3(worldXZ.x, 0f, worldXZ.z);
    }

    private void Update()
    {
        Vector3 moveDir = (targetPosition - positionXZ).normalized;

        positionXZ += moveSpeed * Time.deltaTime * moveDir;

        float distance = Vector3.Distance(positionXZ, targetPosition);
        float distanceNormalized = 1 - distance / totalDistance;

        float maxHeight = totalDistance/ 4f;
        float positionY = arcYAnimationCurve.Evaluate(distanceNormalized) * maxHeight;
        transform.position = new Vector3(positionXZ.x, positionY, positionXZ.z);

        float reachedTargetDistance = .2f;
        if (Vector3.Distance(positionXZ, targetPosition) < reachedTargetDistance)
        {

            Collider[] colliderArray = Physics.OverlapSphere(targetPosition, damageRadius);

            foreach (Collider collider in colliderArray)
            {
                if (collider.TryGetComponent<Unit>(out Unit targetUnit))
                {

                    NetworkSync.ApplyDamage(targetUnit, damage);
                }
            }

            OnAnyGranadeExploded?.Invoke(this, EventArgs.Empty);

            Instantiate(granadeExplodeVFXPrefab, targetPosition + Vector3.up *1f, Quaternion.identity);
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
