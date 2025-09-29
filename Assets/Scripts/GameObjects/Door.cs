using UnityEngine;
using System;
public class Door : MonoBehaviour
{
    [SerializeField] private bool isOpen = false;
    private GridPosition gridPosition;
    private Animator animator;
    private Action onInteractComplete;
    private bool isActive;
    private float timer;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        // Prevent  animation from playing on start
        if (isOpen)
        {
            animator.SetBool("IsOpen", true);
            animator.Play("DoorOpen", 0, 1f);
        } else
        {
            animator.SetBool("IsOpen", false);
            animator.Play("DoorClose", 0, 1f);
        }

        animator.Update(0f);
       
    }
    private void Start()
    {
        gridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
        LevelGrid.Instance.SetDoorAtGridPosition(gridPosition, this);

        if (isOpen)
        {
            OpenDoor();
        }
        else
        {
            CloseDoor();
        }   
    }

    private void Update()
    {   
        if(!isActive) return;
   
        timer -= Time.deltaTime;
        if (timer < 0f)
        {
            isActive = false;
            onInteractComplete();           
        }
    }

    public void Interact(Action onInteractComplete)
    {
        this.onInteractComplete = onInteractComplete;
        isActive = true;
        timer = 0.5f; // Duration of the interaction animation
        if (isOpen)
        {
            CloseDoor();
        }
        else
        {
            OpenDoor();
        }
    }

    private void OpenDoor()
    {
        isOpen = true;
        animator.SetBool("IsOpen", isOpen);
        PathFinding.Instance.SetIsWalkableGridPosition(gridPosition, true);
    }

    private void CloseDoor()
    {
        isOpen = false;
        animator.SetBool("IsOpen", isOpen);
        PathFinding.Instance.SetIsWalkableGridPosition(gridPosition, false);
        // Add animation or state change logic here
    }
}
