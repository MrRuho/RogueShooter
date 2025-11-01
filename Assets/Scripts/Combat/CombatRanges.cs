using UnityEngine;

[CreateAssetMenu(menuName="RogueShooter/Combat Ranges")]
public class CombatRanges : ScriptableObject
{

    [Header("Use tiles instead of world units")]
    public bool useTiles = true;

    [Header("Max distance per band (in tiles)")]
    public int meleeMaxTiles  = 1;  // "vieressä"
    public int closeMaxTiles  = 5;
    public int mediumMaxTiles = 15;
    public int longMaxTiles = 20;
    
    [Header("Legacy world units (fallback if useTiles==false)")]
    public float meleeMaxWU  = 1.2f;
    public float closeMaxWU  = 4f;
    public float mediumMaxWU = 9f;
    public float longMaxWU   = 15f;
}
