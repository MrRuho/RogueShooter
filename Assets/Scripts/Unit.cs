using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    private Vector3 targetPosition;

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.T))
        {
           Move(new Vector3(4, 0, 4));
        }
        Vector3 moveDirection = (targetPosition - transform.position).normalized;
        float moveSpeed = 4f;
        transform.position += moveSpeed * Time.deltaTime * moveDirection;

        if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position += moveSpeed * Time.deltaTime * moveDirection;
        }
    }
    private void Move(Vector3 newTargetPosition)
    {
        // Move to destination
        targetPosition = newTargetPosition;
    }
}
