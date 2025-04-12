using Mirror;
using UnityEngine;

public class Unit : NetworkBehaviour
{
    [SerializeField] private Animator unitAnimator;
    private Vector3 targetPosition;

    private void Awake() 
    {
        // Initialize the target position to the current position of the unit
        targetPosition = transform.position;
    }

    private void Update()
    {   
    
        float stoppingDistance = 0.2f;
        if (Vector3.Distance(transform.position, targetPosition) > stoppingDistance)
        {

            // Move towards the target position
            // Calculate the direction to the target position and normalize it
            Vector3 moveDirection = (targetPosition - transform.position).normalized;
            float moveSpeed = 4f;
            transform.position += moveSpeed * Time.deltaTime * moveDirection;

             // Rotate towards the target position
            float rotationSpeed = 10f;
            transform.forward = Vector3.Lerp(transform.forward, moveDirection, Time.deltaTime * rotationSpeed);

            unitAnimator.SetBool("IsRunning", true);
        } else 
        {
            unitAnimator.SetBool("IsRunning", false);
        }
    }

    // Move to new target position
    public void Move(Vector3 newTargetPosition)
    {
        targetPosition = newTargetPosition;
    }
}

// This script is responsible for controlling the unit's movement and animation. It uses Mirror for networking functionality.
// The unit moves towards a target position and plays the running animation when moving. The target position can be updated using the Move method.
