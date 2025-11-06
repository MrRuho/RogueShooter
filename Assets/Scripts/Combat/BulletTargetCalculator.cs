using UnityEngine;

public static class BulletTargetCalculator
{
    [System.Serializable]
    public class HitZoneConfig
    {
        [Header("Critical Hit Zones (Head/Heart)")]
        public float critHeightMin = 2.2f;
        public float critHeightMax = 2.8f;
        public float critHorizontalSpread = 0.15f;

        [Header("Normal Hit Zone (Torso)")]
        public float hitHeightMin = 1.5f;
        public float hitHeightMax = 2.2f;
        public float hitHorizontalSpread = 0.25f;

        [Header("Graze Zones (Limbs)")]
        public float grazeHeightMin = 0.8f;
        public float grazeHeightMax = 2.5f;
        public float grazeHorizontalSpread = 0.4f;

        [Header("Close Call (Near Miss)")]
        public float closeHorizontalOffset = 0.5f;
        public float closeVerticalSpread = 0.3f;

        [Header("Critical Miss (Wild Shot)")]
        public float critMissHorizontalOffset = 1.5f;
        public float critMissVerticalSpread = 1.0f;
    }

    private static HitZoneConfig _config;
    public static HitZoneConfig Config
    {
        get
        {
            if (_config == null)
            {
                _config = new HitZoneConfig();
            }
            return _config;
        }
        set => _config = value;
    }

    public static Vector3 CalculateBulletTarget(Unit targetUnit, ShotTier tier, Unit shootingUnit = null)
    {
        if (targetUnit == null) return Vector3.zero;

        Vector3 basePosition = targetUnit.GetWorldPosition();
        var cfg = Config;

        switch (tier)
        {
            case ShotTier.Crit:
                return CalculateCriticalHit(basePosition, cfg);

            case ShotTier.Hit:
                return CalculateNormalHit(basePosition, cfg);

            case ShotTier.Graze:
                return CalculateGraze(basePosition, cfg);

            case ShotTier.Close:
                return CalculateCloseMiss(basePosition, shootingUnit, cfg);

            case ShotTier.CritMiss:
                return CalculateCriticalMiss(basePosition, shootingUnit, cfg);

            default:
                return basePosition + Vector3.up * 1.5f;
        }
    }

    private static Vector3 CalculateCriticalHit(Vector3 basePos, HitZoneConfig cfg)
    {
        float height = Random.Range(cfg.critHeightMin, cfg.critHeightMax);
        float xOffset = Random.Range(-cfg.critHorizontalSpread, cfg.critHorizontalSpread);
        float zOffset = Random.Range(-cfg.critHorizontalSpread, cfg.critHorizontalSpread);

        return basePos + new Vector3(xOffset, height, zOffset);
    }

    private static Vector3 CalculateNormalHit(Vector3 basePos, HitZoneConfig cfg)
    {
        float height = Random.Range(cfg.hitHeightMin, cfg.hitHeightMax);
        float xOffset = Random.Range(-cfg.hitHorizontalSpread, cfg.hitHorizontalSpread);
        float zOffset = Random.Range(-cfg.hitHorizontalSpread, cfg.hitHorizontalSpread);

        return basePos + new Vector3(xOffset, height, zOffset);
    }

    private static Vector3 CalculateGraze(Vector3 basePos, HitZoneConfig cfg)
    {
        bool hitLimb = Random.value > 0.5f;
        
        float height;
        if (hitLimb)
        {
            height = Random.value > 0.5f 
                ? Random.Range(cfg.grazeHeightMin, cfg.hitHeightMin)
                : Random.Range(cfg.hitHeightMax, cfg.grazeHeightMax);
        }
        else
        {
            height = Random.Range(cfg.grazeHeightMin, cfg.grazeHeightMax);
        }

        float xOffset = Random.Range(-cfg.grazeHorizontalSpread, cfg.grazeHorizontalSpread);
        float zOffset = Random.Range(-cfg.grazeHorizontalSpread, cfg.grazeHorizontalSpread);

        return basePos + new Vector3(xOffset, height, zOffset);
    }

    private static Vector3 CalculateCloseMiss(Vector3 basePos, Unit shootingUnit, HitZoneConfig cfg)
    {
        Vector3 direction = Vector3.right;
        
        if (shootingUnit != null)
        {
            Vector3 toTarget = basePos - shootingUnit.GetWorldPosition();
            toTarget.y = 0;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Vector3 perpendicular = Vector3.Cross(toTarget.normalized, Vector3.up);
                direction = Random.value > 0.5f ? perpendicular : -perpendicular;
            }
        }
        else
        {
            direction = Random.insideUnitCircle.normalized;
            direction = new Vector3(direction.x, 0, direction.y);
        }

        float offset = Random.Range(cfg.closeHorizontalOffset * 0.7f, cfg.closeHorizontalOffset);
        float height = Random.Range(cfg.hitHeightMin, cfg.hitHeightMax) + 
                      Random.Range(-cfg.closeVerticalSpread, cfg.closeVerticalSpread);

        return basePos + direction * offset + Vector3.up * height;
    }

    private static Vector3 CalculateCriticalMiss(Vector3 basePos, Unit shootingUnit, HitZoneConfig cfg)
    {
        Vector3 randomDir = Random.insideUnitCircle.normalized;
        Vector3 direction = new Vector3(randomDir.x, 0, randomDir.y);

        float offset = Random.Range(cfg.critMissHorizontalOffset * 0.8f, cfg.critMissHorizontalOffset * 1.2f);
        float height = Random.Range(0.5f, 3.0f) + Random.Range(-cfg.critMissVerticalSpread, cfg.critMissVerticalSpread);

        return basePos + direction * offset + Vector3.up * height;
    }
}
