using UnityEngine;

public class SpinAction : BaseAction
{

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
            Debug.Log("Spin complete!");
        }
       
    }
    public void Spin()
    {
        isActive = true;
        totalSpinAmount = 0f;
        Debug.Log("Spinning!");
    }
}
