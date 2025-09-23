using System;
using UnityEngine;

public class ScreenShakeActions : MonoBehaviour
{
    private void Start()
    {
        ShootAction.OnAnyShoot += ShootAction_OnAnyShoot;
    }

    private void OnDisable()
    { 
        ShootAction.OnAnyShoot -= ShootAction_OnAnyShoot;
    }

    private void ShootAction_OnAnyShoot(object sender, ShootAction.OnShootEventArgs e)
    {
        // ScreenShake.Instance.Shake();
        ScreenShake.Instance.RecoilCameraShake();
    }

}
