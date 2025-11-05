using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    [SerializeField] private UnitAnimator unitAnimator;

    void Awake()
    {
        if (!unitAnimator) unitAnimator = GetComponentInParent<UnitAnimator>();
    }

    public void AE_ThrowGrenadeStandRelease()
    {
        unitAnimator?.AE_ThrowGrenadeStandRelease();
    }

    public void AE_PickGrenadeStand()
    {
        unitAnimator?.AE_PickGrenadeStand();
    }

    public void AE_OnGrenadeThrowStandFinished()
    {
        unitAnimator?.AE_OnGrenadeThrowStandFinished();
    }
}