// Assets/Scripts/GameLogic/Camera/CameraThaw.cs
using UnityEngine;

public static class CameraThaw
{
    public static void Thaw(string reason = "")
    {
        Debug.Log($"[CameraThaw] Thaw camera. Reason={reason}");

        // 1) Poista mahdollinen action-kamera pelistä
        var cm = CameraManager.Instance;    // teillä jo oleva manageri, joka togglaa action-kameran
        if (cm != null) cm.HideActionCamera(); // no-op jos ei ollut päällä

        // 2) Varmista vapaa kamera päällä
        var cc = CameraController.Instance;
        if (cc != null) cc.enabled = true;

        // 3) Pakota input-map takaisin päälle
        var im = InputManager.Instance;
        if (im != null) im.ForceEnablePlayerMap();
    }
}
