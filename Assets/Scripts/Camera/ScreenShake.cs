/*
using Unity.Cinemachine;
using UnityEngine;


public class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance { get; private set; }

    
    private CinemachineImpulseSource cinemachineImpulseSource;

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

        cinemachineImpulseSource = GetComponent<CinemachineImpulseSource>();
    }

    public void Shake(float intensity = 1f)
    {
        cinemachineImpulseSource.GenerateImpulse(intensity);
    }
}
*/

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

        // cinemachineRecoilImpulseSource = GetComponent<CinemachineImpulseSource>();
    }

    public void ExplosiveCameraShake()
    {
        cinemachineExplosiveImpulseSource.GenerateImpulse();
    }

    public void RecoilCameraShake()
    { 
        cinemachineRecoilImpulseSource.GenerateImpulse();
    }
}

