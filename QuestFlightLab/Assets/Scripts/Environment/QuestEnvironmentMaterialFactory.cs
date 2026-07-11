using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.Environment
{
    /// <summary>One bounded Quest-safe material system for ground and stable water surfaces.</summary>
    public static class QuestEnvironmentMaterialFactory
    {
        public const string GroundShaderName = "QuestFlightLab/KBDU Ground AntiTile";
        public const string WaterShaderName = "QuestFlightLab/KBDU Stable Water";
        public const string GroundShaderResourcePath = "QuestFlightLab/Environment/Shaders/KbduGroundAntiTile";
        public const string WaterShaderResourcePath = "QuestFlightLab/Environment/Shaders/KbduStableWater";
        public const string WitheredGrassResourcePath = "QuestFlightLab/Environment/GroundMaterials/withered_grass_diff_1k";
        public const string SparseGrassResourcePath = "QuestFlightLab/Environment/GroundMaterials/sparse_grass_diff_1k";
        public const string DrySoilResourcePath = "QuestFlightLab/Environment/GroundMaterials/dry_ground_01_diff_1k";
        public const int GroundTextureSampleBudget = 3;
        public const int GroundTextureResolution = 1024;
        public const float MicroMetersPerTile = 15f;
        public const float MacroVariationMeters = 220f;
        public const float DetailFadeStartMeters = 350f;
        public const float DetailFadeEndMeters = 2400f;

        private static Texture2D _witheredGrass;
        private static Texture2D _sparseGrass;
        private static Texture2D _drySoil;

        public static int LoadedGroundTextureCount
        {
            get
            {
                EnsureTexturesLoaded();
                int count = 0;
                if (_witheredGrass != null) count++;
                if (_sparseGrass != null) count++;
                if (_drySoil != null) count++;
                return count;
            }
        }

        public static Material CreateGroundMaterial(string name, string materialId, Color tint)
        {
            EnsureTexturesLoaded();
            Shader shader = Resources.Load<Shader>(GroundShaderResourcePath) ?? Shader.Find(GroundShaderName);
            if (shader == null)
            {
                Material fallback = new Material(Shader.Find("Standard"))
                {
                    name = name + "_GroundFallback",
                    color = tint,
                    mainTexture = _witheredGrass,
                    mainTextureScale = Vector2.one * 3f,
                    enableInstancing = true
                };
                fallback.SetFloat("_Glossiness", 0.08f);
                return fallback;
            }

            Material material = new Material(shader)
            {
                name = name,
                enableInstancing = true
            };
            material.SetTexture("_DryGrassTex", _witheredGrass);
            material.SetTexture("_GreenGrassTex", _sparseGrass);
            material.SetTexture("_SoilTex", _drySoil);
            material.SetTexture("_MainTex", _witheredGrass);
            material.SetColor("_Tint", tint);
            material.SetFloat("_MicroScale", 1f / MicroMetersPerTile);
            material.SetFloat("_MacroScale", 1f / MacroVariationMeters);
            material.SetFloat("_DetailFadeStart", DetailFadeStartMeters);
            material.SetFloat("_DetailFadeEnd", DetailFadeEndMeters);
            GetCategoryBlend(materialId, out float greenBlend, out float soilBlend, out float roughness);
            material.SetFloat("_GreenBlend", greenBlend);
            material.SetFloat("_SoilBlend", soilBlend);
            material.SetFloat("_Roughness", roughness);
            return material;
        }

        public static Material CreateStableWaterMaterial(string name, Color color)
        {
            Shader shader = Resources.Load<Shader>(WaterShaderResourcePath) ?? Shader.Find(WaterShaderName);
            if (shader == null)
            {
                Material fallback = new Material(Shader.Find("Standard"))
                {
                    name = name + "_OpaqueFallback",
                    color = new Color(color.r, color.g, color.b, 1f),
                    enableInstancing = true
                };
                fallback.SetFloat("_Glossiness", 0.32f);
                fallback.SetInt("_ZWrite", 1);
                fallback.SetOverrideTag("RenderType", "Opaque");
                fallback.renderQueue = (int)RenderQueue.Geometry + 5;
                return fallback;
            }

            Material material = new Material(shader)
            {
                name = name,
                enableInstancing = true,
                renderQueue = (int)RenderQueue.Geometry + 5
            };
            material.SetColor("_Color", new Color(color.r, color.g, color.b, 1f));
            material.SetFloat("_Roughness", 0.58f);
            material.SetFloat("_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            return material;
        }

        public static bool IsGroundMaterialId(string materialId)
        {
            switch (materialId)
            {
                case "dry_prairie":
                case "irrigated_field":
                case "harvested_field":
                case "orchard":
                case "meadow":
                case "forest":
                case "quarry":
                case "industrial_ground":
                case "airfield_turf":
                case "terrain_mid":
                case "terrain_far":
                case "water_bank":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsStableWaterMaterial(Material material)
        {
            return material != null && material.shader != null &&
                   string.Equals(material.shader.name, WaterShaderName, StringComparison.Ordinal);
        }

        public static bool IsGroundMaterial(Material material)
        {
            return material != null && material.shader != null &&
                   string.Equals(material.shader.name, GroundShaderName, StringComparison.Ordinal);
        }

        public static void ApplyDeterministicBatchVariation(
            Renderer renderer,
            string stableKey,
            bool preserveGlobalContinuity)
        {
            if (renderer == null || !IsGroundMaterial(renderer.sharedMaterial)) return;
            Vector4 transform = new Vector4(1f, 0f, 0f, 0f);
            if (!preserveGlobalContinuity)
            {
                uint hash = StableHash(stableKey ?? string.Empty);
                int quarterTurns = (int)(hash & 3u);
                float angle = quarterTurns * Mathf.PI * 0.5f;
                transform.x = Mathf.Cos(angle);
                transform.y = Mathf.Sin(angle);
                transform.z = ((hash >> 8) & 0xFFu) / 255f * 47.13f;
                transform.w = ((hash >> 16) & 0xFFu) / 255f * 31.79f;
            }
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetVector("_BatchUvTransform", transform);
            renderer.SetPropertyBlock(properties);
        }

        public static Vector2 WorldUv(Vector3 worldPosition, int textureVariant)
        {
            float scale = 1f / MicroMetersPerTile;
            switch (textureVariant)
            {
                case 1: return new Vector2(-worldPosition.z, worldPosition.x) * (scale * 0.83f) + new Vector2(17.31f, 9.17f);
                case 2: return new Vector2(worldPosition.x * 0.819f + worldPosition.z * 0.574f,
                                           -worldPosition.x * 0.574f + worldPosition.z * 0.819f) * (scale * 1.13f) + new Vector2(31.73f, 4.91f);
                default: return new Vector2(worldPosition.x, worldPosition.z) * scale;
            }
        }

        private static void EnsureTexturesLoaded()
        {
            if (_witheredGrass == null) _witheredGrass = Resources.Load<Texture2D>(WitheredGrassResourcePath);
            if (_sparseGrass == null) _sparseGrass = Resources.Load<Texture2D>(SparseGrassResourcePath);
            if (_drySoil == null) _drySoil = Resources.Load<Texture2D>(DrySoilResourcePath);
        }

        private static void GetCategoryBlend(string materialId, out float greenBlend, out float soilBlend, out float roughness)
        {
            greenBlend = 0.16f;
            soilBlend = 0.22f;
            roughness = 0.82f;
            switch (materialId)
            {
                case "irrigated_field": greenBlend = 0.78f; soilBlend = 0.08f; roughness = 0.76f; break;
                case "harvested_field": greenBlend = 0.04f; soilBlend = 0.52f; roughness = 0.86f; break;
                case "orchard": greenBlend = 0.62f; soilBlend = 0.15f; roughness = 0.8f; break;
                case "meadow": greenBlend = 0.52f; soilBlend = 0.1f; roughness = 0.79f; break;
                case "forest": greenBlend = 0.48f; soilBlend = 0.3f; roughness = 0.88f; break;
                case "quarry": greenBlend = 0f; soilBlend = 0.92f; roughness = 0.9f; break;
                case "industrial_ground": greenBlend = 0.03f; soilBlend = 0.78f; roughness = 0.87f; break;
                case "airfield_turf": greenBlend = 0.36f; soilBlend = 0.12f; roughness = 0.79f; break;
                case "terrain_mid": greenBlend = 0.12f; soilBlend = 0.34f; roughness = 0.9f; break;
                case "terrain_far": greenBlend = 0.08f; soilBlend = 0.42f; roughness = 0.93f; break;
                case "water_bank": greenBlend = 0.06f; soilBlend = 0.84f; roughness = 0.92f; break;
            }
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }
                return hash;
            }
        }
    }
}
