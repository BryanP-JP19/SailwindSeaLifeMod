using UnityEngine;

// Common species-data schema and registry. Individual values live in each
// species AI class. No Unity editor asset is needed; absent prefabs stay inactive.
public sealed class MarineSpecies
{
    public string Name;
    public string PrefabName;
    public int Weight;
    public float MinSpawnRadius;
    public float MaxSpawnRadius;
    public float DespawnRadius;
    // Required depth of still water below mean sea level at all sampled points.
    // A little extra safety margin for waves is added in SpawnWaterValidator.
    public float MinSpawnWaterDepth = 3f;
    // Approximate horizontal footprint of the animal at spawn. Sample seafloor
    // at the center, head, tail and both flanks; use max size for conservatism.
    public float SpawnFootprintForward = 5f;
    public float SpawnFootprintSide = 2f;
    public int MinGroup;
    public int MaxGroup;
    public float MinSpeed;
    public float MaxSpeed;
    public float TurnDegreesPerSecond;
    // Turn animation only: reference rate giving a BendAmount of +/-1,
    // and response smoothing. Defaults preserve the v0.1.5 Fin Whale look.
    public float TurnAnimationReferenceDegreesPerSecond = 40f;
    public float TurnAnimationSmoothing = .5f;
    public float SwimAcceleration = 1f;
    public float SwimDeceleration = 1.5f;
    // Maximum rate at which actual travel direction follows body orientation.
    public float VelocityTurnDegreesPerSecond = 45f;
    public float SpeedChangeMinSeconds = 18f;
    public float SpeedChangeMaxSeconds = 40f;
    public float HazardSpeedFraction = .65f;
    // Optional animation controls; require matching Animator setup in Unity.
    public string SwimState = "Swim";
    public string SwimSpeedParameter = "SwimSpeedMultiplier";
    public float MinSwimAnimationMultiplier = .35f;
    public bool OrcaAppearanceAndAnimations;
    public string SurfaceTrigger = "Surface";
    public string BreachTrigger = "Breach";
    public float AnimationEventMinSeconds = 30f;
    public float AnimationEventMaxSeconds = 90f;
    public float OrcaMaleProbability = .35f;
    // Optional species-specific size multiplier for males. Applied AFTER the
    // universal random scale, only by species that implement sexual dimorphism.
    public float MaleScaleMultiplier = 1f;
    // Zero keeps the original immediate turn-rate response for other species.
    public float TurnAccelerationDegreesPerSecondSquared = 0f;
    public float SurfaceOffset = -2f;
    public float AvoidanceRadius = 5f;
    public float LookAhead = 50f;
    public float GroupSpacing = 20f;
    // Predictive podmate avoidance. These control personal space, not a formation.
    // The fore/aft distance is larger than the lateral distance because these
    // animals have long bodies and can close quickly during wake pursuit.
    public float PodPersonalSpaceForward = 0f;
    public float PodPersonalSpaceSide = 0f;
    public float PodPredictionSeconds = 1.8f;
    public float PodPassingStrength = 7f;
    public float PodFollowingDeceleration = 3.5f;
    // Low-amplitude, independently smoothed speed variation while wandering.
    // Zero preserves the existing movement of species not explicitly opted in.
    public float CruiseSpeedJitterFraction = 0f;
    public bool HasFinWhaleAnimations;
    public bool CanBowRide;
    // An attraction corridor along the forward quarter of a vessel, not fixed
    // formation slots. Distances are measured from the ship Rigidbody footprint.
    // The near-hull limit includes an allowance for the animal's body width.
    public float WakeForwardStartFraction = .20f;
    public float WakeAheadOfBow = 3f;
    public float WakeBodyHalfWidth = 1.5f;
    public float WakeSideClearance = 2.5f;
    public float WakeOuterWanderWidth = 3.5f;
    public float WakeDriftRate = 2f;
    // Sprint is ONLY available during an active wake encounter. Normal wildlife
    // cruising continues to use MinSpeed/MaxSpeed.
    public float WakeMaxSwimSpeedKnots = 0f;
    public float WakeAcceleration = 0f;
    public float WakeDeceleration = 0f;
    // Optional reference speed where swim animation playback is 1.0x.
    // Zero preserves each species' previous MaxSpeed animation reference.
    public float SwimAnimationReferenceMetersPerSecond = 0f;
    // The spawner instantiates this species' own controller without a chain of
    // hardcoded species-name conditions. Set it inside CreateDefinition().
    public System.Type ControllerType = typeof(MarineAnimalAI);
    public GameObject Prefab;

    // The species class is a shared schema only. Every animal's individual
    // tuning lives beside its unique behavior in that animal's AI file.
    // The order below controls only the registry order, not spawn weights.
    public static MarineSpecies[] Defaults()
    {
        return new MarineSpecies[]
        {
            FinWhaleAI.CreateDefinition(),
            OrcaAI.CreateDefinition(),
            BottlenoseDolphinAI.CreateDefinition(),
            BeakedWhaleAI.CreateDefinition(),
            SpermWhaleAI.CreateDefinition(),
            WhaleSharkAI.CreateDefinition()
        };
    }
}
