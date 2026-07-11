using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.Runtime
{
    [Serializable]
    public sealed class CockpitLightingReport
    {
        public int rendererCount;
        public int reflectionAwareRendererCount;
        public int realtimeShadowCasterCountDisabled;
        public int realtimeShadowReceiverCountDisabled;
        public int rendererLightmapBindingCountCleared;
        public int opaqueMaterialCloneCount;
        public int occlusionMaterialCountNeutralized;
        public int occlusionTextureSlotCountCleared;
        public int occlusionStrengthCountZeroed;
        public int lightmapLikeTextureSlotCountCleared;
        public int coarseNormalMaterialCountNeutralized;
        public int coarseNormalTextureSlotCountCleared;
        public int remainingRealtimeShadowCasterCount;
        public int remainingRealtimeShadowReceiverCount;
        public int remainingActiveOcclusionMaterialCount;

        // Retained for report compatibility with earlier evidence readers. The new
        // cockpit-safe policy deliberately leaves the active caster/receiver values
        // at zero; the disabled counters classify work performed on the first pass.
        public int stableMajorShadowCasterCount;
        public int glassShadowCasterCountDisabled;
        public int microDetailShadowCasterCountDisabled;
        public int shadowReceiverCount;
        public int glassMaterialCloneCount;
        public string strategy;
    }

    /// <summary>
    /// Applies a stable, mobile-safe lighting policy to the imported cockpit.
    ///
    /// Quest's shadow atlas is intentionally not sampled by first-person cockpit
    /// geometry. At this viewing distance its texels and cascade transitions are
    /// visible as coarse moving patches. Direct diffuse/specular lighting, the
    /// authored base colour, and the static sky reflection remain enabled. Authored
    /// AO/lightmap inputs are also neutralized because this particular source model
    /// packs broad baked darkness into low-density atlases on the interior shell.
    /// </summary>
    public static class QuestCockpitLightingPolicy
    {
        public const float GlassSmoothness = 0.78f;
        public const float MaximumGlassAlpha = 0.22f;

        private const string StableGlassSuffix = " Quest Stable Glass";
        private const string StableOpaqueSuffix = " Quest Stable Opaque";

        private static readonly string[] OcclusionTextureProperties =
        {
            "occlusionTexture", "_OcclusionMap", "_OcclusionTexture", "_AOMap"
        };

        private static readonly string[] OcclusionStrengthProperties =
        {
            "occlusionTexture_strength", "_OcclusionStrength", "_AOIntensity"
        };

        private static readonly string[] LightmapLikeTextureProperties =
        {
            "_LightMap", "_Lightmap", "lightmapTexture", "_BakedLightMap",
            "_ShadowMap", "_ShadowTex"
        };

        private static readonly string[] NormalTextureProperties =
        {
            "normalTexture", "_BumpMap", "_NormalMap"
        };

        private static readonly string[] CoarseNormalMaterialTokens =
        {
            // This 1024px source atlas covers the entire cabin shell and panel. At
            // seated distance its normal texels visibly crawl under direct light.
            "Interior_Body_MAT"
        };

        private static readonly string[] MicroDetailTokens =
        {
            "button", "switch", "paddle", "seatbelt", "seatgear", "meter",
            "static_txt", "static_display"
        };

        public static CockpitLightingReport ConfigureImportedAircraft(GameObject root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            CockpitLightingReport report = new CockpitLightingReport
            {
                strategy = "no_cockpit_shadow_map_plus_neutral_ao_and_static_sky_reflections"
            };
            Dictionary<Material, Material> glassClones = new Dictionary<Material, Material>();
            Dictionary<Material, Material> opaqueClones = new Dictionary<Material, Material>();

            HashSet<Material> remainingActiveOcclusion = new HashSet<Material>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                report.rendererCount++;

                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
                report.reflectionAwareRendererCount++;

                bool realtimeCasterDisabled = false;
                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    report.realtimeShadowCasterCountDisabled++;
                    realtimeCasterDisabled = true;
                }

                if (renderer.receiveShadows)
                {
                    renderer.receiveShadows = false;
                    report.realtimeShadowReceiverCountDisabled++;
                }

                if (IsRealLightmapIndex(renderer.lightmapIndex) ||
                    IsRealLightmapIndex(renderer.realtimeLightmapIndex))
                {
                    renderer.lightmapIndex = -1;
                    renderer.realtimeLightmapIndex = -1;
                    report.rendererLightmapBindingCountCleared++;
                }

                Material[] materials = renderer.sharedMaterials;
                bool containsGlass = false;
                bool materialArrayChanged = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;

                    if (IsGlassMaterial(source))
                    {
                        containsGlass = true;
                        if (source.name.EndsWith(StableGlassSuffix, StringComparison.Ordinal))
                        {
                            ConfigureTransparentGlass(source);
                            continue;
                        }

                        if (!glassClones.TryGetValue(source, out Material glass))
                        {
                            glass = new Material(source)
                            {
                                name = CleanMaterialName(source.name) + StableGlassSuffix
                            };
                            ConfigureTransparentGlass(glass);
                            glassClones.Add(source, glass);
                            report.glassMaterialCloneCount++;
                        }

                        materials[i] = glass;
                        materialArrayChanged = true;
                        continue;
                    }

                    if (source.name.EndsWith(StableOpaqueSuffix, StringComparison.Ordinal))
                    {
                        NeutralizeOpaqueLightingTextures(source, report);
                        continue;
                    }

                    if (!NeedsStableOpaqueClone(source)) continue;
                    if (!opaqueClones.TryGetValue(source, out Material opaque))
                    {
                        opaque = new Material(source)
                        {
                            name = CleanMaterialName(source.name) + StableOpaqueSuffix
                        };
                        NeutralizeOpaqueLightingTextures(opaque, report);
                        opaqueClones.Add(source, opaque);
                        report.opaqueMaterialCloneCount++;
                    }

                    materials[i] = opaque;
                    materialArrayChanged = true;
                }

                if (materialArrayChanged) renderer.sharedMaterials = materials;

                // Compatibility counters describe which old class of caster was
                // removed; no renderer is allowed to remain a caster or receiver.
                if (realtimeCasterDisabled && containsGlass) report.glassShadowCasterCountDisabled++;
                else if (realtimeCasterDisabled && IsMicroDetail(renderer))
                    report.microDetailShadowCasterCountDisabled++;
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                    report.remainingRealtimeShadowCasterCount++;
                if (renderer.receiveShadows) report.remainingRealtimeShadowReceiverCount++;

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && HasActiveOcclusion(material))
                        remainingActiveOcclusion.Add(material);
                }
            }
            report.remainingActiveOcclusionMaterialCount = remainingActiveOcclusion.Count;

            Debug.Log(
                $"[QuestFlightLab][CockpitLighting] renderers={report.rendererCount} " +
                $"castersDisabled={report.realtimeShadowCasterCountDisabled} " +
                $"receiversDisabled={report.realtimeShadowReceiverCountDisabled} " +
                $"aoMaterialsNeutralized={report.occlusionMaterialCountNeutralized} " +
                $"coarseNormalsNeutralized={report.coarseNormalMaterialCountNeutralized} " +
                $"remainingCasters={report.remainingRealtimeShadowCasterCount} " +
                $"remainingReceivers={report.remainingRealtimeShadowReceiverCount} " +
                $"remainingActiveAO={report.remainingActiveOcclusionMaterialCount} " +
                $"glassMaterials={report.glassMaterialCloneCount} strategy={report.strategy}");
            return report;
        }

        public static bool IsGlassMaterial(Material material)
        {
            return material != null &&
                   material.name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsMicroDetail(Renderer renderer)
        {
            if (renderer == null) return false;
            string identity = renderer.name;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null) identity += " " + material.name;
            }

            for (int i = 0; i < MicroDetailTokens.Length; i++)
            {
                if (identity.IndexOf(MicroDetailTokens[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        public static bool HasActiveOcclusion(Material material)
        {
            if (material == null) return false;
            if (material.IsKeywordEnabled("_OCCLUSION") ||
                material.IsKeywordEnabled("_OCCLUSIONMAP") ||
                material.IsKeywordEnabled("_OCCLUSION_TEXTURE") ||
                material.IsKeywordEnabled("_AO_MAP")) return true;

            // glTFast's glTF shader declares an always-present white default for
            // occlusionTexture. Its _OCCLUSION keyword above is the authoritative
            // indication that the source actually bound that channel. Texture-based
            // fallback detection is only needed for Standard/custom AO properties.
            for (int i = 1; i < OcclusionTextureProperties.Length; i++)
            {
                string textureProperty = OcclusionTextureProperties[i];
                if (!material.HasProperty(textureProperty) || material.GetTexture(textureProperty) == null)
                    continue;

                for (int strengthIndex = 1; strengthIndex < OcclusionStrengthProperties.Length; strengthIndex++)
                {
                    string strengthProperty = OcclusionStrengthProperties[strengthIndex];
                    if (material.HasProperty(strengthProperty) &&
                        material.GetFloat(strengthProperty) > 0.0001f) return true;
                }
            }

            return false;
        }

        private static bool NeedsStableOpaqueClone(Material material)
        {
            return HasActiveOcclusion(material) ||
                   HasAnyTexture(material, LightmapLikeTextureProperties) ||
                   ShouldNeutralizeCoarseNormal(material);
        }

        private static bool ShouldNeutralizeCoarseNormal(Material material)
        {
            if (material == null || !HasAnyTexture(material, NormalTextureProperties)) return false;
            for (int i = 0; i < CoarseNormalMaterialTokens.Length; i++)
            {
                if (material.name.IndexOf(CoarseNormalMaterialTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static void NeutralizeOpaqueLightingTextures(Material material, CockpitLightingReport report)
        {
            bool neutralizedOcclusion = HasActiveOcclusion(material);
            int clearedOcclusion = ClearTextureProperties(material, OcclusionTextureProperties);
            int zeroedStrengths = ZeroFloatProperties(material, OcclusionStrengthProperties);
            material.DisableKeyword("_OCCLUSION");
            material.DisableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_OCCLUSION_TEXTURE");
            material.DisableKeyword("_AO_MAP");

            if (neutralizedOcclusion || clearedOcclusion > 0 || zeroedStrengths > 0)
                report.occlusionMaterialCountNeutralized++;
            report.occlusionTextureSlotCountCleared += clearedOcclusion;
            report.occlusionStrengthCountZeroed += zeroedStrengths;
            report.lightmapLikeTextureSlotCountCleared +=
                ClearTextureProperties(material, LightmapLikeTextureProperties);

            if (ShouldNeutralizeCoarseNormal(material))
            {
                int clearedNormals = ClearTextureProperties(material, NormalTextureProperties);
                material.DisableKeyword("_NORMALMAP");
                if (clearedNormals > 0)
                {
                    report.coarseNormalMaterialCountNeutralized++;
                    report.coarseNormalTextureSlotCountCleared += clearedNormals;
                }
            }
        }

        private static int ClearTextureProperties(Material material, string[] propertyNames)
        {
            int cleared = 0;
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string property = propertyNames[i];
                if (!material.HasProperty(property) || material.GetTexture(property) == null) continue;
                material.SetTexture(property, null);
                cleared++;
            }

            return cleared;
        }

        private static int ZeroFloatProperties(Material material, string[] propertyNames)
        {
            int zeroed = 0;
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string property = propertyNames[i];
                if (!material.HasProperty(property) || Mathf.Approximately(material.GetFloat(property), 0f))
                    continue;
                material.SetFloat(property, 0f);
                zeroed++;
            }

            return zeroed;
        }

        private static bool HasAnyTexture(Material material, string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string property = propertyNames[i];
                if (material.HasProperty(property) && material.GetTexture(property) != null) return true;
            }

            return false;
        }

        private static string CleanMaterialName(string materialName)
        {
            return (materialName ?? string.Empty).Replace(" (Instance)", string.Empty);
        }

        private static bool IsRealLightmapIndex(int index)
        {
            // Unity reserves 0xFFFF for "no lightmap" and 0xFFFE for a renderer
            // whose scale/offset exists but whose lightmap is not loaded.
            return index >= 0 && index < 0xFFFE;
        }

        private static void ConfigureTransparentGlass(Material material)
        {
            Shader standard = Shader.Find("Standard");
            if (standard != null && material.shader != standard) material.shader = standard;

            Color color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color") ? material.GetColor("_Color") : material.color;
            float brightness = Mathf.Max(color.r, color.g, color.b);
            if (brightness < 0.08f) color = new Color(0.55f, 0.82f, 0.96f, color.a);
            color.a = Mathf.Min(color.a <= 0f ? 0.18f : color.a, MaximumGlassAlpha);

            material.color = color;
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", GlassSmoothness);
            if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 1f);
            if (material.HasProperty("_GlossyReflections")) material.SetFloat("_GlossyReflections", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
