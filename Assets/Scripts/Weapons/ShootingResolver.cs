using UnityEngine;

public struct ShotResult {
    public ShotTier tier;
    public int damage;          // paljonko “vahinkoa” tämä laukaus tuottaa
    public bool bypassCover;    // true = suoraan Healthiin (Crit)
    public bool coverOnly;      // true = vain cover-pooliin (Miss/Graze)
}

public static class ShootingResolver
{
    public static RangeBand GetBand(float dist, WeaponDefinition w)
    {
        if (dist <= 1.2f) return RangeBand.Melee;
        if (dist <= w.closeMax) return RangeBand.Close;
        if (dist <= w.mediumMax) return RangeBand.Medium;
        if (dist <= w.longMax) return RangeBand.Long;
        return RangeBand.Extreme;
    }

    public static int BaseAcc(RangeBand b, WeaponDefinition w) => b switch
    {
        RangeBand.Melee => w.meleeAcc,
        RangeBand.Close => w.closeAcc,
        RangeBand.Medium => w.mediumAcc,
        RangeBand.Long => w.longAcc,
        _ => w.extremeAcc
    };

    public static int CritStart(RangeBand b, WeaponDefinition w) => b switch
    {
        RangeBand.Melee => w.critStartMelee,
        RangeBand.Close => w.critStartClose,
        RangeBand.Medium => w.critStartMedium,
        RangeBand.Long => w.critStartLong,
        _ => w.critStartExtreme
    };

    // Palauttaa myös käytetyn cover-penaltin (UI:lle, debugiin).
    public static ShotResult Resolve(Unit attacker, Unit target, WeaponDefinition w)
    {
        // etäisyys & band
        Vector3 a = attacker.GetWorldPosition();
        Vector3 t = target.GetWorldPosition();
        float dist = Vector3.Distance(a, t);
        var band = GetBand(dist, w);

        // lähtötarkkuus
        int acc = BaseAcc(band, w);

        // skillibonus
        // var arch = attacker ? attacker.GetComponent<Unit>()?.GetComponent<Unit>() : null; // ei tarvita, käytä suoraan:
        // var atArch = attacker != null ? attacker.archetype : null;// jos säilytät viitteen julkisesti, käytä attacker.archetype
        // int skill = attacker.GetComponent<Unit>().isServer ? 0 : 0; // älä näin – käytä suoraan attacker.archetype
        //  var atkArch = attacker.GetComponent<Unit>().GetComponent<UnitArchetype>(); // jos ei ole helposti käsillä, lisää Unitille getter archetypeen

        int skillBonus = (attacker as Unit)?.archetype != null
            ? (attacker as Unit).archetype.shootingSkill * (attacker as Unit).archetype.accPerSkill
            : 0;
        acc += skillBonus;

        // cover-penalty suunnasta
        var targetGridPosition = target.GetGridPosition();
        var node = PathFinding.Instance.GetNode(targetGridPosition.x, targetGridPosition.z, targetGridPosition.floor);
        var ct = CoverService.EvaluateCoverHalfPlane(attacker.GetGridPosition(), target.GetGridPosition(), node);
        int coverPenalty = 0;
        if ((attacker as Unit)?.archetype != null)
        {
            var archA = (attacker as Unit).archetype;
            coverPenalty = ct == CoverService.CoverType.High ? archA.highCoverPenalty :
                           ct == CoverService.CoverType.Low ? archA.lowCoverPenalty : 0;
        }
        acc -= coverPenalty;

        // rajaa 0..100 ja heitto
        acc = Mathf.Clamp(acc, 0, 100);
        int roll = UnityEngine.Random.Range(1, 101);

        // määritä tier kynnysten mukaan
        int critStart = CritStart(band, w);        // esim. 80–90
        int hitStart = Mathf.Max(35, acc - 15);   // pehmeä siirtymä: mitä parempi acc, sitä alempaa alkaa “Hit”
        int grazeStart = Mathf.Max(15, acc / 2);    // pienikin acc antaa mahdollisuuden grazeen

        ShotTier tier;
        if (roll > Mathf.Max(critStart, acc + 5)) tier = ShotTier.Crit;        // pieni “over-roll” mahdollistaa critin
        else if (roll > hitStart) tier = ShotTier.Hit;
        else if (roll > grazeStart) tier = ShotTier.Graze;
        else if (roll > 10) tier = ShotTier.Miss;
        else tier = ShotTier.CritMiss;

        // rakenna tulos
        var res = new ShotResult { tier = tier };

        switch (tier)
        {
            case ShotTier.CritMiss:
                res.damage = 0;
                res.coverOnly = false;   // ei mitään vaikutusta
                res.bypassCover = false;
                break;

            case ShotTier.Miss:
                res.damage = Mathf.RoundToInt(w.baseDamage * w.missChipFactor);
                res.coverOnly = true;    // vaikuttaa vain cover-pooliin
                res.bypassCover = false;
                break;

            case ShotTier.Graze:
                res.damage = Mathf.RoundToInt(w.baseDamage * w.grazeFactor);
                res.coverOnly = true;    // vain cover-pooliin
                res.bypassCover = false;
                break;

            case ShotTier.Hit:
                res.damage = w.baseDamage;
                res.coverOnly = false;   // normaali pipeline (ensin cover-mitigation, sitten personal cover, ylijäämä healthiin)
                res.bypassCover = false;
                break;

            case ShotTier.Crit:
                res.damage = w.baseDamage + w.critBonusDamage;
                res.coverOnly = false;
                res.bypassCover = true;  // ohita cover completely (suoraan healthiin)
                break;
        }

//#if UNITY_EDITOR
            DebugShot(attacker, target, w, band, acc, roll, res);
//#endif

        return res;
    }
    
//#if UNITY_EDITOR
    private static void DebugShot(Unit attacker, Unit target, WeaponDefinition w, RangeBand band, int acc, int roll, ShotResult result)
    {
        string txt =
            $"<b>{attacker.name}</b> → <b>{target.name}</b>\n" +
            $"Weapon: {w.name}\n" +
            $"Range: {band} | Roll: {roll}\n" +
            $"Accuracy: {acc}% | Result: <color={(result.tier==ShotTier.Crit ? "lime" : result.tier==ShotTier.Hit ? "cyan" : result.tier==ShotTier.Graze ? "yellow" : "red")}>{result.tier}</color>\n" +
            $"Damage: {result.damage} | " +
            $"{(result.bypassCover ? "Bypass Cover" : result.coverOnly ? "Cover Only" : "Normal")}";

        // Tulostaa konsoliin
        Debug.Log(txt);

        // Näyttää tekstin maailmassa (Scene/Game näkymässä)
        Vector3 pos = target.transform.position + Vector3.up * 2.0f;
        //UnityEditor.Handles.Label(pos, txt.Replace("<b>", "").Replace("</b>", ""));
    }
//#endif
}
