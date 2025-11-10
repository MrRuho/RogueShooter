
using UnityEngine;
using System.Collections.Generic;

public static class OverwatchVisionUpdater
{
    // --- lisätty: kevyt kuristus online-lähetyksille ---
    private const float SEND_INTERVAL = 0.1f;  // ~5 Hz
    private const float MIN_DEG_STEP = 1f;     // lähetä vasta kun kääntynyt ≥ 6°
    private static readonly Dictionary<int, float> s_nextSendAt = new();
    private static readonly Dictionary<int, byte> s_lastYawQ = new();

    public static void UpdateVision(Unit unit, Vector3 facingWorld, float coneAngle)
    {
        if (unit == null) return;

        facingWorld = OverwatchHelpers.NormalizeFacing(facingWorld);

        UpdatePayload(unit, facingWorld, coneAngle);
        UpdateLocalOverlay(unit, facingWorld, coneAngle);
        UpdateVisionCacheAndTeamVision(unit, facingWorld, coneAngle);
    }

    private static void UpdatePayload(Unit unit, Vector3 facingWorld, float coneAngle)
    {
        if (unit.TryGetComponent<UnitStatusController>(out var status))
        {
            if (status.TryGet<OverwatchPayload>(UnitStatusType.Overwatch, out var payload))
            {
                payload.facingWorld = facingWorld;
                payload.coneAngleDeg = coneAngle;
                status.AddOrUpdate(UnitStatusType.Overwatch, payload);
            }
        }
    }

    private static void UpdateLocalOverlay(Unit unit, Vector3 facingWorld, float coneAngle)
    {
        if (unit.TryGetComponent<UnitVision>(out var vision))
        {
            vision.ShowUnitOverWatchVision(facingWorld, coneAngle);
        }
    }

    private static void UpdateVisionCacheAndTeamVision(Unit unit, Vector3 facingWorld, float coneAngle)
    {
        if (NetworkSync.IsOffline)
        {
            UpdateVisionOffline(unit, facingWorld, coneAngle);
        }
        else if (Mirror.NetworkServer.active && NetworkSyncAgent.Local != null)
        {
            // --- lisätty: kuristus ennen raskasta polkua ---
            if (!ShouldSendOnline(unit, facingWorld)) return;

            UpdateVisionOnline(unit, facingWorld, coneAngle);
        }
    }

    private static void UpdateVisionOffline(Unit unit, Vector3 facingWorld, float coneAngle)
    {
        if (unit.TryGetComponent<UnitVision>(out var vision) && vision.IsInitialized)
        {
            vision.UpdateVisionNow();
            vision.ApplyAndPublishDirectionalVision(facingWorld, coneAngle);
        }
    }

    private static void UpdateVisionOnline(Unit unit, Vector3 facingWorld, float coneAngle)
    {
        if (unit.TryGetComponent<UnitVision>(out var vision) && vision.IsInitialized)
        {
            vision.UpdateVisionNow();
            vision.ApplyAndPublishDirectionalVision(facingWorld, coneAngle);
        }

        var ni = unit.GetComponent<Mirror.NetworkIdentity>();
        if (ni != null)
        {
            NetworkSyncAgent.Local.RpcUpdateSingleUnitVision(ni.netId, facingWorld, coneAngle);
        }
    }

    // --- lisätty: yksinkertainen kulma- ja aika-kuristin ---
    private static bool ShouldSendOnline(Unit unit, Vector3 facingWorld)
    {
        int id = unit.GetInstanceID();
        float now = Time.time;

        if (s_nextSendAt.TryGetValue(id, out var next) && now < next)
            return false;

        // kvantisoi yaw 0..255 (halpa tapa mitata kulmamuutos)
        float yaw360 = Mathf.Repeat(Mathf.Atan2(facingWorld.x, facingWorld.z) * Mathf.Rad2Deg, 360f);
        byte yawQ = (byte)Mathf.RoundToInt(yaw360 * 255f / 360f);

        if (s_lastYawQ.TryGetValue(id, out var last))
        {
            float degStep = Mathf.Abs((yawQ - last) * (360f / 255f));
            if (degStep < MIN_DEG_STEP) return false;
        }

        s_lastYawQ[id] = yawQ;
        s_nextSendAt[id] = now + SEND_INTERVAL;
        return true;
    }
}
