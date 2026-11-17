using UnityEngine;

// Orca appearance and animation configuration; Boids and navigation remain shared.
public sealed class OrcaAI : MarineAnimalAI
{
    // All Orca tuning is defined here. Shared swimming/Boids algorithms live
    // in MarineAnimalAI and MarinePod, not in this species-specific file.
    public static MarineSpecies CreateDefinition()
    {
        return new MarineSpecies
        {
            // Asset and natural spawning
            Name = "Orca",
            PrefabName = "OrcaPrefab",
            Weight = 25,
            MinSpawnRadius = 200,
            MaxSpawnRadius = 600,
            DespawnRadius = 1600,
            MinSpawnWaterDepth = 2f,
            SpawnFootprintForward = 5f,
            SpawnFootprintSide = 3f,
            MinGroup = 1,
            MaxGroup = 5,

            // Swimming and navigation
            MinSpeed = 3,
            MaxSpeed = 8,
            TurnDegreesPerSecond = 40,
            TurnAccelerationDegreesPerSecondSquared = 27f,
            SwimAcceleration = 1.6f,
            SwimDeceleration = 2.2f,
            VelocityTurnDegreesPerSecond = 48f,
            SurfaceOffset = -1.5f,
            AvoidanceRadius = 3,
            LookAhead = 48,
            GroupSpacing = 15,
            HazardSpeedFraction = .65f,

            // Animations and species-specific appearance
            OrcaAppearanceAndAnimations = true,
            OrcaMaleProbability = .35f,
            MaleScaleMultiplier = 1.15f,
            SwimState = "Swim",
            SwimSpeedParameter = "SwimSpeedMultiplier",
            MinSwimAnimationMultiplier = .35f,
            TurnAnimationReferenceDegreesPerSecond = 40f,
            TurnAnimationSmoothing = .5f,
            SurfaceTrigger = "Surface",
            BreachTrigger = "Breach",
            AnimationEventMinSeconds = 30f,
            AnimationEventMaxSeconds = 90f,
            SwimAnimationReferenceMetersPerSecond = 15.43333f,

            // Group movement and separation
            PodPersonalSpaceForward = 11f,
            PodPersonalSpaceSide = 4.5f,
            PodPredictionSeconds = 2.0f,
            PodPassingStrength = 7.5f,
            PodFollowingDeceleration = 3.5f,
            CruiseSpeedJitterFraction = .05f,

            // Wake riding
            CanBowRide = true,
            WakeForwardStartFraction = .20f,
            WakeAheadOfBow = 4f,
            WakeBodyHalfWidth = 1.5f,
            WakeSideClearance = 2.25f,
            WakeOuterWanderWidth = 3.5f,
            WakeDriftRate = 2f,
            WakeMaxSwimSpeedKnots = 30f,
            WakeAcceleration = 2.8f,
            WakeDeceleration = .9f,

            ControllerType = typeof(OrcaAI)
        };
    }

    public bool IsMale { get; private set; }

    protected override void ConfigureSpecies()
    {
        IsMale = Random.value < Mathf.Clamp01(Species.OrcaMaleProbability);
        // The imported FBX has a female base mesh with male morph at 100%.
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            int index = smr.sharedMesh.GetBlendShapeIndex("OrcaMale");
            if (index < 0) continue;
            smr.SetBlendShapeWeight(index, IsMale ? 100f : 0f);
        }
        // No per-spawn console message in the release build if an optional
        // appearance morph is absent; swimming and navigation still work.
        if (IsMale)
            ApplySpeciesSizeMultiplier(Species.MaleScaleMultiplier);
    }
}
