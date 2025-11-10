using UnityEngine;

[CreateAssetMenu(menuName = "RogueShooter/UnitArchetype")]
public class UnitArchetype : ScriptableObject
{

    [Header("BASE SKILLS")]
    [Space(10)]
    [Header("Covering Skill")]
    public int personalCoverMax = 200;
    public int coverRegenOnMove = 1;
    public int coverRegenPerUnusedAP = 25;

    [Tooltip("Kuinka paljon matala suoja vähentää vihollisen todennäköisyyttä osua. Huom! Suhteellinen vähennys. Esim 2% vähentää 100% taidon 98%, ja 50% taidon 49%")]
    public int LowCoverEnemyHitPenalty = 12;      // -12% vihollisen osumatodennäköisyyteen
    [Tooltip("Kuinka paljon korkea suoja vähentää vihollisen todennäköisyyttä osua. Huom! Suhteellinen vähennys. Esim 2% vähentää 100% taidon 98%, ja 50% taidon 49%")]
    public int highCoverEnemyHitPenalty = 25;      // -25% vihollinen osumatodennäköisyyteen

    [Space(10)]
    [Header("Moving Skill")]
    public int moveRange = 4;

     [Tooltip("Kuinka paljon liike vähentää vihollisen mahdollisuutta osua. Huom! Suhteellinen vähennys. Esim 2% vähentää 100% taidon 98%, ja 50% taidon 49%")]
    public int moveEnemyHitPenalty = 5; // Kuinka paljon vähentää osumatodennäköisyyttä kun on liikkeessä. 

    [Space(10)]
    [Header("Shooting Skill")]
    public int basicShootinSkill = 69;
    public int shootingSkillLevel = 0; // 0..10
    public int accuracyBonusPerSkillLevel = 3; // +3% tarkkuutta / taso


    [Space(10)]
    [Header("Grenade Skill")]
    public int grenadeCapacity = 2;
    public int throwingRange = 7;

    [Space(10)]
    [Header("Vision Skill")]
    public int visionRange = 20;
    public bool useHeightAware = true;
    

    [Header("Progression (optional)")]
    public AnimationCurve coverMaxByLevel = AnimationCurve.Linear(1, 200, 10, 300);
    public AnimationCurve regenByLevel = AnimationCurve.Linear(1, 20, 10, 35);

}
