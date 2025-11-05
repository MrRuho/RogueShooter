using Unity.Cinemachine;
using UnityEngine;

public class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance { get; private set; }

    [SerializeField]
    private CinemachineImpulseSource cinemachineRecoilImpulseSource;

    [SerializeField]
    private CinemachineImpulseSource cinemachineExplosiveImpulseSource;

    [Header("Recoil Shake Settings")]
    [Tooltip("Minimum time between recoil shakes to prevent stacking")]
    [SerializeField]
    private float recoilCooldown = 0.1f;

    private float lastRecoilTime = -999f;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("ScreenShake: More than one ScreenShake in the scene!" + transform + " " + Instance);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void ExplosiveCameraShake(float ShakeStrength)
    {
        cinemachineExplosiveImpulseSource.GenerateImpulse(ShakeStrength);
    }

    public void RecoilCameraShake(float ShakeStrength)
    {
        if (Time.time - lastRecoilTime < recoilCooldown)
        {
            return;
        }

        lastRecoilTime = Time.time;
        cinemachineRecoilImpulseSource.GenerateImpulse(ShakeStrength);
    }
}
