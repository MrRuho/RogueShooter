using UnityEngine;

public enum GrenadeType { Frag, Flash, Smoke }

[CreateAssetMenu(menuName = "RogueShooter/Grenade Definition")]
public class GrenadeDefinition : ScriptableObject
{
    [Header("General")]
    public GrenadeType grenadeType = GrenadeType.Frag;
    public string grenadeName = "Fragmentation Grenade";
    
    [Header("Projectile Prefab")]
    [Tooltip("Prefabi joka sisältää BaseGrenadeProjectile-tyyppisen komponentin")]
    public GameObject projectilePrefab;
    
    [Header("Visual Effects")]
    public Transform explosionVFXPrefab;
    
    [Header("Audio")]
    public AudioClip[] explosionSounds;
    [Range(0f, 1f)] public float explosionVolume = 1f;
    public float explosionMaxHearingDistance = 80f;
    public AnimationCurve explosionVolumeRolloff = AnimationCurve.Linear(0, 1, 1, 0.2f);
    
    [Header("Timer")]
    [Tooltip("Montako actionia/käännöstä ennen räjähdystä")]
    public int timer = 2;

    [Tooltip("Jos true, käyttää action-pohjaista ajastinta, muuten turn-pohjaista")]
    public bool actionBasedTimer = true;
    
    [Tooltip("Räjähtää heti heittämisen jälkeen. Ohittaa muut timerit")]
    public bool InstantTimer = true;
    
    [Header("Explosion Timing")]
    [Tooltip("Pieni hajonta räjähdyksen ajankohtaan")]
    public float explosionJitterMin = 0.02f;
    public float explosionJitterMax = 0.08f;
    

    [Header("Frag-Specific Settings")]
    [Tooltip("Vahinko räjähdyskeskipisteessä")]
    public int baseDamage = 100;
    
    [Tooltip("Vaikuttaa vain yksikön suojaustaitoon ja kevyisiin esteisiin")]
    public float pressureFactor = 0.2f;
    
    [Tooltip("Vahinkoalue world-yksiköissä")]
    public float damageRadius = 4f;
}

