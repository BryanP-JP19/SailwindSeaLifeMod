using BepInEx;
using BepInEx.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[BepInPlugin("com.yourname.sailwind.sealifeplugin", "Sealife Plugin", "0.1.20")]
public sealed class SeaLifePlugin : BaseUnityPlugin
{
    public static SeaLifePlugin Instance { get; private set; }
    public AssetBundle seaLifeBundle;

    // Random Encounters 2.0.1 reads this exact private field through Harmony.
    // Keep it as an alias to the same FinWhalePrefab used by our new spawner.
    // Do not rename or remove: the other mod cannot see MarineSpecies.Prefab.
    private GameObject animalPrefab;

    private MarineSpecies[] species;
    private GameObject player;
    private Transform shiftingWorld;
    private readonly List<GameObject> activeAnimals = new List<GameObject>();
    private readonly List<GameObject> naturalAnimals = new List<GameObject>();
    private readonly ShipWakeTracker shipWakeTracker = new ShipWakeTracker();
    private ConfigEntry<int> maxNaturalAnimals;
    private ConfigEntry<float> minSpawnDelay;
    private ConfigEntry<float> maxSpawnDelay;
    // Config changes can occur while a spawn countdown is already in progress.
    // The event handler only sets this flag; the coroutine resets on Unity's thread.
    private volatile bool spawnTimerResetRequested;

    // Release defaults live here, not in the player-facing configuration UI.
    // To re-enable the optional tools, uncomment ONE line in Awake below.
    private const bool ValidateWaterDepth = true;
    private const int SpawnPlacementAttempts = 8;
    private const bool UseTriggerColliders = true;
    private const bool RefreshStandardShaders = true;
    private const bool RefreshParticleShaders = false;

    // Internal wake-riding settings. Tuning can be revisited after release.
    public bool WakeRidingEnabled { get { return true; } }
    public float WakeEnterKnots { get { return 8f; } }
    public float WakeExitKnots { get { return 6f; } }
    public float WakeMinDurationSeconds { get { return 60f; } }
    public float WakeMaxDurationSeconds { get { return 300f; } }
    public float WakeMinCooldownSeconds { get { return 30f; } }
    public float WakeMaxCooldownSeconds { get { return 300f; } }
    public float WakeAttractionRadius { get { return 400f; } }
    public float WakeInterestChance { get { return .40f; } }
    public float WakeApproachTimeoutSeconds { get { return 180f; } }
    public float WakeApproachExtraMinKnots { get { return 5f; } }
    public float WakeApproachExtraMaxKnots { get { return 10f; } }
    public bool TryGetShipWake(out ShipWakeData data) { return shipWakeTracker.TryGet(out data); }

    // Optional debug module subscribes here; release has no key hooks or debug config.
    internal System.Action<MarineAnimalAI> DebugAnimalSpawned;

    private void Awake()
    {
        Instance = this;
        maxNaturalAnimals = Config.Bind("Spawning", "MaxNaturalAnimals", 35,
            "Maximum naturally spawned animals. Does not include developer spawns.");
        minSpawnDelay = Config.Bind("Spawning", "MinSpawnDelaySeconds", 300f,
            "Minimum time between wildlife encounter attempts (seconds).");
        maxSpawnDelay = Config.Bind("Spawning", "MaxSpawnDelaySeconds", 900f,
            "Maximum time between wildlife encounter attempts (seconds).");

        // Changing either delay while playing starts a NEW countdown in that range.
        minSpawnDelay.SettingChanged += OnSpawnDelaySettingChanged;
        maxSpawnDelay.SettingChanged += OnSpawnDelaySettingChanged;

        species = MarineSpecies.Defaults();
        LoadSeaLifeAssetBundle();
        StartCoroutine(SpawnSeaLife());

        // DEVELOPER TOOLS: uncomment only the following line for local test builds.
        // gameObject.AddComponent<SeaLifeDebugTools>();

        Logger.LogInfo("SeaLife loaded.");
    }

    private void OnSpawnDelaySettingChanged(object sender, System.EventArgs e)
    {
        // Config may be edited from the F1 menu or reloaded from disk.
        // Do not call Unity coroutine APIs from the config change callback.
        spawnTimerResetRequested = true;
    }

    private void OnDestroy()
    {
        if (minSpawnDelay != null)
            minSpawnDelay.SettingChanged -= OnSpawnDelaySettingChanged;
        if (maxSpawnDelay != null)
            maxSpawnDelay.SettingChanged -= OnSpawnDelaySettingChanged;
        if (Instance == this) Instance = null;
    }

    // The optional debug module calls these; no debug configuration or input
    // polling is present in the release plugin.
    internal bool TryGetDebugPlayerPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (!FindPlayerObject()) return false;
        position = player.transform.position;
        return true;
    }

    internal void SpawnDebug(string prefabName, int count, Vector3 origin, float radius)
    {
        if (!FindPlayerObject())
        {
            Logger.LogWarning("[Debug] Cannot spawn: player not found.");
            return;
        }
        MarineSpecies selected = null;
        foreach (MarineSpecies entry in species)
            if (entry.PrefabName == prefabName) { selected = entry; break; }
        if (selected == null || selected.Prefab == null)
        {
            Logger.LogWarning("[Debug] Cannot spawn: prefab unavailable: " + prefabName);
            return;
        }
        radius = Mathf.Clamp(radius, 15f, 1000f);
        count = Mathf.Clamp(count, 1, 30);
        Vector3 center = RandomAnnulus(origin, radius * .4f, radius * .55f);
        SpawnEncounter(selected, center, count, true, origin, radius);
    }

    private void LoadSeaLifeAssetBundle()
    {
        string bundlePath = Path.Combine(Paths.PluginPath, "SeaLifeMod/assets");
        if (!File.Exists(bundlePath)) { Logger.LogError("AssetBundle not found: " + bundlePath); return; }
        seaLifeBundle = AssetBundle.LoadFromFile(bundlePath);
        if (seaLifeBundle == null) { Logger.LogError("Failed to load AssetBundle: " + bundlePath); return; }
        foreach (MarineSpecies entry in species)
        {
            entry.Prefab = seaLifeBundle.LoadAsset<GameObject>(entry.PrefabName);
            if (entry.PrefabName == "FinWhalePrefab") animalPrefab = entry.Prefab;
            if (entry.Prefab == null)
            {
                continue;
            }
            // Rebuild only opaque Standard materials for fog compatibility.
            // Particle materials use the tested Fade/Soft Particles configuration.
            MaterialManager.Refresh(entry.Prefab, null,
                RefreshStandardShaders, RefreshParticleShaders);
        }
    }

    // The live F1 values are sampled whenever a fresh countdown begins.
    // An in-progress countdown is discarded if either delay changes.
    private float RollSpawnDelay()
    {
        float low = Mathf.Max(1f, minSpawnDelay.Value);
        float high = Mathf.Max(low, maxSpawnDelay.Value);
        return Random.Range(low, high);
    }

    private IEnumerator SpawnSeaLife()
    {
        float secondsUntilNextAttempt = RollSpawnDelay();
        while (true)
        {
            if (spawnTimerResetRequested)
            {
                spawnTimerResetRequested = false;
                secondsUntilNextAttempt = RollSpawnDelay();
            }

            // Preserve WaitForSeconds' scaled game-time behavior, but poll once
            // per frame so live config changes reset the timer immediately.
            secondsUntilNextAttempt -= Time.deltaTime;
            if (secondsUntilNextAttempt <= 0f)
            {
                TrySpawnNaturalEncounter();
                secondsUntilNextAttempt = RollSpawnDelay();
            }
            yield return null;
        }
    }

    private void TrySpawnNaturalEncounter()
    {
        if (!FindPlayerObject()) return;
        CompactActiveLists();

        // The cap is deliberately read on EVERY attempt, so changing it in F1
        // affects natural spawning immediately without a restart or despawns.
        // Developer-spawned animals are excluded from this count, as before.
        int limit = Mathf.Max(0, maxNaturalAnimals.Value);
        if (naturalAnimals.Count >= limit) return;

        MarineSpecies chosen = ChooseSpecies();
        if (chosen == null) return;
        int requestedGroupSize = Random.Range(
            Mathf.Max(1, chosen.MinGroup),
            Mathf.Max(chosen.MinGroup, chosen.MaxGroup) + 1);
        int remaining = limit - naturalAnimals.Count;
        int count = Mathf.Min(requestedGroupSize, remaining);
        if (count < 1) return;

        Vector3 center = RandomAnnulus(player.transform.position,
            chosen.MinSpawnRadius, chosen.MaxSpawnRadius);
        SpawnEncounter(chosen, center, count, false);
    }

    private MarineSpecies ChooseSpecies()
    {
        int total = 0;
        foreach (MarineSpecies s in species) if (s.Prefab != null && s.Weight > 0) total += s.Weight;
        if (total <= 0) return null;
        int roll = Random.Range(0, total);
        foreach (MarineSpecies s in species)
        {
            if (s.Prefab == null || s.Weight <= 0) continue;
            roll -= s.Weight;
            if (roll < 0) return s;
        }
        return null;
    }

    private void SpawnEncounter(MarineSpecies s, Vector3 center, int count, bool debug)
    {
        SpawnEncounter(s, center, count, debug, Vector3.zero, 0f);
    }

    private void SpawnEncounter(MarineSpecies s, Vector3 center, int count, bool debug,
        Vector3 debugOrigin, float debugRadius)
    {
        if (s.Prefab == null || player == null) return;
        if (s.ControllerType == null || !typeof(MarineAnimalAI).IsAssignableFrom(s.ControllerType))
        {
            Logger.LogError("Invalid animal controller for " + s.Name);
            return;
        }
        if (shiftingWorld == null)
        {
            GameObject world = GameObject.Find("_shifting world");
            if (world != null) shiftingWorld = world.transform;
        }
        Vector3 heading = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
        List<Vector3> spawnPositions;
        string rejection;
        bool fullGroup = SelectSpawnPositions(s, center, count, debug,
            debugOrigin, debugRadius, heading, out spawnPositions, out rejection);
        if (spawnPositions.Count == 0)
        {
            // No suitable water at the selected location is ordinary, not an error.
            if (debug)
                Logger.LogWarning("[Debug] No safe water found for " + s.Name + ": " + rejection);
            return;
        }
        // Create a pod ONLY after at least one viable animal position exists.
        // Independent pod object is deliberately NOT parented under the wildlife.
        GameObject podObject = new GameObject("SeaLife Pod - " + s.Name);
        if (shiftingWorld != null) podObject.transform.SetParent(shiftingWorld, false);
        MarinePod pod = podObject.AddComponent<MarinePod>();
        pod.Initialize(heading, s);
        int actualCount = spawnPositions.Count;
        for (int i = 0; i < actualCount; i++)
        {
            Vector3 position = spawnPositions[i];
            // Spawn at the normal water-following depth, not the old arbitrary
            // Y=-10. A shallow-but-valid location may have ground above -10.
            float seaLevel = Crest.OceanRenderer.Instance != null
                ? Crest.OceanRenderer.Instance.SeaLevel : 0f;
            position.y = seaLevel + s.SurfaceOffset;
            Quaternion rotation = Quaternion.LookRotation(heading);
            GameObject animal = Instantiate(s.Prefab, position, rotation);
            animal.name = s.Name + (debug ? " [debug]" : "");
            if (shiftingWorld != null) animal.transform.SetParent(shiftingWorld, true);
            // Use triggers to prevent rigidbody contact impulses. Navigation still
            // uses world colliders for non-penetration; collision exclusions must be
            // verified against the actual Sailwind damage implementation.
            if (UseTriggerColliders)
            {
                Collider[] colliders = animal.GetComponentsInChildren<Collider>(true);
                foreach (Collider c in colliders) c.isTrigger = true;
            }
            // Animation Events are delivered to the GameObject that owns the
            // Animator. Attach the effect receiver there (not necessarily at
            // the prefab root), but search the entire animal for particles.
            // This works with both Fin Whale and Orca animation clips.
            if (s.HasFinWhaleAnimations || s.OrcaAppearanceAndAnimations)
            {
                Animator actorAnimator = animal.GetComponentInChildren<Animator>(true);
                GameObject eventReceiver = actorAnimator != null ? actorAnimator.gameObject : animal;
                EffectController effects = eventReceiver.GetComponent<EffectController>();
                if (effects == null)
                    effects = eventReceiver.AddComponent<EffectController>();
                effects.SetEffectRoot(animal.transform);
            }

            // Every species selects its controller in its own CreateDefinition().
            // No hardcoded species names in the shared spawning pipeline.
            MarineAnimalAI ai = animal.AddComponent(s.ControllerType) as MarineAnimalAI;
            ai.Initialize(s, player.transform, pod);
            pod.Members.Add(ai);
            activeAnimals.Add(animal);
            if (debug)
            {
                if (DebugAnimalSpawned != null) DebugAnimalSpawned(ai);
            }
            else naturalAnimals.Add(animal);
        }
        LogPopulation(s, actualCount, debug);
    }

    // Select an entire loose group over known-deep water before instantiating.
    // Re-rolling the group center keeps encounters cohesive if the first center
    // happens to land inside an island or too close to its shoreline.
    // Candidate limits prevent repeated key presses from causing long searches.
    private bool SelectSpawnPositions(MarineSpecies speciesEntry, Vector3 initialCenter,
        int count, bool debug, Vector3 debugOrigin, float debugRadius, Vector3 heading,
        out List<Vector3> best, out string lastRejection)
    {
        best = new List<Vector3>();
        lastRejection = "none";
        float spacing = Mathf.Max(2f, speciesEntry.GroupSpacing * .65f);
        int groupAttempts = Mathf.Clamp(SpawnPlacementAttempts, 1, 20);
        Vector3 naturalOrigin = player.transform.position;
        for (int groupTry = 0; groupTry < groupAttempts; groupTry++)
        {
            Vector3 candidateCenter = groupTry == 0 ? initialCenter :
                debug ? RandomAnnulus(debugOrigin, debugRadius * .3f, debugRadius * .55f) :
                RandomAnnulus(naturalOrigin, speciesEntry.MinSpawnRadius, speciesEntry.MaxSpawnRadius);
            if (ValidateWaterDepth &&
                !SpawnWaterValidator.IsSafe(speciesEntry, candidateCenter, heading, out lastRejection))
                continue;
            float spread = speciesEntry.GroupSpacing * Mathf.Sqrt(Mathf.Max(1, count)) * .6f;
            if (debug && debugRadius > 0f)
            {
                Vector3 toCenter = candidateCenter - debugOrigin;
                toCenter.y = 0f;
                spread = Mathf.Min(spread, Mathf.Max(0f, debugRadius - toCenter.magnitude - 1f));
            }
            var positions = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    Vector2 offset = count == 1 && attempt == 0
                        ? Vector2.zero : Random.insideUnitCircle * spread;
                    Vector3 candidate = candidateCenter + new Vector3(offset.x, 0f, offset.y);
                    if (debug)
                    {
                        Vector3 fromOrigin = candidate - debugOrigin;
                        fromOrigin.y = 0f;
                        if (fromOrigin.sqrMagnitude > debugRadius * debugRadius) continue;
                    }
                    bool tooClose = false;
                    for (int j = 0; j < positions.Count; j++)
                    {
                        Vector3 delta = candidate - positions[j];
                        delta.y = 0f;
                        if (delta.sqrMagnitude < spacing * spacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) { lastRejection = "insufficient pod spacing"; continue; }
                    if (ValidateWaterDepth &&
                        !SpawnWaterValidator.IsSafe(speciesEntry, candidate, heading, out lastRejection))
                        continue;
                    positions.Add(candidate);
                    placed = true;
                    break;
                }
                if (!placed) break;
            }
            if (positions.Count == count)
            {
                best = positions;
                return true;
            }
            if (positions.Count > best.Count) best = positions;
        }
        return false;
    }

    private static Vector3 RandomAnnulus(Vector3 center, float minDistance, float maxDistance)
    {
        float min = Mathf.Max(0f, minDistance);
        float max = Mathf.Max(min, maxDistance);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Mathf.Lerp(min * min, max * max, Random.value));
        return center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private void LogPopulation(MarineSpecies justSpawned, int number, bool debug)
    {
        CompactActiveLists();
        var counts = new Dictionary<MarineSpecies, int>();
        foreach (GameObject obj in activeAnimals)
        {
            if (obj == null) continue;
            MarineAnimalAI animal = obj.GetComponent<MarineAnimalAI>();
            if (animal == null || animal.Species == null) continue;
            int previous;
            counts.TryGetValue(animal.Species, out previous);
            counts[animal.Species] = previous + 1;
        }
        var summary = new System.Text.StringBuilder();
        int total = 0;
        foreach (MarineSpecies entry in species)
        {
            int count;
            if (!counts.TryGetValue(entry, out count) || count == 0) continue;
            if (summary.Length > 0) summary.Append(", ");
            summary.Append(entry.Name).Append("=").Append(count);
            total += count;
        }
        Logger.LogInfo("Spawned " + number + " " + justSpawned.Name +
            (debug ? " [debug]" : "") + ". Live: " + total +
            " (" + summary + ")");
    }

    private void CompactActiveLists()
    {
        activeAnimals.RemoveAll(a => a == null);
        naturalAnimals.RemoveAll(a => a == null);
    }

    private bool FindPlayerObject()
    {
        if (player != null && player.activeInHierarchy) return true;
        player = null;
        GameObject[] objects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in objects)
        {
            if (obj.CompareTag("PlayerController")) { player = obj; return true; }
        }
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = root.transform.Find("OVRPlayerController (controller)");
            if (found != null) { player = found.gameObject; return true; }
        }
        return false;
    }
}
