using UnityEngine;

public enum EffectArea { Melee, Close, Medium }
public enum ThrowTier { CritMiss, Miss, Hit, Bullseye }
public enum GrenadeType  {Frag, flash, Smoke}

[CreateAssetMenu(menuName = "RogueShooter/Grenade")]
public class GrenadeDefinition : ScriptableObject
{
    [Header("Base damage")]
    public int baseDamage = 300;
    public float pressureFactor = 0.2f; // Effects only Unit cover skill and light covers and objects.

    [Header("Base hit chance baseline by band (% before skill) Overall must be 100%")]
    public int critMiss = 10;
    public int miss = 20;
    public int hit = 60;
    public int Bullseye = 10;

    [Header("Timer(turns before explotion)")]
    public int timer = 1;
}
