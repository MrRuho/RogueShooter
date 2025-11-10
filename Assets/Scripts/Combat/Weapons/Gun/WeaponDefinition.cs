using UnityEngine;

public enum RangeBand { Melee, Close, Medium, Long, Extreme }
public enum ShotTier  { CritMiss, Close, Graze, Hit, Crit }

[System.Serializable]
public struct RangeBandTuning
{
    [Header("Stage 1: Base chance to HIT (before skill/cover)")]
    [Range(0, 100)] public int baseHitChance;

    [Header("Stage 2: On HIT distribution (Close/Graze/Hit/Crit)")]
    [Range(0, 100)] public int onHit_Close;
    [Range(0, 100)] public int onHit_Graze;
    [Range(0, 100)] public int onHit_Hit;
    [Range(0, 100)] public int onHit_Crit;
}

[System.Serializable]
public struct NormalShootingSettings
{
    [Header("Shooting Tempo")]
    [Tooltip("Kääntymisen nopeus normaalissa ampumisessa (deg/s)")]
    [Range(10f, 600f)] public float turnSpeed;

    [Tooltip("Minimiaika tähtäämiseen")]
    [Range(0.05f, 1.0f)] public float minAimTime;

    [Tooltip("Kokonaisaika tähtäystilassa")]
    [Range(0.1f, 2.0f)] public float aimingStateTime;

    [Tooltip("Aika ampumistilan jälkeen")]
    [Range(0.1f, 2.0f)] public float cooloffStateTime;

    public static NormalShootingSettings Default => new NormalShootingSettings
    {
        turnSpeed = 45f,
        minAimTime = 0.40f,
        aimingStateTime = 1.00f,
        cooloffStateTime = 0.50f
    };
}

[System.Serializable]
public struct OverwatchShootingSettings
{
    [Header("Geometry")]
    [Tooltip("Overwatch-kartion kulma asteina")]
    [Range(30f, 180f)] public float coneAngleDeg;

    [Tooltip("Overwatchin kantama ruutuina")]
    [Range(1, 15)] public int rangeTiles;

    [Header("Shooting Tempo")]
    [Tooltip("Kääntymisen nopeus overwatch-laukauksissa (deg/s)")]
    [Range(10f, 600f)] public float turnSpeed;

    [Tooltip("Minimiaika tähtäämiseen")]
    [Range(0.05f, 0.5f)] public float minAimTime;

    [Tooltip("Kokonaisaika tähtäystilassa")]
    [Range(0.1f, 1.0f)] public float aimingStateTime;

    [Tooltip("Aika ampumistilan jälkeen")]
    [Range(0.1f, 1.0f)] public float cooloffStateTime;

    [Header("Reaction Timing")]
    [Tooltip("Satunnainen viive reaktioon (maksimi)")]
    [Range(0f, 0.25f)] public float reactionJitterMaxSeconds;

    [Tooltip("Minimi-aikaväli kahden overwatch-reaktion välillä")]
    [Range(0.1f, 2.0f)] public float reactionCooldownSeconds;

    [Tooltip("Overwatch shooting accuracy penalty")]
    [Range(0f, 100f)] public float overwatchShootPenalty;

    public static OverwatchShootingSettings Default => new OverwatchShootingSettings
    {
        coneAngleDeg = 80f,
        rangeTiles = 8,
        turnSpeed = 45f,
        minAimTime = 0.15f,
        aimingStateTime = 0.5f,
        cooloffStateTime = 0.3f,
        reactionJitterMaxSeconds = 0.10f,
        reactionCooldownSeconds = 0.5f,
        overwatchShootPenalty = 10f
    };
}

[CreateAssetMenu(menuName = "RogueShooter/Weapon")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Normal Shooting")]
    [Tooltip("Normaali ampuminen (deg/s + vaiheet)")]
    public NormalShootingSettings normalShooting = NormalShootingSettings.Default;

    [Header("Overwatch Mode")]
    public OverwatchShootingSettings overwatch = OverwatchShootingSettings.Default;

    [Header("Range: Weapon basic max Range (No upgrades)")]
    public int maxShootRange = 10;

    [Header("Bonus when the target is not behind cover")]
    public int NoCoverDamageBonus = 30;

    [Header("Base damage")]
    public int baseDamage = 10;
    public int critBonusDamage = 8;
    public float grazeFactor = 0.4f;
    public float missChipFactor = 0.2f;

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

    [Header("Burst Fire Settings")]
    [Tooltip("Minimum shots per burst (e.g., 3 for assault rifles)")]
    public int burstMin = 3;
    [Tooltip("Maximum shots per burst (e.g., 4 for assault rifles)")]
    public int burstMax = 4;
    [Tooltip("Time between shots in a burst (seconds)")]
    public float burstShotDelay = 0.1f;

    public int GetRandomBurstCount() => Random.Range(burstMin, burstMax + 1);

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (normalShooting.turnSpeed < 5f)
            normalShooting.turnSpeed = 45f;

        if (overwatch.turnSpeed < 5f)
            overwatch.turnSpeed = Mathf.Max(normalShooting.turnSpeed, 20f);

        if (normalShooting.minAimTime <= 0f)       normalShooting.minAimTime       = 0.40f;
        if (normalShooting.aimingStateTime <= 0f)  normalShooting.aimingStateTime  = 1.00f;
        if (normalShooting.cooloffStateTime <= 0f) normalShooting.cooloffStateTime = 0.50f;

        if (overwatch.minAimTime <= 0f)       overwatch.minAimTime       = 0.15f;
        if (overwatch.aimingStateTime <= 0f)  overwatch.aimingStateTime  = 0.50f;
        if (overwatch.cooloffStateTime <= 0f) overwatch.cooloffStateTime = 0.30f;
    }
#endif
}
