using System;
using UnityEngine;

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
    public void Spin(Action onActionComplete)
    {
        this.onActionComplete = onActionComplete;
        isActive = true;
        totalSpinAmount = 0f;
    }

    public override string GetActionName()
    {
        return "Spin";
    }
}
