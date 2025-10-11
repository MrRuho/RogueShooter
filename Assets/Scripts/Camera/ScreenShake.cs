using Unity.Cinemachine;
using UnityEngine;


public class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance { get; private set; }

    [SerializeField]
    private CinemachineImpulseSource cinemachineRecoilImpulseSource;

    [SerializeField]
    private CinemachineImpulseSource cinemachineExplosiveImpulseSource;

    private void Awake()
    {

        // Ensure that there is only one instance in the scene
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
        cinemachineRecoilImpulseSource.GenerateImpulse(ShakeStrength);
    }
}

