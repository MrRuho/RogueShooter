using UnityEngine;

public static class SircleCalculator
{
   
   public static int Sircle(int x, int z)
    {
        int ax = Mathf.Abs(x);
        int az = Mathf.Abs(z);
        int diag = Mathf.Min(ax, az);
        int straight = Mathf.Abs(ax - az);
        int cost = 14 * diag + 10 * straight;

        return cost;
    }
}
