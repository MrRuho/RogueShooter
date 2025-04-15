using Mirror;
using UnityEngine;

// This script is responsible for controlling the unit's movement and animation. It uses Mirror for networking functionality.
// The unit moves towards a target position and plays the running animation when moving. The target position can be updated using the Move method.

public class Unit : NetworkBehaviour
{
    //private NetworkAnimator networkAnimator;
    [SerializeField] private Animator unitAnimator;
    private Vector3 targetPosition;
    private GridPosition gridPosition;


    private void Awake() 
    {
       // networkAnimator = GetComponent<NetworkAnimator>();
        // Initialize the target position to the current position of the unit
        targetPosition = transform.position;
    }

    private void Start()
    {
        gridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
        LevelGrid.Instance.AddUnitAtGridPosition(gridPosition, this);
    }

    private void Update()
    {   
        // Check if the unit is controlled by the local player
        // If not, return and do not update the unit's position or animation
        if(AuthorityHelper.HasLocalControl(this)) return;

       
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

        GridPosition newGridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
        if (newGridPosition != gridPosition)
        {
            LevelGrid.Instance.UnitMoveToGridPosition(gridPosition, newGridPosition, this);
            gridPosition = newGridPosition;
        }
    }

    // Move to new target position
    public void Move(Vector3 newTargetPosition)
    {
        targetPosition = newTargetPosition;
    }
}
