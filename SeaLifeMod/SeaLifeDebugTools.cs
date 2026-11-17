using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

// Optional development helper. Release builds DO NOT attach this component.
// To enable everything (F1 Debug options, L/J/K, visual wake markers), uncomment
// ONE line in SeaLifePlugin.Awake(): gameObject.AddComponent<SeaLifeDebugTools>();
public enum DebugSpawnOrigin { ShipRigidbody, PlayerController }
public enum DebugSpawnSpecies
{
    FinWhale, Orca, BottlenoseDolphin, BeakedWhale, SpermWhale, WhaleShark
}

public sealed class SeaLifeDebugTools : MonoBehaviour
{
    private SeaLifePlugin plugin;
    private ConfigEntry<KeyCode> spawnKey, surfaceKey, breachKey;
    private ConfigEntry<DebugSpawnSpecies> spawnSpecies;
    private ConfigEntry<DebugSpawnOrigin> spawnOrigin;
    private ConfigEntry<int> spawnCount;
    private ConfigEntry<float> spawnRadius, animationTargetRadius;
    private ConfigEntry<bool> showMarkers, showWakeTargets;
    private readonly List<MarineAnimalAI> spawned = new List<MarineAnimalAI>();
    private readonly Dictionary<MarineAnimalAI, GameObject> markers =
        new Dictionary<MarineAnimalAI, GameObject>();
    private readonly Dictionary<MarinePod, GameObject> targets =
        new Dictionary<MarinePod, GameObject>();
    private float nextMarkerUpdate;
    private static Material wanderingMaterial, approachingMaterial, ridingMaterial,
        cooldownMaterial, targetMaterial;

    private void Awake()
    {
        plugin = GetComponent<SeaLifePlugin>();
        if (plugin == null) { enabled = false; return; }
        // These entries are not bound in release builds, so they do not appear in F1.
        spawnKey = plugin.Config.Bind("Debug", "ForceFinWhaleKey", KeyCode.L,
            "Spawn the selected species. Only available when developer tools are enabled.");
        surfaceKey = plugin.Config.Bind("Debug", "ForceSurfaceKey", KeyCode.J,
            "Trigger Surface on the nearest developer-spawned animal.");
        breachKey = plugin.Config.Bind("Debug", "ForceBreachKey", KeyCode.K,
            "Trigger Breach on the nearest developer-spawned animal.");
        spawnSpecies = plugin.Config.Bind("Debug", "SpawnSpecies", DebugSpawnSpecies.FinWhale,
            "Species selected for debug spawning.");
        spawnOrigin = plugin.Config.Bind("Debug", "SpawnOrigin", DebugSpawnOrigin.ShipRigidbody,
            "Spawn around the current ship's Rigidbody or the player transform.");
        spawnCount = plugin.Config.Bind("Debug", "SpawnCount", 1,
            new ConfigDescription("Individual animals per spawn.",
                new AcceptableValueRange<int>(1, 30)));
        spawnRadius = plugin.Config.Bind("Debug", "SpawnRadius", 50f,
            new ConfigDescription("Maximum spawn radius in meters.",
                new AcceptableValueRange<float>(15f, 1000f)));
        animationTargetRadius = plugin.Config.Bind("Debug", "AnimationTargetRadius", 200f,
            new ConfigDescription("Range for the nearest debug animation target.",
                new AcceptableValueRange<float>(20f, 1000f)));
        showMarkers = plugin.Config.Bind("Debug", "ShowWakeMarkers", true,
            "Show grey/yellow/green/blue spheres over Orcas.");
        showWakeTargets = plugin.Config.Bind("Debug", "ShowWakeTarget", false,
            "Show the calculated magenta wake-corridor target per pod.");
        plugin.DebugAnimalSpawned += RegisterDebugAnimal;
    }

    private void RegisterDebugAnimal(MarineAnimalAI animal)
    {
        if (animal != null) spawned.Add(animal);
    }

    private static string PrefabName(DebugSpawnSpecies choice)
    {
        switch (choice)
        {
            case DebugSpawnSpecies.FinWhale: return "FinWhalePrefab";
            case DebugSpawnSpecies.Orca: return "OrcaPrefab";
            case DebugSpawnSpecies.BottlenoseDolphin: return "BottlenoseDolphinPrefab";
            case DebugSpawnSpecies.BeakedWhale: return "BeakedWhalePrefab";
            case DebugSpawnSpecies.SpermWhale: return "SpermWhalePrefab";
            case DebugSpawnSpecies.WhaleShark: return "WhaleSharkPrefab";
            default: return "FinWhalePrefab";
        }
    }

    private void Update()
    {
        if (spawnKey.Value != KeyCode.None && Input.GetKeyDown(spawnKey.Value))
            ForceSpawn();
        if (surfaceKey.Value != KeyCode.None && Input.GetKeyDown(surfaceKey.Value))
            TriggerNearest(false);
        if (breachKey.Value != KeyCode.None && Input.GetKeyDown(breachKey.Value))
            TriggerNearest(true);
        if (Time.time >= nextMarkerUpdate)
        {
            nextMarkerUpdate = Time.time + .5f;
            RefreshMarkers();
        }
    }

    private void ForceSpawn()
    {
        Vector3 playerPosition;
        if (!plugin.TryGetDebugPlayerPosition(out playerPosition)) return;
        Vector3 center = playerPosition;
        ShipWakeData ship;
        if (spawnOrigin.Value == DebugSpawnOrigin.ShipRigidbody &&
            plugin.TryGetShipWake(out ship)) center = ship.Position;
        plugin.SpawnDebug(PrefabName(spawnSpecies.Value),
            spawnCount.Value, center, spawnRadius.Value);
    }

    private void TriggerNearest(bool breach)
    {
        Vector3 origin;
        if (!plugin.TryGetDebugPlayerPosition(out origin)) return;
        spawned.RemoveAll(a => a == null);
        // Sailwind's tagged player transform can be hundreds of meters from
        // the current ship pivot. Use the same origin as the debug spawner.
        ShipWakeData currentShip;
        if (spawnOrigin.Value == DebugSpawnOrigin.ShipRigidbody &&
            plugin.TryGetShipWake(out currentShip)) origin = currentShip.Position;
        MarineAnimalAI nearest = null;
        float closestSq = animationTargetRadius.Value * animationTargetRadius.Value;
        string selected = PrefabName(spawnSpecies.Value);
        foreach (MarineAnimalAI animal in spawned)
        {
            if (animal == null || animal.Species == null || animal.Species.PrefabName != selected) continue;
            Vector3 displacement = animal.transform.position - origin;
            displacement.y = 0f;
            if (displacement.sqrMagnitude >= closestSq) continue;
            closestSq = displacement.sqrMagnitude;
            nearest = animal;
        }
        if (nearest != null && !nearest.TriggerDebugAnimation(breach))
            Debug.LogWarning("SeaLife Debug: missing " + (breach ? "Breach" : "Surface") + " trigger.");
    }

    private static Material GetMaterial(ref Material material, Color color)
    {
        if (material != null) return material;
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;
        material = new Material(shader);
        material.color = color;
        material.name = "SeaLife debug marker";
        return material;
    }

    private static GameObject MakeMarker(string name, float size, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = name;
        obj.layer = 2; // Ignore Raycast; never affect avoidance.
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; Destroy(collider); }
        obj.transform.localScale = Vector3.one * size;
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (material != null) renderer.sharedMaterial = material;
        }
        return obj;
    }

    private void RefreshMarkers()
    {
        if (!showMarkers.Value) { ClearMarkers(); return; }
        ShipWakeData ship;
        bool hasShip = plugin.TryGetShipWake(out ship);
        var active = new HashSet<MarineAnimalAI>();
        var visiblePods = new HashSet<MarinePod>();
        foreach (MarineAnimalAI animal in MarineAnimalAI.ActiveAnimals)
        {
            if (animal == null || animal.Species == null ||
                !animal.Species.OrcaAppearanceAndAnimations || animal.Pod == null) continue;
            MarinePod pod = animal.Pod;
            active.Add(animal);
            visiblePods.Add(pod);
            string phase = pod.DebugWakeState;
            bool riding = hasShip && pod.DebugIsRidingHere(ship, animal);
            Material tint = riding ? GetMaterial(ref ridingMaterial, new Color(.12f, 1f, .3f)) :
                phase == "Approaching" || phase == "Riding" ?
                    GetMaterial(ref approachingMaterial, new Color(1f, .78f, .12f)) :
                phase == "Cooldown" ? GetMaterial(ref cooldownMaterial, new Color(.25f, .65f, 1f)) :
                    GetMaterial(ref wanderingMaterial, new Color(.65f, .65f, .65f));
            GameObject marker;
            if (!markers.TryGetValue(animal, out marker) || marker == null)
            {
                marker = MakeMarker("SeaLife Debug: " + phase, 2f, tint);
                markers[animal] = marker;
            }
            marker.transform.position = animal.transform.position + Vector3.up * 8f;
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null && tint != null && renderer.sharedMaterial != tint)
                renderer.sharedMaterial = tint;
        }
        var stale = new List<MarineAnimalAI>();
        foreach (var item in markers)
            if (!active.Contains(item.Key))
            {
                if (item.Value != null) Destroy(item.Value);
                stale.Add(item.Key);
            }
        foreach (MarineAnimalAI animal in stale) markers.Remove(animal);

        var displayedTargets = new HashSet<MarinePod>();
        if (hasShip && showWakeTargets.Value)
            foreach (MarinePod pod in visiblePods)
            {
                Vector3 target;
                if (pod == null || !pod.DebugTryGetWakeTarget(ship, out target)) continue;
                Vector3 offset = target - ship.Position;
                offset.y = 0f;
                if (offset.magnitude > plugin.WakeAttractionRadius * 1.5f) continue;
                displayedTargets.Add(pod);
                GameObject marker;
                if (!targets.TryGetValue(pod, out marker) || marker == null)
                {
                    marker = MakeMarker("SeaLife Debug: Bow wake target", 2.4f,
                        GetMaterial(ref targetMaterial, new Color(1f, .1f, 1f)));
                    targets[pod] = marker;
                }
                marker.transform.position = target + Vector3.up * 7f;
            }
        var oldTargets = new List<MarinePod>();
        foreach (var item in targets)
            if (!displayedTargets.Contains(item.Key))
            {
                if (item.Value != null) Destroy(item.Value);
                oldTargets.Add(item.Key);
            }
        foreach (MarinePod pod in oldTargets) targets.Remove(pod);
    }

    private void ClearMarkers()
    {
        foreach (GameObject marker in markers.Values)
            if (marker != null) Destroy(marker);
        foreach (GameObject marker in targets.Values)
            if (marker != null) Destroy(marker);
        markers.Clear();
        targets.Clear();
    }

    private void OnDestroy()
    {
        if (plugin != null) plugin.DebugAnimalSpawned -= RegisterDebugAnimal;
        ClearMarkers();
    }
}
