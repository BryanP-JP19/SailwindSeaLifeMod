using UnityEngine;

// Universal individual size variation used by every marine animal.
// Keep these values in one place, never on individual species definitions.
public static class MarineScale
{
    public const float MinimumRandomScale = 0.75f;
    public const float MaximumRandomScale = 1.50f;

    // Called once per animal during MarineAnimalAI.Initialize.
    public static float RollIndividualScale()
    {
        return Random.Range(MinimumRandomScale, MaximumRandomScale);
    }

    // Conservatively size spawn clearance for the biggest possible individual,
    // including any species-specific male anatomy/size modifier.
    public static float MaximumPossibleMultiplier(MarineSpecies species)
    {
        float sexMultiplier = species != null
            ? Mathf.Max(1f, species.MaleScaleMultiplier)
            : 1f;
        return MaximumRandomScale * sexMultiplier;
    }
}
