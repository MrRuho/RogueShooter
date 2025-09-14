using System;

public static class PlayerLocalTurnGate
{
    public static bool CanAct { get; private set; }
    public static event Action<bool> OnCanActChanged;

    public static void Set(bool canAct)
    {
        if (CanAct == canAct) return;
        CanAct = canAct;
        OnCanActChanged?.Invoke(CanAct);
    }
}
