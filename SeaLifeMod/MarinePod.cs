using System.Collections.Generic;
using UnityEngine;

// Each pod keeps the original loose Boids system. Optional Orca wake interest
// changes the pod's shared intention, NOT individual formation positions.
public sealed class MarinePod : MonoBehaviour
{
    private enum WakePhase { Wandering, Approaching, Riding, Cooldown }
    // A ship-relative corridor, deliberately not a single waypoint. Orcas can
    // circulate around one another without being assigned formation slots.
    private struct WakeCorridor
    {
        public float NearAlong;
        public float FarAlong;
        public float NearSide;
        public float FarSide;
        public float SideSign;
        public float CenterAlong { get { return (NearAlong + FarAlong) * .5f; } }
        public float CenterSide { get { return (NearSide + FarSide) * .5f; } }
    }

    public readonly List<MarineAnimalAI> Members = new List<MarineAnimalAI>();
    private Vector3 desiredHeading;
    private float nextHeadingChange;
    private float nextMaintenance;
    private float nextWakeEvaluation;
    private float nextInterestAttempt;
    private float wakeStopTime;
    private float wakeCooldownEnd;
    private float approachDeadline;
    private WakePhase wakePhase = WakePhase.Wandering;
    private MarineSpecies species;
    private Transform wakeBoat;
    private float wakeSide;
    public Vector3 DesiredHeading { get { return desiredHeading; } }
    public bool IsWakeEngaged { get { return wakePhase == WakePhase.Approaching || wakePhase == WakePhase.Riding; } }
    public bool IsWakeRiding { get { return wakePhase == WakePhase.Riding; } }

    // Exposed only to the optional SeaLifeDebugTools helper.
    internal string DebugWakeState { get { return wakePhase.ToString(); } }
    internal bool DebugIsRidingHere(ShipWakeData ship, MarineAnimalAI animal)
    {
        return wakePhase == WakePhase.Riding &&
            IsInsideWakeCorridor(ship, animal.transform.position, 3.5f);
    }
    internal bool DebugTryGetWakeTarget(ShipWakeData ship, out Vector3 target)
    {
        target = Vector3.zero;
        if (species == null || !species.CanBowRide) return false;
        target = WakeAreaCenter(ship);
        return true;
    }

    public void Initialize(Vector3 heading, MarineSpecies podSpecies)
    {
        species = podSpecies;
        desiredHeading = Vector3.ProjectOnPlane(heading, Vector3.up).normalized;
        if (desiredHeading.sqrMagnitude < .01f) desiredHeading = Vector3.forward;
        nextHeadingChange = Time.time + Random.Range(15f, 40f);
        nextWakeEvaluation = Time.time + Random.Range(.2f, 1.2f);
    }

    private void Update()
    {
        float now = Time.time;
        if (now >= nextMaintenance)
        {
            nextMaintenance = now + 1f;
            Members.RemoveAll(member => member == null);
            if (Members.Count == 0) { Destroy(gameObject); return; }
        }
        if (now >= nextHeadingChange)
        {
            nextHeadingChange = now + Random.Range(15f, 40f);
            desiredHeading = Quaternion.Euler(0, Random.Range(-35f, 35f), 0) * desiredHeading;
        }
        // Orcas only in this initial implementation. Dolphins may opt in after testing.
        if (species != null && species.OrcaAppearanceAndAnimations && species.CanBowRide &&
            now >= nextWakeEvaluation)
        {
            nextWakeEvaluation = now + Random.Range(.75f, 1.25f);
            EvaluateWake(now);
        }
    }

    private Vector3 PodCenter()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (MarineAnimalAI animal in Members)
        {
            if (animal == null) continue;
            sum += animal.transform.position;
            count++;
        }
        return count > 0 ? sum / count : transform.position;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // Preview the side closest to a wandering pod. Lock that side when interest
    // begins, preventing the attraction corridor from flipping around the hull.
    private float ActiveWakeSide(ShipWakeData ship)
    {
        if (IsWakeEngaged) return wakeSide;
        return Vector3.Dot(PodCenter() - ship.Position, ship.Right) < 0f ? -1f : 1f;
    }

    private WakeCorridor GetWakeCorridor(ShipWakeData ship)
    {
        WakeCorridor region = new WakeCorridor();
        // Start alongside the forward hull, and extend just beyond the bow.
        // This responds to vessel size rather than adding a speed-scaled lead.
        region.NearAlong = ship.BowExtent * Mathf.Clamp01(species.WakeForwardStartFraction);
        region.FarAlong = ship.BowExtent + Mathf.Max(0f, species.WakeAheadOfBow);
        region.NearSide = ship.HalfBeam + Mathf.Max(0f, species.WakeBodyHalfWidth)
            + Mathf.Max(.25f, species.WakeSideClearance);
        region.FarSide = region.NearSide + Mathf.Max(0f, species.WakeOuterWanderWidth);
        region.SideSign = ActiveWakeSide(ship);
        return region;
    }

    private Vector3 PointInCorridor(ShipWakeData ship, float along, float side)
    {
        WakeCorridor region = GetWakeCorridor(ship);
        return ship.Position + ship.Forward * along +
            ship.Right * (side * region.SideSign);
    }

    private Vector3 WakeAreaCenter(ShipWakeData ship)
    {
        WakeCorridor region = GetWakeCorridor(ship);
        return PointInCorridor(ship, region.CenterAlong, region.CenterSide);
    }

    private Vector3 WakeAnimalTarget(ShipWakeData ship, MarineAnimalAI animal)
    {
        WakeCorridor region = GetWakeCorridor(ship);
        Vector2 drift = animal.WakeDrift;
        // Constrain wandering within the *area*, never allow an offset to move
        // an animal's desired position through the hull's safety boundary.
        float along = Mathf.Clamp(region.CenterAlong + drift.y,
            region.NearAlong, region.FarAlong);
        float side = Mathf.Clamp(region.CenterSide + drift.x,
            region.NearSide, region.FarSide);
        return PointInCorridor(ship, along, side);
    }

    // Distance from this individual's position to the nearest point in the
    // safe rectangular region. Unlike distance to a drifting target, this is
    // zero throughout the whole corridor, preventing random speed surges.
    private float DistanceOutsideWakeCorridor(ShipWakeData ship, Vector3 worldPosition)
    {
        WakeCorridor region = GetWakeCorridor(ship);
        Vector3 relative = Flat(worldPosition - ship.Position);
        float along = Vector3.Dot(relative, ship.Forward);
        float side = Vector3.Dot(relative, ship.Right) * region.SideSign;
        float foreGap = Mathf.Max(0f, Mathf.Max(region.NearAlong - along, along - region.FarAlong));
        float sideGap = Mathf.Max(0f, Mathf.Max(region.NearSide - side, side - region.FarSide));
        return Mathf.Sqrt(foreGap * foreGap + sideGap * sideGap);
    }

    private bool IsInsideWakeCorridor(ShipWakeData ship, Vector3 worldPosition,
        float outwardMargin)
    {
        WakeCorridor region = GetWakeCorridor(ship);
        Vector3 relative = Flat(worldPosition - ship.Position);
        float along = Vector3.Dot(relative, ship.Forward);
        float side = Vector3.Dot(relative, ship.Right) * region.SideSign;
        // A little latitude fore/aft and outboard avoids a rigid formation;
        // do NOT loosen the inner boundary enough to put a whale in the hull.
        return along >= region.NearAlong - 2.5f &&
            along <= region.FarAlong + 2.5f &&
            side >= region.NearSide - .5f &&
            side <= region.FarSide + outwardMargin;
    }

    private void EvaluateWake(float now)
    {
        SeaLifePlugin plugin = SeaLifePlugin.Instance;
        if (plugin == null || !plugin.WakeRidingEnabled)
        {
            if (IsWakeEngaged) LeaveWake(now);
            return;
        }
        ShipWakeData ship;
        bool hasShip = plugin.TryGetShipWake(out ship);

        if (wakePhase == WakePhase.Cooldown)
        {
            if (now >= wakeCooldownEnd) wakePhase = WakePhase.Wandering;
            else return;
        }
        if (IsWakeEngaged)
        {
            if (!hasShip || ship.Boat != wakeBoat || !ship.IsMovingForward ||
                ship.SpeedKnots < plugin.WakeExitKnots)
            {
                LeaveWake(now);
                return;
            }
            float distanceToShip = Flat(PodCenter() - ship.Position).magnitude;
            if (distanceToShip > plugin.WakeAttractionRadius * 2.5f)
            {
                LeaveWake(now);
                return;
            }
            if (wakePhase == WakePhase.Approaching)
            {
                if (now > approachDeadline) { LeaveWake(now); return; }
                // The first member reaching the safe forward-hull corridor
                // starts the ride timer; the rest remain free to catch up.
                // Pod centroid is a poor test for loose groups spanning 10-30m.
                int arrived = 0;
                foreach (MarineAnimalAI animal in Members)
                    if (animal != null && IsInsideWakeCorridor(ship,
                        animal.transform.position, 2.5f)) arrived++;
                if (arrived > 0)
                {
                    wakePhase = WakePhase.Riding;
                    wakeStopTime = now + Random.Range(plugin.WakeMinDurationSeconds, plugin.WakeMaxDurationSeconds);
                }
            }
            else if (now >= wakeStopTime)
            {
                LeaveWake(now);
            }
            return;
        }

        if (now < nextInterestAttempt || !hasShip || !ship.IsMovingForward ||
            ship.SpeedKnots < plugin.WakeEnterKnots) return;
        float distance = Flat(PodCenter() - ship.Position).magnitude;
        if (distance > plugin.WakeAttractionRadius) return;
        // Do not compare against the normal 8m/s cruise limit: an Orca can
        // sprint at up to 30 knots while actively chasing a vessel.
        float sprintMaximum = Mathf.Max(species.MaxSpeed,
            species.WakeMaxSwimSpeedKnots / 1.94384f);
        if (ship.SpeedMetersPerSecond >= sprintMaximum - .5f) return;
        nextInterestAttempt = now + Random.Range(45f, 95f);
        if (Random.value > plugin.WakeInterestChance) return;
        float relativeSide = Vector3.Dot(PodCenter() - ship.Position, ship.Right);
        wakeSide = Mathf.Abs(relativeSide) > 3f ? Mathf.Sign(relativeSide) :
            (Random.value < .5f ? -1f : 1f);
        wakeBoat = ship.Boat;
        wakePhase = WakePhase.Approaching;
        approachDeadline = now + plugin.WakeApproachTimeoutSeconds;
    }

    private void LeaveWake(float now)
    {
        // Resume wandering from the pod's CURRENT course, not its pre-ride
        // heading from several minutes ago. This prevents an abrupt U-turn.
        Vector3 currentCourse = Vector3.zero;
        foreach (MarineAnimalAI animal in Members)
            if (animal != null) currentCourse += Flat(animal.transform.forward);
        if (currentCourse.sqrMagnitude > .01f)
            desiredHeading = Quaternion.Euler(0f, wakeSide * Random.Range(10f, 35f), 0f)
                * currentCourse.normalized;
        SeaLifePlugin plugin = SeaLifePlugin.Instance;
        wakePhase = WakePhase.Cooldown;
        wakeBoat = null;
        wakeCooldownEnd = now + (plugin != null
            ? Random.Range(plugin.WakeMinCooldownSeconds, plugin.WakeMaxCooldownSeconds)
            : Random.Range(30f, 300f));
    }

    public bool TryGetWakeSpeed(MarineAnimalAI animal, out float requestedSpeed)
    {
        requestedSpeed = 0f;
        if (!IsWakeEngaged || species == null) return false;
        SeaLifePlugin plugin = SeaLifePlugin.Instance;
        ShipWakeData ship;
        if (plugin == null || !plugin.TryGetShipWake(out ship) || ship.Boat != wakeBoat) return false;
        // IMPORTANT: Even after the first Orca starts the pod's ride timer,
        // distant members retain catch-up authority until they arrive.
        float outside = DistanceOutsideWakeCorridor(ship, animal.transform.position);
        Vector3 target = WakeAnimalTarget(ship, animal);
        float alongError = Vector3.Dot(Flat(target - animal.transform.position), ship.Forward);
        // Never sprint farther *ahead* of the boat just to correct a lateral
        // gap. Allow speed to fall slightly below boat speed until it catches up.
        float behind = Mathf.Max(0f, alongError);
        float lateralContribution = Mathf.Max(0f, outside - behind) * .30f;
        float approachDistance = behind + lateralContribution;
        // Blend down well before the boat, leaving room for controlled braking.
        // SmoothStep has a zero slope at each end (no abrupt mode switch).
        float pursuit = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(approachDistance / 40f));
        float pacingCorrection = Mathf.Clamp(alongError * .035f, -1.25f, 1.15f);
        float ridingPace = ship.SpeedMetersPerSecond * (1f + animal.WakePaceVariation)
            + pacingCorrection;
        float pursuitPace = ship.SpeedMetersPerSecond
            + animal.WakeApproachBonusKnots / 1.94384f;
        float sprintMaximum = Mathf.Max(species.MaxSpeed,
            species.WakeMaxSwimSpeedKnots / 1.94384f);
        requestedSpeed = Mathf.Clamp(Mathf.Lerp(ridingPace, pursuitPace, pursuit),
            .5f, sprintMaximum);
        return true;
    }

    // Nearby animals from a *different pod* may still need local avoidance.
    // This is intentionally only a proximity check: no cohesion, alignment,
    // leader selection, or shared wake target crosses pod boundaries.
    public static bool HasNearbyCrossPodNeighbor(MarineAnimalAI animal)
    {
        if (animal == null || animal.Species == null ||
            animal.Species.PodPersonalSpaceForward <= 0f ||
            animal.Species.PodPersonalSpaceSide <= 0f) return false;
        float horizon = Mathf.Max(.1f, animal.Species.PodPredictionSeconds);
        foreach (MarineAnimalAI other in MarineAnimalAI.ActiveAnimals)
        {
            if (other == null || other == animal || other.Pod == animal.Pod ||
                other.Species != animal.Species) continue;
            float radius = animal.Species.PodPersonalSpaceForward *
                Mathf.Max(1f, Mathf.Max(animal.TotalSizeMultiplier, other.TotalSizeMultiplier)) +
                horizon * (animal.CurrentSwimSpeed + other.CurrentSwimSpeed);
            Vector3 delta = Flat(other.transform.position - animal.transform.position);
            if (delta.sqrMagnitude < radius * radius) return true;
        }
        return false;
    }

    // Existing same-pod distances remain unchanged. Across separate pods,
    // leave more room for large individuals, including adult male Orcas.
    private static float CrossPodSizeFactor(MarineAnimalAI animal, MarineAnimalAI other)
    {
        return animal.Pod == other.Pod ? 1f :
            Mathf.Max(1f, Mathf.Max(animal.TotalSizeMultiplier, other.TotalSizeMultiplier));
    }

    // Short-range predictive separation supplements the original loose Boids
    // steering. It is intentionally stronger than a wake destination: arriving
    // at a moving corridor must not require swimming through another Orca.
    // Return an UNNORMALIZED lateral escape direction and an urgency (0..1).
    private Vector3 PredictPodmatePassing(MarineAnimalAI animal, ShipWakeData ship,
        bool hasWakeShip, out float urgency)
    {
        urgency = 0f;
        float forwardSpace = animal.Species.PodPersonalSpaceForward;
        float sideSpace = animal.Species.PodPersonalSpaceSide;
        if (forwardSpace <= 0f || sideSpace <= 0f) return Vector3.zero;

        Vector3 travel = Flat(animal.SwimVelocity).normalized;
        if (travel.sqrMagnitude < .01f) travel = Flat(animal.transform.forward).normalized;
        if (travel.sqrMagnitude < .01f) return Vector3.zero;
        Vector3 right = Vector3.Cross(Vector3.up, travel).normalized;
        Vector3 push = Vector3.zero;
        float horizon = Mathf.Max(.1f, animal.Species.PodPredictionSeconds);
        Vector3 selfPosition = animal.transform.position;
        Vector3 selfVelocity = Flat(animal.SwimVelocity);
        float currentSpeedMagnitude = selfVelocity.magnitude;
        float strongest = 0f;

        foreach (MarineAnimalAI other in MarineAnimalAI.ActiveAnimals)
        {
            // Only collision avoidance crosses pod boundaries; different
            // species are left for a future, size-aware interspecies policy.
            if (other == null || other == animal || other.Species != animal.Species) continue;
            Vector3 delta = Flat(other.transform.position - selfPosition);
            float scale = CrossPodSizeFactor(animal, other);
            float effectiveForwardSpace = forwardSpace * scale;
            float effectiveSideSpace = sideSpace * scale;
            // Reject distant candidates cheaply before velocity prediction.
            float reach = effectiveForwardSpace +
                horizon * (currentSpeedMagnitude + Flat(other.SwimVelocity).magnitude);
            if (delta.sqrMagnitude > reach * reach) continue;
            Vector3 relativeVelocity = Flat(other.SwimVelocity) - selfVelocity;
            float t = relativeVelocity.sqrMagnitude > .01f
                ? Mathf.Clamp(-Vector3.Dot(delta, relativeVelocity) /
                    relativeVelocity.sqrMagnitude, 0f, horizon)
                : 0f;
            Vector3 anticipated = delta + relativeVelocity * t;
            float foreGap = Mathf.Abs(Vector3.Dot(anticipated, travel));
            float lateralGap = Mathf.Abs(Vector3.Dot(anticipated, right));
            if (foreGap >= effectiveForwardSpace || lateralGap >= effectiveSideSpace) continue;

            float strength = (1f - lateralGap / effectiveSideSpace) *
                Mathf.Lerp(.35f, 1f, 1f - foreGap / effectiveForwardSpace);
            // If their actual bodies are already very close, do not diminish
            // the response merely because the predicted tracks will diverge.
            float currentFore = Mathf.Abs(Vector3.Dot(delta, travel));
            float currentSide = Mathf.Abs(Vector3.Dot(delta, right));
            if (currentFore < effectiveForwardSpace * .6f &&
                currentSide < effectiveSideSpace * .6f)
                strength = Mathf.Max(strength, .7f);

            // Choose a stable pass side, avoiding oscillation at zero lateral
            // offset. Close to the ship, bias toward open water, not the hull.
            float otherSide = Vector3.Dot(anticipated, right);
            float sideSign;
            if (Mathf.Abs(otherSide) > .4f)
                sideSign = otherSide > 0f ? -1f : 1f;
            else if (hasWakeShip)
            {
                // At the bow, two different pods may occupy exactly the same
                // swimming lane. Give one pod the outboard passing maneuver
                // while the other holds course instead of steering both
                // Orcas outward in lockstep (or pushing one into the hull).
                float outward = Vector3.Dot(right, ship.Right) * ActiveWakeSide(ship) >= 0f ? 1f : -1f;
                sideSign = animal.Pod != other.Pod &&
                    animal.Pod.GetInstanceID() > other.Pod.GetInstanceID() ? 0f : outward;
            }
            else if (animal.Pod != other.Pod)
                sideSign = animal.GetInstanceID() < other.GetInstanceID() ? 1f : -1f;
            else
                sideSign = (animal.GetInstanceID() & 1) == 0 ? 1f : -1f;

            push += right * sideSign * strength;
            strongest = Mathf.Max(strongest, strength);
        }
        urgency = strongest;
        return push.sqrMagnitude > .0001f ? push.normalized : Vector3.zero;
    }

    // Predictive following-speed limit. Only acts on podmates directly ahead
    // within the same narrow swimming lane; passing Orcas are free to accelerate.
    // Steering is primary. This provides enough braking distance for faster
    // pursuers rather than abruptly stopping at the last instant.
    public bool TryGetPodmateSpeedLimit(MarineAnimalAI animal, out float limit)
    {
        limit = float.MaxValue;
        if (animal == null || animal.Species == null ||
            animal.Species.PodPersonalSpaceForward <= 0f ||
            animal.Species.PodPersonalSpaceSide <= 0f)
            return false;
        Vector3 forward = Flat(animal.SwimVelocity).normalized;
        if (forward.sqrMagnitude < .01f) forward = Flat(animal.transform.forward).normalized;
        if (forward.sqrMagnitude < .01f) return false;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float safeGap = animal.Species.PodPersonalSpaceForward;
        float lateralLimit = animal.Species.PodPersonalSpaceSide;
        float availableBraking = Mathf.Max(1f, animal.Species.PodFollowingDeceleration);
        bool limited = false;
        foreach (MarineAnimalAI other in MarineAnimalAI.ActiveAnimals)
        {
            if (other == null || other == animal || other.Species != animal.Species) continue;
            Vector3 delta = Flat(other.transform.position - animal.transform.position);
            float scale = CrossPodSizeFactor(animal, other);
            float effectiveSafeGap = safeGap * scale;
            float effectiveLateralLimit = lateralLimit * scale;
            float along = Vector3.Dot(delta, forward);
            if (along <= 0f) continue;
            float sideways = Mathf.Abs(Vector3.Dot(delta, right));
            if (sideways > effectiveLateralLimit) continue;
            // This speed adjustment is for animals heading in roughly the same
            // direction, not an Orca crossing a different member's track.
            Vector3 otherForward = Flat(other.SwimVelocity).normalized;
            if (otherForward.sqrMagnitude > .01f && Vector3.Dot(forward, otherForward) < .6f)
                continue;
            float closing = Mathf.Max(0f, animal.CurrentSwimSpeed - other.CurrentSwimSpeed);
            float anticipatedGap = effectiveSafeGap + closing * 1.0f +
                closing * closing / (2f * availableBraking);
            if (along >= anticipatedGap) continue;
            // Ease toward the leading Orca's speed. A little undershoot when
            // genuinely crowded creates space, without a hard stop or teleport.
            float candidate = Mathf.Max(.8f,
                other.CurrentSwimSpeed + Mathf.Clamp((along - effectiveSafeGap) * .35f, -1.5f, 1.5f));
            limit = Mathf.Min(limit, candidate);
            limited = true;
        }
        return limited;
    }

    public Vector3 GetSteering(MarineAnimalAI animal)
    {
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        int nearby = 0;
        float neighborRange = animal.Species.GroupSpacing * 3f;
        float minimumSpacing = animal.Species.GroupSpacing;
        for (int i = Members.Count - 1; i >= 0; i--)
        {
            MarineAnimalAI other = Members[i];
            if (other == null) { Members.RemoveAt(i); continue; }
            if (other == animal) continue;
            Vector3 offset = other.transform.position - animal.transform.position;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr < .01f || sqr > neighborRange * neighborRange) continue;
            float distance = Mathf.Sqrt(sqr);
            nearby++;
            alignment += other.transform.forward;
            cohesion += offset;
            if (distance < minimumSpacing) separation -= offset.normalized * (minimumSpacing - distance) / minimumSpacing;
        }
        Vector3 boids = DesiredHeading * 2.5f;
        if (nearby > 0)
        {
            boids += Flat(alignment / nearby).normalized * 1.1f;
            boids += Flat(cohesion / nearby).normalized * .65f;
            boids += separation * 3.5f;
        }
        SeaLifePlugin plugin = SeaLifePlugin.Instance;
        ShipWakeData ship = default(ShipWakeData);
        bool hasWakeShip = IsWakeEngaged && plugin != null &&
            plugin.TryGetShipWake(out ship) && ship.Boat == wakeBoat;
        float passingUrgency;
        Vector3 passing = PredictPodmatePassing(animal, ship, hasWakeShip,
            out passingUrgency);
        Vector3 passingForce = passing * animal.Species.PodPassingStrength * passingUrgency;
        if (!IsWakeEngaged || !hasWakeShip)
            return Flat(boids + passingForce).normalized;
        // Each member independently wanders *within* the same safe region.
        Vector3 destination = WakeAnimalTarget(ship, animal);
        Vector3 toDestination = Flat(destination - animal.transform.position);
        float distanceToTarget = toDestination.magnitude;
        if (wakePhase == WakePhase.Approaching)
        {
            // Approach from the chosen outer side of the hull, not across its center.
            return Flat(toDestination.normalized * 3.5f + ship.Forward * 1.3f +
                boids.normalized * .7f + passingForce).normalized;
        }
        // Riding: travel along the ship's course, with loose Boids spacing and
        // a gentle attraction to a broad, drifting zone (not fixed slots).
        float correction = Mathf.Lerp(.45f, 2.5f,
            Mathf.Clamp01(distanceToTarget / 24f));
        return Flat(ship.Forward * 3f + toDestination.normalized * correction +
            boids.normalized * 1.35f + passingForce).normalized;
    }
}
