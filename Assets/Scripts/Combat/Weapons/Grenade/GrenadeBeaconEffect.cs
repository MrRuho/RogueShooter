using UnityEngine;

[RequireComponent(typeof(Light))]
public class GrenadeBeaconEffect : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Color beaconColor = Color.red;
    [SerializeField] private float minIntensity = 0.5f;
    [SerializeField] private float maxIntensity = 3f;
    [SerializeField] private float minRange = 2f;
    [SerializeField] private float maxRange = 5f;
    [SerializeField] private float basePulseSpeed = 3f;

    [Header("Turn-Based Timing")]
    [SerializeField] private int turnsUntilExplosion = 2;
    [SerializeField] private float finalPulseSpeedMultiplier = 5f;

    [Header("Pulse Variation")]
    [Tooltip("Satunnainen vaihtelu pulssin aloituksessa (sekunteina)")]
    [SerializeField] private float pulseStartOffsetRange = 0.5f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip beepSound;
    [SerializeField] private float basePitch = 1f;
    [SerializeField] private float finalPitchMultiplier = 1.5f;

    private Light beaconLight;
    private int currentTurnsRemaining;
    private bool isArmed = false;
    private float currentPulseSpeed;
    private float currentPitch;
    private float pulseTimeOffset;

    void Awake()
    {
        beaconLight = GetComponent<Light>();

        if (beaconLight == null)
        {
            beaconLight = gameObject.AddComponent<Light>();
        }

        beaconLight.type = LightType.Point;
        beaconLight.color = beaconColor;
        beaconLight.intensity = minIntensity;
        beaconLight.range = minRange;
        beaconLight.renderMode = LightRenderMode.ForcePixel;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 3f;
                audioSource.maxDistance = 20f;
            }
        }

        currentTurnsRemaining = turnsUntilExplosion;
        currentPulseSpeed = basePulseSpeed;
        currentPitch = basePitch;
        
        pulseTimeOffset = Random.Range(0f, pulseStartOffsetRange);
    }

    void Update()
    {
        UpdateLightPulse();
    }

    public void OnTurnAdvanced()
    {
        if (currentTurnsRemaining > 0)
        {
            currentTurnsRemaining--;
            UpdatePulseParameters();
            PlayBeep();
        }

        if (currentTurnsRemaining == 0)
        {
            isArmed = true;
        }
    }
    
    public void TriggerFinalCountdown()
    {
        isArmed = true;
        currentTurnsRemaining = 0;
        currentPulseSpeed = basePulseSpeed * finalPulseSpeedMultiplier;
        currentPitch = basePitch * finalPitchMultiplier;
    }
    
    private void UpdatePulseParameters()
    {
        if (turnsUntilExplosion <= 0) return;

        float progress = 1f - ((float)currentTurnsRemaining / turnsUntilExplosion);

        currentPulseSpeed = Mathf.Lerp(basePulseSpeed, basePulseSpeed * finalPulseSpeedMultiplier, progress);
        currentPitch = Mathf.Lerp(basePitch, basePitch * finalPitchMultiplier, progress);
    }
    
    private void UpdateLightPulse()
    {
        float pulseValue = (Mathf.Sin((Time.time + pulseTimeOffset) * currentPulseSpeed) + 1f) * 0.5f;

        beaconLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, pulseValue);
        beaconLight.range = Mathf.Lerp(minRange, maxRange, pulseValue);
    }
    
    private void PlayBeep()
    {
        if (audioSource != null && beepSound != null)
        {
            audioSource.pitch = currentPitch;
            audioSource.PlayOneShot(beepSound);
        }
    }
    
    public void SetTurnsUntilExplosion(int turns)
    {
        turnsUntilExplosion = turns;
        currentTurnsRemaining = turns;
        UpdatePulseParameters();
    }

    public bool IsArmed => isArmed;
    public int TurnsRemaining => currentTurnsRemaining;

    public void SetRemainingDirect(int remaining)
    {
        currentTurnsRemaining = Mathf.Max(remaining, 0);
        UpdatePulseParameters();
    }

    public void PlayBeepOnce()
    {
        PlayBeep();
    }
}
