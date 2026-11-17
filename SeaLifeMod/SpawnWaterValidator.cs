using UnityEngine;
using Crest;

// A one-time spawn-position validator, NOT a continuous physics system.
// Samples depth beneath a candidate animal's center, head, tail and flanks.
// Island terrain and seabed colliders are expected to be non-trigger and static.
// Moving Rigidbody colliders (boats, loose cargo) are ignored here; the normal
// animal steering handles them after spawning.
public static class SpawnWaterValidator
{
    private const float AboveSeaStart = 500f;
    private const float WaveMargin = 1f;

    public static bool IsSafe(MarineSpecies species, Vector3 position,
        Vector3 heading, out string rejection)
    {
        rejection = "none";
        if (OceanRenderer.Instance == null)
        {
            rejection = "Crest ocean not initialized";
            return false;
        }
        float sea = OceanRenderer.Instance.SeaLevel;
        float requiredDepth = Mathf.Max(.1f, species.MinSpawnWaterDepth) + WaveMargin;
        // Account for the shared 1.5x maximum and any species-specific male
        // multiplier (Orca: 1.5 * 1.15 = 1.725). The minimum water depth
        // itself remains the species-defined threshold plus wave margin.
        float scale = MarineScale.MaximumPossibleMultiplier(species);
        float forwardDist = Mathf.Max(0f, species.SpawnFootprintForward) * scale;
        float sideDist = Mathf.Max(0f, species.SpawnFootprintSide) * scale;
        Vector3 forward = Vector3.ProjectOnPlane(heading, Vector3.up);
        forward = forward.sqrMagnitude > .001f ? forward.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
        if (!HasRequiredDepth(position, sea, requiredDepth, out rejection)) return false;
        if (!HasRequiredDepth(position + forward * forwardDist, sea, requiredDepth, out rejection)) return false;
        if (!HasRequiredDepth(position - forward * forwardDist, sea, requiredDepth, out rejection)) return false;
        if (!HasRequiredDepth(position + side * sideDist, sea, requiredDepth, out rejection)) return false;
        return HasRequiredDepth(position - side * sideDist, sea, requiredDepth, out rejection);
    }

    private static bool HasRequiredDepth(Vector3 location, float sea, float required,
        out string reason)
    {
        reason = "none";
        Vector3 rayOrigin = new Vector3(location.x, sea + AboveSeaStart, location.z);
        // Only probe down to the required depth; no terrain hit means the
        // location is at least deep enough OR has no physics seafloor collider.
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down,
            AboveSeaStart + required, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        int waterLayer = LayerMask.NameToLayer("Water");
        foreach (RaycastHit hit in hits)
        {
            Collider collider = hit.collider;
            if (collider == null || collider.isTrigger) continue;
            if (collider.attachedRigidbody != null) continue;
            if (waterLayer >= 0 && collider.gameObject.layer == waterLayer) continue;
            // Covers islands above sea, shallow sandbars, docks, and seafloor
            // shallower than the configured species threshold.
            float depth = sea - hit.point.y;
            if (depth <= required)
            {
                reason = "static collider '" + collider.name + "' at depth " +
                    depth.ToString("F1") + "m (requires >= " + required.ToString("F1") + "m)";
                return false;
            }
        }
        return true;
    }
}
