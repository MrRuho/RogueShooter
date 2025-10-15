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

    // Euclidinen etäisyys ruutuina (vastaa aiempaa world-distancea, mutta gridissä)
    private static float TileDistance(GridPosition a, GridPosition b)
    {
        int dx = a.x - b.x;
        int dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // Uusi band-määritys ruuduilla
    public static RangeBand GetBandTiles(Unit attacker, Unit target, WeaponDefinition w)
    {
        var gpA = attacker.GetGridPosition();
        var gpT = target.GetGridPosition();

        // (Valinta: jos kerros eri, voit palauttaa Extreme heti)
        // if (gpA.floor != gpT.floor) return RangeBand.Extreme;

        if (Ranges && Ranges.useTiles)
        {
            float tiles = TileDistance(gpA, gpT);
            if (tiles <= Ranges.meleeMaxTiles)  return RangeBand.Melee;
            if (tiles <= Ranges.closeMaxTiles)  return RangeBand.Close;
            if (tiles <= Ranges.mediumMaxTiles) return RangeBand.Medium;
            if (tiles <= Ranges.longMaxTiles)   return RangeBand.Long;
            return RangeBand.Extreme;
        }

        // Fallback: world-yksiköt (takaperin yhteensopiva)
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

        // Jos ei CombatRanges.assetia → käytä asekohtaisia world-rajoja (nykyinen polku)
        if (distWU <= 1.2f)          return RangeBand.Melee;
        if (distWU <= w.closeMax)    return RangeBand.Close;
        if (distWU <= w.mediumMax)   return RangeBand.Medium;
        if (distWU <= w.longMax)     return RangeBand.Long;
        return RangeBand.Extreme;
    }

    // Päälogiikka: 1) osuma vai huti, 2) tarkempi tier
    public static ShotResult Resolve(Unit attacker, Unit target, WeaponDefinition w)
    {
        Vector3 a = attacker.GetWorldPosition();
        Vector3 t = target.GetWorldPosition();
        float dist = Vector3.Distance(a, t);
       // var band = GetBand(dist, w);
       var band = GetBandTiles(attacker, target, w);

        // Skill + cover -muokkaukset vaikuttavat vain "vaihe 1: baseHitChance" -arvoon
        int baseHit = GetBaseHitChance(band, w);
        baseHit += GetSkillBonus(attacker);
        baseHit -= GetCoverPenalty(attacker, target);

        baseHit = Mathf.Clamp(baseHit, 0, 100);

        int roll1 = UnityEngine.Random.Range(1, 101);
        bool isHitPool = roll1 <= baseHit;

        ShotTier tier = isHitPool
            ? RollOnHit(band, w)
            : RollOnMiss(band, w);

        var res = new ShotResult { tier = tier };
        ApplyDamageModel(ref res, w);

        DebugShot(attacker, target, w, band, baseHit, roll1, res);
        return res;
    }

    private static int GetBaseHitChance(RangeBand b, WeaponDefinition w)
    {
        if (w.useAdvancedAccuracy)
            return w.GetTuning(b).baseHitChance;

        // Legacy polku (taaksepäin-yhteensopiva)
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

    private static int GetCoverPenalty(Unit attacker, Unit target)
    {
        var targetGridPosition = target.GetGridPosition();
        var node = PathFinding.Instance.GetNode(targetGridPosition.x, targetGridPosition.z, targetGridPosition.floor);
        var ct = CoverService.EvaluateCoverHalfPlane(attacker.GetGridPosition(), target.GetGridPosition(), node);

        if (attacker != null && attacker.archetype != null)
        {
            var archA = attacker.archetype;
            if (ct == CoverService.CoverType.High) return archA.highCoverPenalty;
            if (ct == CoverService.CoverType.Low)  return archA.lowCoverPenalty;
        }
        return 0;
    }

    private static ShotTier RollOnHit(RangeBand b, WeaponDefinition w)
    {
        var t = w.GetTuning(b);

        int c  = Mathf.Max(0, t.onHit_Close);
        int g  = Mathf.Max(0, t.onHit_Graze);
        int h  = Mathf.Max(0, t.onHit_Hit);
        int cr = Mathf.Max(0, t.onHit_Crit);

        int sum = c + g + h + cr;
        if (sum <= 0) return ShotTier.Hit; // fallback

        int r = UnityEngine.Random.Range(1, sum + 1);
        if (r <= c) return ShotTier.Miss;      // "Close" = cover chip → käytetään ShotTier.Miss pipelinea
        r -= c;
        if (r <= g) return ShotTier.Graze;
        r -= g;
        if (r <= h) return ShotTier.Hit;
        return ShotTier.Crit;
    }

    private static ShotTier RollOnMiss(RangeBand b, WeaponDefinition w)
    {
        var t = w.GetTuning(b);

        int m  = Mathf.Max(0, t.onMiss_Miss);
        int cm = Mathf.Max(0, t.onMiss_CritMiss);
        int sum = m + cm;
        if (sum <= 0) return ShotTier.CritMiss; // fallback: rankka huti

        int r = UnityEngine.Random.Range(1, sum + 1);
        return (r <= m) ? ShotTier.Miss : ShotTier.CritMiss;
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

            case ShotTier.Miss: // "Close call" → chip cover only
                res.damage = Mathf.RoundToInt(w.baseDamage * w.missChipFactor);
                res.coverOnly = true;
                res.bypassCover = false;
                break;

            case ShotTier.Graze:
                res.damage = Mathf.RoundToInt(w.baseDamage * w.grazeFactor);
                res.coverOnly = true;
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
            result.tier == ShotTier.Graze ? "yellow" : "red";

        string txt =
            $"<b>{attacker.name}</b> → <b>{target.name}</b>\n" +
            $"Weapon: {w.name}\n" +
            $"Range: {band} | Roll1: {roll1} vs Hit%:{baseHit}\n" +
            $"Result: <color={tierColor}>{result.tier}</color> | Dmg:{result.damage} | " +
            $"{(result.bypassCover ? "Bypass Cover" : result.coverOnly ? "Cover Only" : "Normal")}";

        Debug.Log(txt);
        // Halutessa voi näyttää world-labelin editorissa (Handles), jätetty pois runtime-käytön vuoksi.
    }
}
