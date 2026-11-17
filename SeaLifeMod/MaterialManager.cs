using System;
using System.Collections.Generic;
using UnityEngine;

// Repair AssetBundle shader references once per species prefab.
// Standard mesh material repair remains enabled (the proven v0.1.14 path).
// Particle repair is opt-in: restore render queue, shader keywords, and the
// original particle-specific blending properties so splashes keep their look.
// Unity or Sailwind may still use fog variants this cannot restore: test in-game.
public static class MaterialManager
{
    public static int Refresh(GameObject prefab, Action<string> log, bool refreshStandard, bool refreshParticles)
    {
        var cached = new Dictionary<Material, Material>();
        int repaired = 0;
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] originals = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < originals.Length; i++)
            {
                Material source = originals[i];
                if (source == null || source.shader == null) continue;

                Material replacement;
                if (!cached.TryGetValue(source, out replacement))
                {
                    string shaderName = source.shader.name;
                    bool isStandard = shaderName == "Standard";
                    bool isParticle = shaderName == "Particles/Standard Surface";
                    if (!(refreshStandard && isStandard) && !(refreshParticles && isParticle))
                    {
                        cached.Add(source, source);
                        continue;
                    }

                    Shader runtimeShader = Shader.Find(shaderName);
                    if (runtimeShader == null)
                    {
                        if (log != null) log("Shader not found at runtime: " + shaderName);
                        cached.Add(source, source);
                        continue;
                    }

                    // Source material may use Fade, Transparent, Additive, soft
                    // particles, camera fade, etc. Save state before shader swap.
                    int renderQueue = source.renderQueue;
                    string[] keywords = isParticle ? (string[])source.shaderKeywords.Clone() : new string[0];
                    string renderType = isParticle ? source.GetTag("RenderType", false, "") : "";

                    replacement = new Material(source);
                    replacement.shader = runtimeShader;
                    // Leave the proven opaque Standard refresh unchanged.
                    // Only the experimental particle branch restores extra
                    // render state after a shader reassignment.
                    if (isParticle)
                    {
                        replacement.shaderKeywords = keywords;
                        replacement.renderQueue = renderQueue;
                        if (!string.IsNullOrEmpty(renderType))
                            replacement.SetOverrideTag("RenderType", renderType);
                    }
                    replacement.name = source.name + " (SeaLife runtime)";

                    cached.Add(source, replacement);
                    repaired++;
                    if (log != null)
                    {
                        log("[Fog] " + source.name + " shader=" + shaderName +
                            " queue=" + renderQueue + " -> " + replacement.renderQueue +
                            " keywords=[" + string.Join(",", keywords) + "]" +
                            (isParticle ? " particleRefresh=ON" : ""));
                        if (isParticle && source.HasProperty("_SrcBlend") && source.HasProperty("_DstBlend"))
                            log("[Fog] particle blend src=" + source.GetFloat("_SrcBlend") +
                                " dst=" + source.GetFloat("_DstBlend") +
                                " zwrite=" + (source.HasProperty("_ZWrite") ? source.GetFloat("_ZWrite").ToString() : "unknown"));
                    }
                }
                else if (replacement == source) continue;

                originals[i] = replacement;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = originals;
        }
        return repaired;
    }
}
