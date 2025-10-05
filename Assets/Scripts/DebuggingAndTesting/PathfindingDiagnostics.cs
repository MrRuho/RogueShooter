using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public class PathfindingDiagnostics : MonoBehaviour
{
    public static PathfindingDiagnostics Instance { get; private set; }

    [Header("On/Off")]
    public bool enabledRuntime = false;     // kytkin pelissä

    [Header("Window")]
    public int windowSize = 200;            // montako viimeisintä mittausta pidetään

    // Näkyvät lukemat
    public int SamplesCount => samples.Count;
    public double AvgMs { get; private set; }
    public double P95Ms { get; private set; }
    public double P50Ms { get; private set; } // mediaani
    public int CallsTotal { get; private set; }
    public int SuccessesTotal { get; private set; }
    public int FailuresTotal => CallsTotal - SuccessesTotal;

    struct Sample { public double ms; public bool success; public int pathLen; public int expanded; }
    readonly Queue<Sample> samples = new Queue<Sample>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddSample(double ms, bool success, int pathLen, int expanded)
    {
        if (!enabledRuntime) return;

        CallsTotal++;
        if (success) SuccessesTotal++;

        samples.Enqueue(new Sample { ms = ms, success = success, pathLen = pathLen, expanded = expanded });
        while (samples.Count > windowSize) samples.Dequeue();

        RecomputeStats();
    }

    void RecomputeStats()
    {
        if (samples.Count == 0)
        {
            AvgMs = P95Ms = P50Ms = 0;
            return;
        }

        double sum = 0;
        List<double> arr = new List<double>(samples.Count);
        foreach (var s in samples) { sum += s.ms; arr.Add(s.ms); }

        arr.Sort();
        AvgMs = sum / samples.Count;
        P50Ms = Percentile(arr, 0.50);
        P95Ms = Percentile(arr, 0.95);
    }

    static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        double idx = (sorted.Count - 1) * p;
        int lo = (int)Math.Floor(idx);
        int hi = (int)Math.Ceiling(idx);
        if (lo == hi) return sorted[lo];
        double w = idx - lo;
        return sorted[lo] * (1 - w) + sorted[hi] * w;
    }

    // Helppo nollaus napista
    public void ResetStats()
    {
        samples.Clear();
        CallsTotal = 0;
        SuccessesTotal = 0;
        AvgMs = P95Ms = P50Ms = 0;
    }
}
