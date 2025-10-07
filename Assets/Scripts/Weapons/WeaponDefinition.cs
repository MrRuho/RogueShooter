using UnityEngine;

public enum RangeBand { Melee, Close, Medium, Long, Extreme }
public enum ShotTier  { CritMiss, Miss, Graze, Hit, Crit }

[CreateAssetMenu(menuName="RogueShooter/Weapon")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Base damage")]
    public int baseDamage = 10;
    public int critBonusDamage = 8;
    public float grazeFactor = 0.4f;   // 40% damagesta
    public float missChipFactor = 0.2f; // 20% damagesta (vain coveriin)

    [Header("Optimal ranges (world units)")]
    public float closeMax = 4f;
    public float mediumMax = 9f;
    public float longMax = 15f;

    [Header("Hit chance baseline by band (% before skill/cover)")]
    public int meleeAcc   = 95;
    public int closeAcc   = 80;
    public int mediumAcc  = 65;
    public int longAcc    = 45;
    public int extremeAcc = 25;

    [Header("Crit thresholds by band (bonus tunning)")]
    public int critStartMelee   = 90;
    public int critStartClose   = 85;
    public int critStartMedium  = 80;
    public int critStartLong    = 70;
    public int critStartExtreme = 60;
}
