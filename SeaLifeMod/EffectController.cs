using System.Collections;
using System.IO;
using UnityEngine;

public class EffectController : MonoBehaviour
{
    private ParticleSystem blowholeParticleSystem;
    private ParticleSystem breachSplashParticleSystem;
    private ParticleSystem breachEmergeParticleSystem;
    private ParticleSystem tailSplashParticleSystem;
    private AudioSource audioSource;
    private AudioClip[] blowholeSounds;
    private AudioClip[] breachSplashSounds;
    private AudioClip[] breachEmergeSounds;
    private AudioClip[] tailSplashSounds;

    void Start()
    {
        // Find and assign particle systems
        blowholeParticleSystem = FindDeepChild(transform, "WhaleBlowhole").GetComponent<ParticleSystem>();
        breachSplashParticleSystem = FindDeepChild(transform, "WhaleBreachSplash").GetComponent<ParticleSystem>();
        breachEmergeParticleSystem = FindDeepChild(transform, "WhaleBreachEmerge").GetComponent<ParticleSystem>();
        tailSplashParticleSystem = FindDeepChild(transform, "WhaleTailSplash").GetComponent<ParticleSystem>();

        // Add and configure audio source
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.volume = 1.0f;
        audioSource.spatialBlend = 1.0f;
        audioSource.minDistance = 10f;
        audioSource.maxDistance = 250f;

        // Load all sounds
        LoadSounds();
    }

    public void PlayBlowholeEffect()
    {
        if (blowholeParticleSystem != null)
        {
            blowholeParticleSystem.Play();
            PlaySound(blowholeSounds);
        }
    }

    public void PlayBreachSplashEffect()
    {
        if (breachSplashParticleSystem != null)
        {
            breachSplashParticleSystem.Play();
            PlaySound(breachSplashSounds);
        }
    }

    public void PlayBreachEmergeEffect()
    {
        if (breachEmergeParticleSystem != null)
        {
            breachEmergeParticleSystem.Play();
            PlaySound(breachEmergeSounds);
        }
    }

    public void PlayTailSplashEffect()
    {
        if (tailSplashParticleSystem != null)
        {
            tailSplashParticleSystem.Play();
            PlaySound(tailSplashSounds);
        }
    }

    private void PlaySound(AudioClip[] soundArray)
    {
        if (soundArray.Length > 0)
        {
            int randomIndex = Random.Range(0, soundArray.Length);
            audioSource.PlayOneShot(soundArray[randomIndex]);
        }
    }

    private void LoadSounds()
    {
        blowholeSounds = LoadAudioClips("WhaleBlowMed", 6);
        breachSplashSounds = LoadAudioClips("BreachSplashLarge", 5);
        breachEmergeSounds = LoadAudioClips("BreachSplashSmall", 6);
        tailSplashSounds = LoadAudioClips("TailSplash", 4);
    }

    private AudioClip[] LoadAudioClips(string baseName, int count)
    {
        AudioClip[] clips = new AudioClip[count];
        for (int i = 0; i < count; i++)
        {
            clips[i] = LoadAudioClipFromBundle($"{baseName}{i + 1:00}");
        }
        return clips;
    }

    private AudioClip LoadAudioClipFromBundle(string clipName)
    {
        AudioClip clip = null;
        try
        {
            SeaLifePlugin plugin = FindObjectOfType<SeaLifePlugin>();
            if (plugin != null)
            {
                clip = plugin.seaLifeBundle.LoadAsset<AudioClip>($"Assets/Audio/{clipName}.wav");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load audio clip from bundle: {clipName}. Error: {e.Message}");
        }
        return clip;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
