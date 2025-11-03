using System.Collections;
using UnityEngine;

public class MousePlaneMap : MonoBehaviour
{
    public static MousePlaneMap Instance { get; private set; }

    [Header("Scan settings")]
    [SerializeField] private LayerMask mousePlaneMask;    // "MousePlane"
    [SerializeField] private float cellSizeWU = 2f;       // sama kuin LevelGrid
    [SerializeField] private float halfThicknessWU = 0.3f;
    [SerializeField] private float yOffsetWU = 0.02f;

    private BitArray bits;
    private int W, H, F;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Rebuild()
    {
        var lg = LevelGrid.Instance;
        if (!lg) return;

        W = lg.GetWidth(); H = lg.GetHeight(); F = lg.GetFloorAmount();
        bits = new BitArray(W * H * F, false);

        var half = new Vector3(cellSizeWU * 0.5f, halfThicknessWU, cellSizeWU * 0.5f);

        for (int f = 0; f < F; f++)
        for (int z = 0; z < H; z++)
        for (int x = 0; x < W; x++)
        {
            var gp = new GridPosition(x, z, f);
            var c  = lg.GetWorldPosition(gp) + Vector3.up * yOffsetWU;
            bool hasPlane = Physics.CheckBox(c, half, Quaternion.identity, mousePlaneMask, QueryTriggerInteraction.Collide);
            bits[Idx(gp)] = hasPlane;
        }
    }

    public bool Has(in GridPosition gp)
    {
        if (bits == null) return false;
        if (gp.x < 0 || gp.x >= W || gp.z < 0 || gp.z >= H || gp.floor < 0 || gp.floor >= F) return false;
        return bits[Idx(gp)];
    }

    // Valmius tulevaisuuteen (esim. räjähdys tekee reiän):
    public bool Remove(in GridPosition gp) { if (!Has(gp)) return false; bits[Idx(gp)] = false; return true; }
    public bool Add(in GridPosition gp)    { if (Has(gp))  return false; bits[Idx(gp)] = true;  return true; }

    private int Idx(in GridPosition gp) => gp.floor * (W * H) + gp.z * W + gp.x;
}
