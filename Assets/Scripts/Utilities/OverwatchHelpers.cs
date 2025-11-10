using UnityEngine;

public static class OverwatchHelpers
{
    public static Vector3 NormalizeFacing(Vector3 facing)
    {
        facing.y = 0f;
        
        if (facing.sqrMagnitude < 1e-6f)
        {
            return Vector3.forward;
        }
        
        return facing.normalized;
    }
}

