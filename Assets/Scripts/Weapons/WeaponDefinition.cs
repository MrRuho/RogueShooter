using UnityEngine;

public enum RangeBand { Melee, Close, Medium, Long, Extreme }
public enum ShotTier  { CritMiss, Miss, Graze, Hit, Crit }


[System.Serializable]
public struct RangeBandTuning
{
    [Header("Stage 1: Base chance to HIT (before skill/cover)")]
    [Range(0, 100)] public int baseHitChance;

    [Header("Stage 2a: On HIT distribution (Close/Graze/Hit/Crit)")]
    [Range(0, 100)] public int onHit_Close;
    [Range(0, 100)] public int onHit_Graze;
    [Range(0, 100)] public int onHit_Hit;
    [Range(0, 100)] public int onHit_Crit;

    [Header("Stage 2b: On MISS distribution (Miss/CritMiss)")]
    [Range(0, 100)] public int onMiss_Miss;
    [Range(0, 100)] public int onMiss_CritMiss;
}


[CreateAssetMenu(menuName = "RogueShooter/Weapon")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Range: Weapon basic max Range (No upgrades)")]
    public int maxShootRange = 10;

    [Header("Bonus when the target is not behind cover")]
    public int NoCoverDamageBonus = 30;
    
    [Header("Base damage")]
    public int baseDamage = 10;
    public int critBonusDamage = 8;
    public float grazeFactor = 0.4f;   // 40% damagesta
    public float missChipFactor = 0.2f; // 20% damagesta (vain coveriin)
    
    [Header("Legacy per-weapon ranges (used if no global CombatRanges found)")]
    public float closeMax = 4f;
    public float mediumMax = 9f;
    public float longMax = 15f;

    [Header("Advanced accuracy: per-band tunables")]
    public bool useAdvancedAccuracy = true;
    public RangeBandTuning melee;
    public RangeBandTuning close;
    public RangeBandTuning medium;
    public RangeBandTuning @long;
    public RangeBandTuning extreme;

    public RangeBandTuning GetTuning(RangeBand b)
    {
        switch (b)
        {
            case RangeBand.Melee:  return melee;
            case RangeBand.Close:  return close;
            case RangeBand.Medium: return medium;
            case RangeBand.Long:   return @long;
            default:               return extreme;
        }
    }

    // (TAKAISIN-YHTEENSOPIVA): jos haluat käyttää vanhaa baselinea joskus, jätä nämä.
    [Header("Legacy baselines (ignored if useAdvancedAccuracy==true)")]
    public int meleeAcc   = 95;
    public int closeAcc   = 80;
    public int mediumAcc  = 65;
    public int longAcc    = 45;
    public int extremeAcc = 25;

    [Header("Legacy crit starts (ignored if useAdvancedAccuracy==true)")]
    public int critStartMelee   = 90;
    public int critStartClose   = 85;
    public int critStartMedium  = 80;
    public int critStartLong    = 70;
    public int critStartExtreme = 60;
}
