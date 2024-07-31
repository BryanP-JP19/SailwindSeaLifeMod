using BepInEx;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[BepInPlugin("com.yourname.sailwind.sealifeplugin", "Sealife Plugin", "0.0.1")]
public class SeaLifePlugin : BaseUnityPlugin
{
    public AssetBundle seaLifeBundle;
    private GameObject animalPrefab;
    private GameObject player;
    private bool playerFound = false;

    void Awake()
    {
        LoadSeaLifeAssetBundle();
        Logger.LogInfo("Sealife Plugin is loaded!");

        StartCoroutine(SpawnSeaLife());
    }

/*    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            SpawnAnimalAtPlayerPosition();
        }
    }
*/
    private void LoadSeaLifeAssetBundle()
    {
        string bundlePath = Path.Combine(Paths.PluginPath, "SeaLifeMod/assets");
        seaLifeBundle = AssetBundle.LoadFromFile(bundlePath);
        if (seaLifeBundle == null)
        {
            Logger.LogError("Failed to load AssetBundle!");
            return;
        }
        animalPrefab = seaLifeBundle.LoadAsset<GameObject>("FinWhalePrefab");
    }

    private IEnumerator SpawnSeaLife()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(300, 900));
            SpawnAnimal(GetRandomSpawnPositionNearPlayer());
        }
    }

/*    private void SpawnAnimalAtPlayerPosition()
    {
        if (!FindPlayerObject()) return;
        Vector3 spawnPosition = player.transform.position + new Vector3(Random.Range(-500, 500), 0, Random.Range(-500, 500));
        SpawnAnimal(spawnPosition);
    }
*/
    private void SpawnAnimal(Vector3 spawnPosition)
    {
        spawnPosition.y = -10;
        Quaternion spawnRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        GameObject animal = Instantiate(animalPrefab, spawnPosition, spawnRotation);
        animal.AddComponent<FinWhaleAI>();
        animal.AddComponent<EffectController>();

        // Find the "_shifting world" object and set it as the parent
        GameObject shiftingWorld = GameObject.Find("_shifting world");
        if (shiftingWorld != null)
        {
            animal.transform.SetParent(shiftingWorld.transform);
        }
        else
        {
            Logger.LogWarning("_shifting world not found in the scene. Animal spawned without parent.");
        }
    }

    private Vector3 GetRandomSpawnPositionNearPlayer()
    {
        if (!FindPlayerObject()) return Vector3.zero;
        Vector3 playerPosition = player.transform.position;
        return playerPosition + new Vector3(Random.Range(-1000, 1000), 0, Random.Range(-1000, 1000));
    }

    private bool FindPlayerObject()
    {
        if (playerFound) return true;

        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.CompareTag("PlayerController"))
            {
                player = obj;
                playerFound = true;
                return true;
            }
        }

        // Fallback: Search by name in the scene hierarchy
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject rootObject in rootObjects)
        {
            Transform playerTransform = rootObject.transform.Find("OVRPlayerController (controller)");
            if (playerTransform != null)
            {
                player = playerTransform.gameObject;
                playerFound = true;
                return true;
            }
        }
        return false;
    }
}
