using UnityEngine;

/// <summary>
/// This is needed so that animation event-bound functions in UnitAnimator can be used. Such as AE_Throw Grenade Stand Release()
/// </summary>
public class AnimationEventRelay : MonoBehaviour
{
    [SerializeField] private UnitAnimator unitAnimator;

    void Awake()
    {
        // Etsi parentista jos ei asetettu Inspectorissa
        if (!unitAnimator) unitAnimator = GetComponentInParent<UnitAnimator>();
    }

    // Täsmälleen sama nimi kuin Animation Eventin Function-kentässä
    public void AE_ThrowGrenadeStandRelease()
    {
        unitAnimator?.AE_ThrowGrenadeStandRelease();
    }

    public void AE_PickGrenadeStand()
    {
        unitAnimator?.AE_PickGrenadeStand();
    }
}