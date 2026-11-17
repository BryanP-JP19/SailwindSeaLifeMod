// Bottlenose Dolphin settings and future behavior overrides belong in this file.
// The species stays inactive until its prefab is present in the AssetBundle.
public sealed class BottlenoseDolphinAI : MarineAnimalAI
{
    // All Bottlenose Dolphin tuning is defined here. Shared swimming/Boids algorithms live
    // in MarineAnimalAI and MarinePod, not in this species-specific file.
    public static MarineSpecies CreateDefinition()
    {
        return new MarineSpecies
        {
            // Asset and natural spawning
            Name = "Bottlenose Dolphin",
            PrefabName = "BottlenoseDolphinPrefab",
            Weight = 50,
            MinSpawnRadius = 75,
            MaxSpawnRadius = 350,
            DespawnRadius = 700,
            MinSpawnWaterDepth = 1.5f,
            SpawnFootprintForward = 3f,
            SpawnFootprintSide = 1.5f,
            MinGroup = 1,
            MaxGroup = 8,

            // Swimming and navigation
            MinSpeed = 4,
            MaxSpeed = 10,
            TurnDegreesPerSecond = 75,
            SurfaceOffset = -1,
            AvoidanceRadius = 2,
            LookAhead = 38,
            GroupSpacing = 7,

            // Wake riding
            CanBowRide = false,

            ControllerType = typeof(BottlenoseDolphinAI)
        };
    }
}
