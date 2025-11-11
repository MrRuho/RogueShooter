using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
[ExecuteAlways]
#endif
public sealed class ExplosionSolverGizmos : MonoBehaviour
{
    [Header("Yleiset")]
    public bool draw = true;
    public float yOffset = 0.05f;
    public bool drawSteps = true;
    public bool drawBlocks = true;

    [Header("Värit")]
    public Color reachedColor = new Color(0f, 1f, 0f, 0.15f);   // vihreä läpikuultava
    public Color stopColor    = new Color(1f, 0f, 0f, 0.25f);   // punainen läpikuultava
    public Color stepLine     = new Color(0f, 0.8f, 0f, 1f);    // vihreä viiva
    public Color blockLine    = new Color(1f, 0.2f, 0.2f, 1f);  // punainen viiva
    public Color originColor  = new Color(1f, 1f, 0f, 0.45f);   // keltainen läpikuultava

    void OnDrawGizmos()
    {
        if (!draw) return;
        var lg = LevelGrid.Instance;
        var snap = ExplosionSolver.LastDebug;
        if (lg == null || snap == null) return;

        float cs = lg.GetCellSize();
        Vector3 size = new Vector3(cs * 0.95f, 0.02f, cs * 0.95f);

        // Origin
        Gizmos.color = originColor;
        Gizmos.DrawCube(CenterOf(snap.origin), size);

        // Reached tiles (vihreä)
        Gizmos.color = reachedColor;
        foreach (var gp in snap.reached)
        {
            Gizmos.DrawCube(CenterOf(gp), size);
        }

        // Stops (opaque) punaisella
        Gizmos.color = stopColor;
        foreach (var gp in snap.stops)
        {
            Gizmos.DrawCube(CenterOf(gp), size);
        }

        // Step-viivat
        if (drawSteps)
        {
            Gizmos.color = stepLine;
            foreach (var s in snap.steps)
            {
                Gizmos.DrawLine(CenterOf(s.a), CenterOf(s.b));
            }
        }

        // Block-viivat (punaiset)
        if (drawBlocks)
        {
            Gizmos.color = blockLine;
            foreach (var b in snap.blocks)
            {
                Gizmos.DrawLine(CenterOf(b.a), CenterOf(b.b));
            }
        }
    }

    Vector3 CenterOf(GridPosition gp)
    {
        var p = LevelGrid.Instance.GetWorldPosition(gp);
        p.y += yOffset;
        return p;
    }
}
