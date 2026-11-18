using UnityEngine;

// Fin Whale-specific tuning is kept here, next to future Fin Whale behaviors.
// All common navigation, swimming and animation machinery remains shared.
public sealed class FinWhaleAI : MarineAnimalAI
{
    // All Fin Whale tuning is defined here. Shared swimming/Boids algorithms live
    // in MarineAnimalAI and MarinePod, not in this species-specific file.
    public static MarineSpecies CreateDefinition()
    {
        return new MarineSpecies
        {
            // Asset and natural spawning
            Name = "Fin Whale",
            PrefabName = "FinWhalePrefab",
            Weight = 10,
            MinSpawnRadius = 300,
            MaxSpawnRadius = 1000,
            DespawnRadius = 3000,
            MinSpawnWaterDepth = 6f,
            SpawnFootprintForward = 15f,
            SpawnFootprintSide = 6f,
            MinGroup = 1,
            MaxGroup = 1,

            // Swimming and navigation
            MinSpeed = 3,
            MaxSpeed = 7,
            TurnDegreesPerSecond = 11,
            TurnAccelerationDegreesPerSecondSquared = 5f,
            SwimAcceleration = .45f,
            SwimDeceleration = .7f,
            VelocityTurnDegreesPerSecond = 9f,
            HazardSpeedFraction = .72f,
            SurfaceOffset = -2,
            AvoidanceRadius = 5,
            LookAhead = 65,
            GroupSpacing = 35,

            // Animations and species-specific appearance
            HasFinWhaleAnimations = true,
            SwimState = "Swim",
            SwimSpeedParameter = "SwimSpeedMultiplier",
            MinSwimAnimationMultiplier = .35f,
            TurnAnimationReferenceDegreesPerSecond = 40f,
            TurnAnimationSmoothing = .5f,
            SurfaceTrigger = "Surface",
            BreachTrigger = "Breach",
            AnimationEventMinSeconds = 30f,
            AnimationEventMaxSeconds = 90f,

            ControllerType = typeof(FinWhaleAI)
        };
    }


    // ------------------------------------------------------------------
    // Random Encounters 2.0.1 backwards-compatibility surface.
    // It reflects these names from the original 2024 FinWhaleAI class.
    // All normal SeaLife whales are initialized by SeaLifePlugin; this
    // legacy path only runs for whales instantiated by another mod.
    // ------------------------------------------------------------------
    private bool legacyExternalSpawn;

    private void Start()
    {
        if (Species != null) return; // New SeaLife spawning has initialized us.
        // Random Encounters bypasses SeaLifePlugin's normal spawning path,
        // so apply the same non-physical wildlife collider safety here.
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            collider.isTrigger = true;
        // Random Encounters sets its own scale before activating the pooled
        // whale. Preserve that scale instead of rolling a second multiplier.
        InitializeExternallyManaged(CreateDefinition());
        legacyExternalSpawn = true;
    }

    private void OnEnable()
    {
        // Start runs only on the first activation. Subsequent reactivations
        // from Random Encounters use the same pooled GameObject, but with a
        // new position and orientation, so reset its kinematic state.
        if (legacyExternalSpawn) ResetExternallyManagedMovement();
    }

    // The external mod obtains this exact private method via Harmony's
    // MethodInvoker.GetHandler and calls it to animate the encounter.
    private void TriggerRandomAnimation()
    {
        TriggerDebugAnimation(Random.value < .5f);
    }

    // These three method names must exist on FinWhaleAI because Random
    // Encounters Harmony-patches them. The shared controller now performs
    // those responsibilities, so there is nothing to do in these shims.
    // Random Encounters replaces them with its no-op prefix as before.
    private void FindPlayer() { }
    private void CheckDistanceToPlayer() { }
    private void SetRandomScale() { }
}
