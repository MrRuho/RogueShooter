using UnityEngine;

public class LightFlash : MonoBehaviour
{
    [Header("Flash Settings")]
    [Tooltip("Kuinka kauan valo on täydellä kirkkaudella (sekunteina)")]
    [SerializeField] private float fullBrightnessTime = 0.05f;
    
    [Tooltip("Kuinka kauan valo himmenee (sekunteina)")]
    [SerializeField] private float fadeOutTime = 0.1f;
    
    [Tooltip("Käytä käyrää himmenemiseen (jos tyhjä, käytetään lineaarista)")]
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    private Light lightComponent;
    private float initialIntensity;
    private float elapsedTime;
    private bool isFlashing;
    
    void Awake()
    {
        lightComponent = GetComponent<Light>();
        if (lightComponent != null)
        {
            initialIntensity = lightComponent.intensity;
        }
    }
    
    void OnEnable()
    {
        if (lightComponent != null)
        {
            lightComponent.intensity = initialIntensity;
            elapsedTime = 0f;
            isFlashing = true;
        }
    }
    
    void Update()
    {
        if (!isFlashing || lightComponent == null) return;
        
        elapsedTime += Time.deltaTime;
        
        if (elapsedTime < fullBrightnessTime)
        {
            lightComponent.intensity = initialIntensity;
        }
        else
        {
            float fadeTime = elapsedTime - fullBrightnessTime;
            
            if (fadeTime >= fadeOutTime)
            {
                lightComponent.intensity = 0f;
                lightComponent.enabled = false;
                isFlashing = false;
            }
            else
            {
                float t = fadeTime / fadeOutTime;
                float curveValue = fadeOutCurve != null ? fadeOutCurve.Evaluate(t) : (1f - t);
                lightComponent.intensity = initialIntensity * curveValue;
            }
        }
    }
}

