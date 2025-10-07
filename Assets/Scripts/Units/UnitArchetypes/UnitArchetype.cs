using UnityEngine;

[CreateAssetMenu(menuName = "RogueShooter/UnitArchetype")]
public class UnitArchetype : ScriptableObject
{
    [Header("Base skills")]
    public int personalCoverMax = 200;
    public int coverRegenOnMove = 20;
    public int coverRegenPerUnusedAP = 50;

    public int shootingSkill = 0;          // 0..10
    public int accPerSkill = 3;            // +3% tarkkuutta / taso
    public int lowCoverPenalty  = 12;      // -12% osumatodennäköisyys
    public int highCoverPenalty = 25;      // -25%

    [Header("Progression (optional)")]
    public AnimationCurve coverMaxByLevel = AnimationCurve.Linear(1, 200, 10, 300);
    public AnimationCurve regenByLevel = AnimationCurve.Linear(1, 20, 10, 35);

}

