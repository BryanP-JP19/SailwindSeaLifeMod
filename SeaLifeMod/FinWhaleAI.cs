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
}
