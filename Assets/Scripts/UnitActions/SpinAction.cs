using System;
using System.Collections.Generic;
using UnityEngine;



/// <summary>
///     This class is responsible for spinning a unit around its Y-axis.
/// </summary>
/// remarks>
///     Change to turn towards the direction the mouse is pointing
/// </remarks>

public class SpinAction : BaseAction
{

   // public delegate void SpinCompleteDelegate();

  //  private Action onSpinComplete;
    private float totalSpinAmount = 0f;
    private void Update()
    {
        if(!isActive) return;

        float spinAddAmmount = 360f * Time.deltaTime;
        transform.eulerAngles += new Vector3(0, spinAddAmmount, 0);

        totalSpinAmount += spinAddAmmount;
        if (totalSpinAmount >= 360f)
        {
            isActive = false;
            totalSpinAmount = 0f;
            onActionComplete();
        }
       
    }
    public override void TakeAction(GridPosition gridPosition , Action onActionComplete)
    {
        this.onActionComplete = onActionComplete;
        isActive = true;
        totalSpinAmount = 0f;
    }

    public override string GetActionName()
    {
        return "Spin";
    }

    public override List<GridPosition> GetValidGridPositionList()
    {
      
        GridPosition unitGridPosition = unit.GetGridPosition();

        return new List<GridPosition>()
        {
            unitGridPosition
        };
    }

    public override int GetActionPointsCost()
    {
        return 2;
    }
}
