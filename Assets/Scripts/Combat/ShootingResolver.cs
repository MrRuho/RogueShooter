using UnityEngine;

public struct ShotResult {
    public ShotTier tier;
    public int damage;
    public bool bypassCover;
    public bool coverOnly;
}

public static class ShootingResolver
{
    private static CombatRanges _cachedRanges;
    private static CombatRanges Ranges
    {
        get
        {
            if (!_cachedRanges)
                _cachedRanges = Resources.Load<CombatRanges>("CombatRanges");
            return _cachedRanges;
        }
    }

    private static float TileDistance(GridPosition a, GridPosition b) 
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dz = Mathf.Abs(a.z - b.z);
        int cost = SircleCalculator.Sircle(dx, dz);
        return cost / 10f;
    }

    public static RangeBand GetBandTiles(Unit attacker, Unit target, WeaponDefinition w)
    {
        var gpA = attacker.GetGridPosition();
        var gpT = target.GetGridPosition();

        if (Ranges && Ranges.useTiles)
        {
            float tiles = TileDistance(gpA, gpT);
            if (tiles <= Ranges.meleeMaxTiles)  return RangeBand.Melee;
            if (tiles <= Ranges.closeMaxTiles)  return RangeBand.Close;
            if (tiles <= Ranges.mediumMaxTiles) return RangeBand.Medium;
            if (tiles <= Ranges.longMaxTiles)   return RangeBand.Long;
            return RangeBand.Extreme;
        }

        Vector3 aw = attacker.GetWorldPosition();
        Vector3 tw = target.GetWorldPosition();
        float distWU = Vector3.Distance(aw, tw);
        if (Ranges)
        {
            if (distWU <= Ranges.meleeMaxWU)  return RangeBand.Melee;
            if (distWU <= Ranges.closeMaxWU)  return RangeBand.Close;
            if (distWU <= Ranges.mediumMaxWU) return RangeBand.Medium;
            if (distWU <= Ranges.longMaxWU)   return RangeBand.Long;
            return RangeBand.Extreme;
        }

        if (distWU <= 1.2f)          return RangeBand.Melee;
        if (distWU <= w.closeMax)    return RangeBand.Close;
        if (distWU <= w.mediumMax)   return RangeBand.Medium;
        if (distWU <= w.longMax)     return RangeBand.Long;
        return RangeBand.Extreme;
    }

    public static ShotResult Resolve(Unit attacker, Unit target, WeaponDefinition w)
    {
        Vector3 a = attacker.GetWorldPosition();
        Vector3 t = target.GetWorldPosition();
        float dist = Vector3.Distance(a, t);
        var band = GetBandTiles(attacker, target, w);

        int baseHit = GetBaseHitChance(band, w);
        baseHit += GetSkillBonus(attacker);
        baseHit -= GetHitPenalty(attacker, target);

        baseHit = Mathf.Clamp(baseHit, 0, 100);

        int roll1 = UnityEngine.Random.Range(1, 101);
        bool isHit = roll1 <= baseHit;

        ShotTier tier = isHit
            ? RollOnHit(band, w)
            : ShotTier.CritMiss;

        var res = new ShotResult { tier = tier };
        ApplyDamageModel(ref res, w);

       // DebugShot(attacker, target, w, band, baseHit, roll1, res);
        return res;
    }

    private static int GetBaseHitChance(RangeBand b, WeaponDefinition w)
    {
        if (w.useAdvancedAccuracy)
            return w.GetTuning(b).baseHitChance;

        switch (b)
        {
            case RangeBand.Melee:  return w.meleeAcc;
            case RangeBand.Close:  return w.closeAcc;
            case RangeBand.Medium: return w.mediumAcc;
            case RangeBand.Long:   return w.longAcc;
            default:               return w.extremeAcc;
        }
    }

    private static int GetSkillBonus(Unit attacker)
    {
        if (attacker != null && attacker.archetype != null)
            return attacker.archetype.shootingSkillLevel * attacker.archetype.accuracyBonusPerSkillLevel;
        return 0;
    }

    // Kohteen suojautumis ja liikkumistaito vähentää kohteeseen osumista.
    private static int GetHitPenalty(Unit attacker, Unit target)
    {
        if (target == null || target.archetype == null)
            return 0;

        int totalPenalty = 0;
        var targetArch = target.archetype;

        var targetGridPosition = target.GetGridPosition();
        var node = PathFinding.Instance.GetNode(targetGridPosition.x, targetGridPosition.z, targetGridPosition.floor);
        var ct = CoverService.EvaluateCoverHalfPlane(attacker.GetGridPosition(), target.GetGridPosition(), node);

        if (ct == CoverService.CoverType.High)
            totalPenalty += targetArch.highCoverEnemyHitPenalty;
        else if (ct == CoverService.CoverType.Low)
            totalPenalty += targetArch.LowCoverEnemyHitPenalty;

        var moveAction = target.GetAction<MoveAction>();
        if (moveAction != null && moveAction.IsActionActive())
        {
            totalPenalty += targetArch.moveEnemyHitPenalty;
        }

        // DoDo OverwachAction shooting penalty
        

        return totalPenalty;
    }

    private static ShotTier RollOnHit(RangeBand b, WeaponDefinition w)
    {
        var t = w.GetTuning(b);

        int close = Mathf.Max(0, t.onHit_Close);
        int graze = Mathf.Max(0, t.onHit_Graze);
        int hit   = Mathf.Max(0, t.onHit_Hit);
        int crit  = Mathf.Max(0, t.onHit_Crit);

        int sum = close + graze + hit + crit;
        if (sum <= 0) return ShotTier.Hit;

        int r = UnityEngine.Random.Range(1, sum + 1);
        if (r <= close) return ShotTier.Close;
        r -= close;
        if (r <= graze) return ShotTier.Graze;
        r -= graze;
        if (r <= hit) return ShotTier.Hit;
        return ShotTier.Crit;
    }

    private static void ApplyDamageModel(ref ShotResult res, WeaponDefinition w)
    {
        switch (res.tier)
        {
            case ShotTier.CritMiss:
                res.damage = 0;
                res.coverOnly = false;
                res.bypassCover = false;
                break;

            case ShotTier.Close:
                res.damage = Mathf.RoundToInt(w.baseDamage * w.missChipFactor);
                res.coverOnly = true;
                res.bypassCover = false;
                break;

            case ShotTier.Graze:
                res.damage = Mathf.RoundToInt(w.baseDamage * w.grazeFactor);
                res.coverOnly = false;
                res.bypassCover = false;
                break;

            case ShotTier.Hit:
                res.damage = w.baseDamage;
                res.coverOnly = false;
                res.bypassCover = false;
                break;

            case ShotTier.Crit:
                res.damage = w.baseDamage + w.critBonusDamage;
                res.coverOnly = false;
                res.bypassCover = true;
                break;
        }
    }

    private static void DebugShot(Unit attacker, Unit target, WeaponDefinition w, RangeBand band, int baseHit, int roll1, ShotResult result)
    {
        string tierColor =
            result.tier == ShotTier.Crit ? "Green" :
            result.tier == ShotTier.Hit ? "Blue" :
            result.tier == ShotTier.Graze ? "yellow" :
            result.tier == ShotTier.Close ? "orange" : "red";

        string txt =
            $"<b>{attacker.name}</b> → <b>{target.name}</b>\n" +
            $"Weapon: {w.name}\n" +
            $"Range: {band} | Roll1: {roll1} vs Hit%:{baseHit}\n" +
            $"Result: <color={tierColor}>{result.tier}</color> | Dmg:{result.damage} | " +
            $"{(result.bypassCover ? "Bypass Cover" : result.coverOnly ? "Cover Only" : "Normal")}";

        Debug.Log(txt);
    }
}
