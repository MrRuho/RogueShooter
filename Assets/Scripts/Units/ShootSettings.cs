using System;
using UnityEngine;

[Serializable]
public class ShootingSettings
{
    [Header("Aiming Settings")]
    [Tooltip("Kääntymisen nopeus (suurempi = nopeampi)")]
    public float aimTurnSpeed = 10f;
    
    [Tooltip("Minimiaika tähtäämiseen kun kohdistus on saavutettu")]
    public float minAimTime = 0.40f;
    
    [Header("Timing Settings")]
    [Tooltip("Kokonaisaika tähtäystilassa")]
    public float aimingStateTime = 1f;
    
    [Tooltip("Aika ampumistilan jälkeen")]
    public float cooloffStateTime = 0.5f;
}
