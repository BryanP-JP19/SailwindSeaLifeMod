using System.Collections.Generic;
using UnityEngine;

public sealed class EffectController : MonoBehaviour
{
    private Transform effectRoot;
    private bool initialized;
    private ParticleSystem blowholeParticleSystem;
    private ParticleSystem breachSplashParticleSystem;
    private ParticleSystem breachEmergeParticleSystem;
    private ParticleSystem tailSplashParticleSystem;
    private AudioSource audioSource;
    private AudioClip[] blowholeSounds = new AudioClip[0];
    private AudioClip[] breachSplashSounds = new AudioClip[0];
    private AudioClip[] breachEmergeSounds = new AudioClip[0];
    private AudioClip[] tailSplashSounds = new AudioClip[0];

    // The Animator can be on a child; its Animation Events reach this component.
    // The effect objects may be elsewhere in the same animal prefab.
    public void SetEffectRoot(Transform root)
    {
        effectRoot = root != null ? root : transform;
        EnsureInitialized();
    }

    private void Start()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        blowholeParticleSystem = FindEffect("WhaleBlowhole");
        breachSplashParticleSystem = FindEffect("WhaleBreachSplash");
        breachEmergeParticleSystem = FindEffect("WhaleBreachEmerge");
        tailSplashParticleSystem = FindEffect("WhaleTailSplash");
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.volume = 1f;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 10f;
        audioSource.maxDistance = 250f;
        LoadSounds();
    }

    public void PlayBlowholeEffect() { Play(blowholeParticleSystem, blowholeSounds); }
    public void PlayBreachSplashEffect() { Play(breachSplashParticleSystem, breachSplashSounds); }
    public void PlayBreachEmergeEffect() { Play(breachEmergeParticleSystem, breachEmergeSounds); }
    public void PlayTailSplashEffect() { Play(tailSplashParticleSystem, tailSplashSounds); }

    private void Play(ParticleSystem particles, AudioClip[] clips)
    {
        if (particles != null) particles.Play();
        if (audioSource != null && clips != null && clips.Length > 0)
            audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
    }

    private void LoadSounds()
    {
        SeaLifePlugin plugin = SeaLifePlugin.Instance;
        if (plugin == null || plugin.seaLifeBundle == null) return;
        blowholeSounds = LoadClips(plugin.seaLifeBundle, "WhaleBlowMed", 6);
        breachSplashSounds = LoadClips(plugin.seaLifeBundle, "BreachSplashLarge", 5);
        breachEmergeSounds = LoadClips(plugin.seaLifeBundle, "BreachSplashSmall", 6);
        tailSplashSounds = LoadClips(plugin.seaLifeBundle, "TailSplash", 4);
    }

    private static AudioClip[] LoadClips(AssetBundle bundle, string baseName, int count)
    {
        var clips = new List<AudioClip>();
        for (int i = 1; i <= count; i++)
        {
            string clipName = baseName + i.ToString("00");
            AudioClip clip = bundle.LoadAsset<AudioClip>("Assets/Audio/" + clipName + ".wav");
            if (clip != null) clips.Add(clip);
        }
        return clips.ToArray();
    }

    private ParticleSystem FindEffect(string name)
    {
        Transform root = effectRoot != null ? effectRoot : transform;
        ParticleSystem[] effects = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem effect in effects)
            if (effect.gameObject.name == name) return effect;
        return null;
    }
}
