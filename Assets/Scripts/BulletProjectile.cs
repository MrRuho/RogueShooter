using Mirror;
using UnityEngine;

public class BulletProjectile : NetworkBehaviour
{
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private Transform bulletHitVfxPrefab;

    [SyncVar] private Vector3 targetPosition;

    //[Server]
    public void Setup(Vector3 targetPosition)
    {
        this.targetPosition = targetPosition;
        // Implement bullet movement towards the target here
        // For example, you could use a simple linear interpolation or a physics-based approach
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // käynnistä trail kaikilla, jos ei jo päällä
        if (trailRenderer && !trailRenderer.emitting) trailRenderer.emitting = true;
    }

    private void Update()
    {
        Vector3 moveDirection = (targetPosition - transform.position).normalized;

        float distanceBeforeMoving = Vector3.Distance(transform.position, targetPosition);

        float moveSpeed = 200f; // Adjust the speed as needed
        transform.position += moveSpeed * Time.deltaTime * moveDirection;

        float distanceAfterMoving = Vector3.Distance(transform.position, targetPosition);
        /*
        if (distanceBeforeMoving < distanceAfterMoving)
        {
            transform.position = targetPosition;

            trailRenderer.transform.parent = null;

            Destroy(gameObject);

            Instantiate(bulletHitVfxPrefab, targetPosition, Quaternion.identity);
        }
        */
        
        
        if (distanceBeforeMoving < distanceAfterMoving)
        {
            transform.position = targetPosition;

            if (trailRenderer) trailRenderer.transform.parent = null;

            // Hit-VFX jokaiselle clientille
            if (bulletHitVfxPrefab)
                Instantiate(bulletHitVfxPrefab, targetPosition, Quaternion.identity);

            // Tuhoaminen: server tuhoaa verkko-objektin, client paikallisen varalta
            if (isServer) NetworkServer.Destroy(gameObject);
            else Destroy(gameObject);
        }
        
    }
}
