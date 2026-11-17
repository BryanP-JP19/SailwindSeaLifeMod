// Sperm Whale settings and future behavior overrides belong in this file.
// The species stays inactive until its prefab is present in the AssetBundle.
public sealed class SpermWhaleAI : MarineAnimalAI
{
    // All Sperm Whale tuning is defined here. Shared swimming/Boids algorithms live
    // in MarineAnimalAI and MarinePod, not in this species-specific file.
    public static MarineSpecies CreateDefinition()
    {
        return new MarineSpecies
        {
            // Asset and natural spawning
            Name = "Sperm Whale",
            PrefabName = "SpermWhalePrefab",
            Weight = 5,
            MinSpawnRadius = 400,
            MaxSpawnRadius = 1000,
            DespawnRadius = 3000,
            MinSpawnWaterDepth = 8f,
            SpawnFootprintForward = 14f,
            SpawnFootprintSide = 6f,
            MinGroup = 1,
            MaxGroup = 4,

            // Swimming and navigation
            MinSpeed = 2,
            MaxSpeed = 6,
            TurnDegreesPerSecond = 20,
            SurfaceOffset = -2,
            AvoidanceRadius = 6,
            LookAhead = 80,
            GroupSpacing = 40,

            ControllerType = typeof(SpermWhaleAI)
        };
    }
}
