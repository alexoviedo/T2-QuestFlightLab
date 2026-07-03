using System;
using System.IO;
using System.Reflection;
using GaussianSplatting.Runtime;
using QuestFlightLab.Environment;
using UnityEditor;
using UnityEngine;

namespace QuestFlightLab.Experimental.Splats.Editor
{
    public static class QuestSplatRuntimeAssetBuilder
    {
        private const string RuntimeSampleFolder = "Assets/Resources/QuestFlightLab/Splats/Samples";
        private const string ConfigPath = "Assets/Resources/QuestFlightLab/Splats/QuestSplatRuntimeConfig.asset";

        [MenuItem("Quest Flight Lab/Build Quest Runtime Splat Samples")]
        public static void BuildRuntimeSamples()
        {
            string sampleDir = System.Environment.GetEnvironmentVariable("QFL_REAL_SPLAT_SAMPLE_DIR");
            string scenicDir = System.Environment.GetEnvironmentVariable("QFL_SCENIC_SPLAT_SAMPLE_DIR");

            Directory.CreateDirectory(RuntimeSampleFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? "Assets/Resources/QuestFlightLab/Splats");

            QuestSplatRuntimeConfig config = AssetDatabase.LoadAssetAtPath<QuestSplatRuntimeConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<QuestSplatRuntimeConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            if (!string.IsNullOrWhiteSpace(sampleDir))
            {
                config.sample5k = CreateGaussianSplatAsset(Path.Combine(sampleDir, "synthetic_splats_5000.ply"));
                config.sample50k = CreateGaussianSplatAsset(Path.Combine(sampleDir, "synthetic_splats_50000.ply"));
                config.sample100k = CreateGaussianSplatAsset(Path.Combine(sampleDir, "synthetic_splats_100000.ply"));
            }
            else if (config.sample5k == null || config.sample50k == null || config.sample100k == null)
            {
                throw new InvalidOperationException("QFL_REAL_SPLAT_SAMPLE_DIR was not set and existing synthetic runtime assets are incomplete.");
            }

            if (!string.IsNullOrWhiteSpace(scenicDir))
            {
                config.scenicLowSample = CreateGaussianSplatAsset(Path.Combine(scenicDir, "scenic_airfield_low_25000.ply"));
                config.scenicMediumSample = CreateGaussianSplatAsset(Path.Combine(scenicDir, "scenic_airfield_medium_50000.ply"));
                config.scenicHighSample = CreateGaussianSplatAsset(Path.Combine(scenicDir, "scenic_airfield_high_100000.ply"));
            }
            config.renderSplatsShader = LoadShader("RenderGaussianSplats.shader");
            config.compositeShader = LoadShader("GaussianComposite.shader");
            config.debugPointsShader = LoadShader("GaussianDebugRenderPoints.shader");
            config.debugBoxesShader = LoadShader("GaussianDebugRenderBoxes.shader");
            config.splatUtilitiesCompute = LoadCompute("SplatUtilities.compute");
            config.sampleWorldPosition = new Vector3(0f, 3.0f, 18f);
            config.sampleEulerAngles = Vector3.zero;
            config.splatScale = 1.35f;
            config.opacityScale = 1.0f;
            config.sphericalHarmonicsOrder = 0;
            config.sortNthFrame = 1;
            config.scenicWorldPosition = Vector3.zero;
            config.scenicEulerAngles = new Vector3(0f, 90f, 0f);
            config.scenicSplatScale = 1.0f;
            config.scenicOpacityScale = 0.58f;
            config.scenicSortNthFrame = 1;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log($"[QuestFlightLab][SplatRuntime] Runtime splat samples/config built at {ConfigPath}");
        }

        private static GaussianSplatAsset CreateGaussianSplatAsset(string inputFile)
        {
            if (!File.Exists(inputFile))
            {
                throw new FileNotFoundException($"Missing synthetic PLY sample: {inputFile}");
            }

            Type creatorType = Type.GetType("GaussianSplatting.Editor.GaussianSplatAssetCreator, GaussianSplattingEditor", true);
            ScriptableObject creator = ScriptableObject.CreateInstance(creatorType);
            try
            {
                SetField(creatorType, creator, "m_InputFile", inputFile);
                SetField(creatorType, creator, "m_OutputFolder", RuntimeSampleFolder);
                SetField(creatorType, creator, "m_ImportCameras", false);
                Type qualityType = creatorType.GetNestedType("DataQuality", BindingFlags.NonPublic);
                SetField(creatorType, creator, "m_Quality", Enum.Parse(qualityType, "Medium"));
                creatorType.GetMethod("ApplyQualityLevel", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(creator, null);
                creatorType.GetMethod("CreateAsset", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(creator, null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(creator);
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            string assetPath = $"{RuntimeSampleFolder}/{Path.GetFileNameWithoutExtension(inputFile)}.asset";
            GaussianSplatAsset asset = AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"GaussianSplatAsset was not created at {assetPath}");
            }

            return asset;
        }

        private static void SetField(Type type, object instance, string name, object value)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            field.SetValue(instance, value);
        }

        private static Shader LoadShader(string name)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>($"Packages/org.nesnausk.gaussian-splatting/Shaders/{name}");
            if (shader == null) throw new FileNotFoundException($"Missing Gaussian splat shader {name}");
            return shader;
        }

        private static ComputeShader LoadCompute(string name)
        {
            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>($"Packages/org.nesnausk.gaussian-splatting/Shaders/{name}");
            if (shader == null) throw new FileNotFoundException($"Missing Gaussian splat compute shader {name}");
            return shader;
        }
    }
}
