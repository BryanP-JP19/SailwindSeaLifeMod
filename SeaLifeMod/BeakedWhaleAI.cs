// Beaked Whale settings and future behavior overrides belong in this file.
// The species stays inactive until its prefab is present in the AssetBundle.
public sealed class BeakedWhaleAI : MarineAnimalAI
{
    // All Beaked Whale tuning is defined here. Shared swimming/Boids algorithms live
    // in MarineAnimalAI and MarinePod, not in this species-specific file.
    public static MarineSpecies CreateDefinition()
    {
        return new MarineSpecies
        {
            // Asset and natural spawning
            Name = "Beaked Whale",
            PrefabName = "BeakedWhalePrefab",
            Weight = 8,
            MinSpawnRadius = 250,
            MaxSpawnRadius = 700,
            DespawnRadius = 2000,
            MinSpawnWaterDepth = 4f,
            SpawnFootprintForward = 7f,
            SpawnFootprintSide = 3f,
            MinGroup = 1,
            MaxGroup = 3,

            // Swimming and navigation
            MinSpeed = 3,
            MaxSpeed = 7,
            TurnDegreesPerSecond = 30,
            SurfaceOffset = -2,
            AvoidanceRadius = 4,
            LookAhead = 55,
            GroupSpacing = 25,

            ControllerType = typeof(BeakedWhaleAI)
        };
    }
}
