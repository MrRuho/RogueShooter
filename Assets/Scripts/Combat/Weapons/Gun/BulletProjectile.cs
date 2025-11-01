using Mirror;
using UnityEngine;

public class BulletProjectile : NetworkBehaviour
{
    [SyncVar] public uint actorUnitNetId;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private Transform bulletHitVfxPrefab;

    [SyncVar] private Vector3 targetPosition;

 
    public void Setup(Vector3 targetPosition)
    {
        this.targetPosition = targetPosition;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (trailRenderer && !trailRenderer.emitting) trailRenderer.emitting = true;
    }

    private void Update()
    {
        Vector3 moveDirection = (targetPosition - transform.position).normalized;

        float distanceBeforeMoving = Vector3.Distance(transform.position, targetPosition);

        float moveSpeed = 200f; // Adjust the speed as needed
        transform.position += moveSpeed * Time.deltaTime * moveDirection;

        float distanceAfterMoving = Vector3.Distance(transform.position, targetPosition);
       
            // Check if we've reached or passed the target position 
        if (distanceBeforeMoving < distanceAfterMoving)
        {
            transform.position = targetPosition;

            if (trailRenderer) trailRenderer.transform.parent = null;
            /*
            if (bulletHitVfxPrefab)
                Instantiate(bulletHitVfxPrefab, targetPosition, Quaternion.identity);
            */

            if (bulletHitVfxPrefab)
            {
                SpawnRouter.SpawnLocal(
                    bulletHitVfxPrefab.gameObject,
                    targetPosition,
                    Quaternion.identity,
                    source: transform   // -> sama scene kuin luodilla
                );
            }
            
            // Network-aware destruction
            if (isServer) NetworkServer.Destroy(gameObject);
            else Destroy(gameObject);
        }
        
    }
}
