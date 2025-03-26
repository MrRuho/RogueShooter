using Mirror;
using UnityEngine;

public class Unit : NetworkBehaviour
{
    private Vector3 targetPosition;

    private void Update()
    {   

        // Move to mouse position when left mouse button is clicked
        if(Input.GetMouseButtonDown(0))
        {
            Debug.Log("Left mouse button clicked");
            targetPosition = MouseWorld.GetMouseWorldPosition();
            Move(targetPosition);
        }
       
        
        // Move to target position and stop when close enough
        float stoppingDistance = 0.2f;
        if (Vector3.Distance(transform.position, targetPosition) > stoppingDistance)
        {
            Vector3 moveDirection = (targetPosition - transform.position).normalized;
            float moveSpeed = 4f;
            transform.position += moveSpeed * Time.deltaTime * moveDirection;
        }
    }

    // Move to new target position
    private void Move(Vector3 newTargetPosition)
    {
        targetPosition = newTargetPosition;
    }
}
