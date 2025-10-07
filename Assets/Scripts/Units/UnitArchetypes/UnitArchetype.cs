using UnityEngine;

[CreateAssetMenu(menuName = "RogueShooter/UnitArchetype")]
public class UnitArchetype : ScriptableObject
{
    [Header("Base skills")]
    public int personalCoverMax = 200;
    public int coverRegenOnMove = 20;
    public int coverRegenPerUnusedAP = 50;

    [Header("Progression (optional)")]
    public AnimationCurve coverMaxByLevel = AnimationCurve.Linear(1, 200, 10, 300);
    public AnimationCurve regenByLevel = AnimationCurve.Linear(1, 20, 10, 35);

}

