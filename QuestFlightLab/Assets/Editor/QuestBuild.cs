using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace QuestFlightLab.Editor
{
    public static class QuestBuild
    {
        public static void PerformAndroidBuild()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            ConfigureQuestRenderBuildSettings();

            string scenePath = "Assets/Scenes/InputLab.unity";
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException($"Scene not found: {scenePath}. Run QuestProjectBootstrap.CreateInputLabScene first.");
            }

            string outDir = Path.GetFullPath("Builds/Android");
            Directory.CreateDirectory(outDir);
            string apkPath = Path.Combine(outDir, "QuestFlightLab-v0.1-dev.apk");
            WriteRenderSettingsEvidence(outDir);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Android build failed: {report.summary.result}");
            }

            Debug.Log($"[QuestFlightLab] Build succeeded: {apkPath}");
        }

        public static void ConfigureQuestRenderBuildSettings()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
            PlayerSettings.enableFrameTimingStats = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            OpenXRSettings openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXrSettings == null) throw new InvalidOperationException("Android OpenXR settings are missing.");
            openXrSettings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            openXrSettings.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
            OpenXRFeature foveation = FeatureHelpers.GetFeatureWithIdForBuildTarget(
                BuildTargetGroup.Android,
                FoveatedRenderingFeature.featureId);
            if (foveation == null) throw new InvalidOperationException("Android OpenXR foveated-rendering feature is missing.");
            foveation.enabled = true;
            EditorUtility.SetDirty(openXrSettings);
            EditorUtility.SetDirty(foveation);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[QuestFlightLab][BuildRender] Android graphics=Vulkan-only textureCompression=ASTC " +
                "stereo=OpenXR single-pass-instanced frameTimingStats=true multithreadedRendering=true");
        }

        private static void WriteRenderSettingsEvidence(string outDir)
        {
            GraphicsDeviceType[] graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            OpenXRSettings openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            OpenXRFeature foveation = FeatureHelpers.GetFeatureWithIdForBuildTarget(
                BuildTargetGroup.Android,
                FoveatedRenderingFeature.featureId);
            string json = JsonUtility.ToJson(new QuestBuildRenderSettingsEvidence
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                graphicsApis = Array.ConvertAll(graphicsApis, api => api.ToString()),
                textureCompression = EditorUserBuildSettings.androidBuildSubtarget.ToString(),
                colorSpace = PlayerSettings.colorSpace.ToString(),
                frameTimingStats = PlayerSettings.enableFrameTimingStats,
                multithreadedRendering = PlayerSettings.GetMobileMTRendering(BuildTargetGroup.Android),
                targetArchitectures = PlayerSettings.Android.targetArchitectures.ToString(),
                openXrRenderMode = openXrSettings != null ? openXrSettings.renderMode.ToString() : "missing",
                latencyOptimization = openXrSettings != null ? openXrSettings.latencyOptimization.ToString() : "missing",
                foveatedRenderingFeatureEnabled = foveation != null && foveation.enabled
            }, true);
            File.WriteAllText(Path.Combine(outDir, "quest_build_render_settings.json"), json);
        }

        [Serializable]
        private class QuestBuildRenderSettingsEvidence
        {
            public string generatedUtc;
            public string[] graphicsApis;
            public string textureCompression;
            public string colorSpace;
            public bool frameTimingStats;
            public bool multithreadedRendering;
            public string targetArchitectures;
            public string openXrRenderMode;
            public string latencyOptimization;
            public bool foveatedRenderingFeatureEnabled;
        }
    }
}
