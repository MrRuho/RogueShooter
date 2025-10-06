using UnityEngine;

public class PathDiagHotkey : MonoBehaviour
{
    public KeyCode dumpKey = KeyCode.O;
    public KeyCode resetKey = KeyCode.P;

    void Update()
    {
        var diag = PathfindingDiagnostics.Instance;
        if (diag == null) return;

        if (Input.GetKeyDown(dumpKey))
        {
            Debug.Log(
                $"[PathDiag] Samples={diag.SamplesCount} | Avg={diag.AvgMs:F3} ms | P50={diag.P50Ms:F3} ms | P95={diag.P95Ms:F3} ms | Calls={diag.CallsTotal} | OK={diag.SuccessesTotal} | Fail={diag.FailuresTotal}"
            );
        }

        if (Input.GetKeyDown(resetKey))
        {
            diag.ResetStats();
            Debug.Log("[PathDiag] Reset");
        }
    }
}

