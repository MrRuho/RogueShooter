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
    public int lowCoverPenalty  = 12;      // -12% osumatodennäköisyys
    public int highCoverPenalty = 25;      // -25%


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
