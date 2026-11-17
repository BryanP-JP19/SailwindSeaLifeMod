// Whale Shark settings and future behavior overrides belong in this file.
// The species stays inactive until its prefab is present in the AssetBundle.
public sealed class WhaleSharkAI : MarineAnimalAI
{
    // All Whale Shark tuning is defined here. Shared swimming/Boids algorithms live
    // in MarineAnimalAI and MarinePod, not in this species-specific file.
    public static MarineSpecies CreateDefinition()
    {
        return new MarineSpecies
        {
            // Asset and natural spawning
            Name = "Whale Shark",
            PrefabName = "WhaleSharkPrefab",
            Weight = 2,
            MinSpawnRadius = 100,
            MaxSpawnRadius = 400,
            DespawnRadius = 1100,
            MinSpawnWaterDepth = 4f,
            SpawnFootprintForward = 6f,
            SpawnFootprintSide = 3f,
            MinGroup = 1,
            MaxGroup = 1,

            // Swimming and navigation
            MinSpeed = 1,
            MaxSpeed = 4,
            TurnDegreesPerSecond = 20,
            SurfaceOffset = -2,
            AvoidanceRadius = 4,
            LookAhead = 45,
            GroupSpacing = 20,

            ControllerType = typeof(WhaleSharkAI)
        };
    }
}
