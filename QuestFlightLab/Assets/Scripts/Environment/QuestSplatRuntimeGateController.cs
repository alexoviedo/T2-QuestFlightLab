using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace QuestFlightLab.Environment
{
    [Serializable]
    public class QuestSplatRuntimeEvidence
    {
        public string generatedUtc;
        public string platform;
        public string unityVersion;
        public string deviceModel;
        public string graphicsDeviceType;
        public string graphicsDeviceName;
        public string renderPipeline;
        public string launchMode;
        public string requestedMode;
        public string activeMode;
        public string providerName;
        public string sampleKey;
        public string budgetProfile;
        public bool fallbackUsed;
        public bool rendererAvailable;
        public bool rendererInstantiated;
        public bool hasValidAsset;
        public bool hasValidRenderSetup;
        public string sampleName;
        public Vector3 rendererWorldPosition;
        public Vector3 rendererEulerAngles;
        public Vector3 rendererLocalBoundsMin;
        public Vector3 rendererLocalBoundsMax;
        public Vector3 rendererWorldBoundsMin;
        public Vector3 rendererWorldBoundsMax;
        public float rendererSplatScale;
        public string placementNotes;
        public int splatCount;
        public long assetBytes;
        public long estimatedGpuBytes;
        public float loadMs;
        public string loadError;
        public int frameCount;
        public int warmupFrameCount;
        public float averageFrameMs;
        public float minFrameMs;
        public float maxFrameMs;
        public float p95FrameMs;
        public float p99FrameMs;
        public float rawMaxFrameMs;
        public float estimatedFps;
        public int framesOver72HzBudget;
        public float framesOver72HzBudgetRatio;
        public string evidencePath;
        public string screenshotPath;
        public List<string> warnings = new List<string>();
    }

    public class QuestSplatRuntimeGateController : MonoBehaviour
    {
        private const string IntentExtraKey = "qfl_scenery_mode";
        private const string LogPrefix = "[QuestFlightLab][SplatRuntime]";

        public int maxFrameSamples = 720;
        public float maxCaptureSeconds = 12f;
        public float screenshotDelaySeconds = 4f;

        private QuestSplatRuntimeEvidence evidence;
        private SceneryPerformanceProbe performanceProbe;
        private SceneryProviderStatus status;
        private string evidenceDirectory;
        private string screenshotPath;
        private bool evidenceWritten;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (ProductionEnvironmentActivation.IsProductionVerticalSliceActive()) return;
            GameObject go = new GameObject("Quest Splat Runtime Gate Controller");
            DontDestroyOnLoad(go);
            go.AddComponent<QuestSplatRuntimeGateController>();
        }

        private IEnumerator Start()
        {
            yield return null;

            string launchMode = ReadLaunchMode();
            RuntimeSplatLaunchProfile launchProfile = RuntimeSplatLaunchProfile.FromLaunchMode(launchMode);
            bool splatRequested = launchProfile.splatCount > 0;

            GameObject probeObject = new GameObject("Quest Splat Runtime Performance Probe");
            probeObject.transform.SetParent(transform, false);
            performanceProbe = probeObject.AddComponent<SceneryPerformanceProbe>();
            performanceProbe.maxSamples = maxFrameSamples;

            SceneryModeController controller = FindFirstObjectByType<SceneryModeController>();
            if (controller == null)
            {
                controller = new GameObject("Scenery Mode Controller").AddComponent<SceneryModeController>();
            }

            if (splatRequested)
            {
                controller.syntheticSplatCount = launchProfile.splatCount;
                controller.splatSampleKey = launchProfile.sampleKey;
                controller.splatBudgetProfile = launchProfile.budgetProfile;
                controller.enableExperimentalSplatProxy = false;
                status = controller.ApplyMode(SceneryMode.ExperimentalSplatRenderer);
            }
            else
            {
                status = controller.ApplyMode(SceneryMode.MeshFallback);
                status.sampleKey = "mesh";
                status.budgetProfile = launchProfile.budgetProfile;
                if (launchProfile.budgetProfile != "mesh")
                {
                    status.warnings.Add("Playable visual baseline mesh/procedural scenery is active.");
                }
            }

            evidence = CreateEvidence(launchMode, status);
            Debug.Log($"{LogPrefix} Mode {launchMode}; active={status.activeMode}; fallback={status.fallbackUsed}; sample={status.sampleName}; splats={status.splatCount}; validAsset={status.hasValidAsset}; validRender={status.hasValidRenderSetup}");

            yield return new WaitForSecondsRealtime(screenshotDelaySeconds);
            CaptureScreenshot(launchMode);

            float start = Time.unscaledTime;
            while (performanceProbe.SampleCount < maxFrameSamples && Time.unscaledTime - start < maxCaptureSeconds)
            {
                yield return null;
            }

            WriteEvidence();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) WriteEvidence();
        }

        private void OnApplicationQuit()
        {
            WriteEvidence();
        }

        private QuestSplatRuntimeEvidence CreateEvidence(string launchMode, SceneryProviderStatus providerStatus)
        {
            string renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name
                : "Built-in";

            return new QuestSplatRuntimeEvidence
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                deviceModel = SystemInfo.deviceModel,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                renderPipeline = renderPipeline,
                launchMode = launchMode,
                requestedMode = providerStatus.requestedMode,
                activeMode = providerStatus.activeMode,
                providerName = providerStatus.providerName,
                sampleKey = providerStatus.sampleKey,
                budgetProfile = providerStatus.budgetProfile,
                fallbackUsed = providerStatus.fallbackUsed,
                rendererAvailable = providerStatus.rendererAvailable,
                rendererInstantiated = providerStatus.rendererInstantiated,
                hasValidAsset = providerStatus.hasValidAsset,
                hasValidRenderSetup = providerStatus.hasValidRenderSetup,
                sampleName = providerStatus.sampleName,
                rendererWorldPosition = providerStatus.rendererWorldPosition,
                rendererEulerAngles = providerStatus.rendererEulerAngles,
                rendererLocalBoundsMin = providerStatus.rendererLocalBoundsMin,
                rendererLocalBoundsMax = providerStatus.rendererLocalBoundsMax,
                rendererWorldBoundsMin = providerStatus.rendererWorldBoundsMin,
                rendererWorldBoundsMax = providerStatus.rendererWorldBoundsMax,
                rendererSplatScale = providerStatus.rendererSplatScale,
                placementNotes = providerStatus.placementNotes,
                splatCount = providerStatus.splatCount,
                assetBytes = providerStatus.assetBytes,
                estimatedGpuBytes = providerStatus.estimatedGpuBytes,
                loadMs = providerStatus.loadMs,
                loadError = providerStatus.loadError,
                warnings = new List<string>(providerStatus.warnings)
            };
        }

        private void CaptureScreenshot(string launchMode)
        {
            try
            {
                EnsureEvidenceDirectory();
                screenshotPath = Path.Combine(evidenceDirectory, $"quest_splat_runtime_{Sanitize(launchMode)}.png");
                Type screenCaptureType = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
                MethodInfo captureMethod = screenCaptureType?.GetMethod("CaptureScreenshot", new[] { typeof(string) });
                if (captureMethod == null)
                {
                    evidence?.warnings.Add("Unity ScreenCapture API is unavailable in this build; use ADB screencap artifact.");
                    Debug.LogWarning($"{LogPrefix} Unity ScreenCapture API unavailable; use ADB screencap artifact.");
                    return;
                }

                captureMethod.Invoke(null, new object[] { screenshotPath });
                if (evidence != null) evidence.screenshotPath = screenshotPath;
                Debug.Log($"{LogPrefix} Screenshot requested: {screenshotPath}");
            }
            catch (Exception ex)
            {
                evidence?.warnings.Add("Screenshot capture failed: " + ex.Message);
                Debug.LogWarning($"{LogPrefix} Screenshot capture failed: {ex.Message}");
            }
        }

        private void WriteEvidence()
        {
            if (evidence == null || evidenceWritten) return;

            SceneryPerformanceSnapshot snapshot = performanceProbe != null ? performanceProbe.Capture() : new SceneryPerformanceSnapshot();
            evidence.frameCount = snapshot.sampleCount;
            evidence.warmupFrameCount = snapshot.warmupSamplesDiscarded;
            evidence.averageFrameMs = snapshot.averageFrameMs;
            evidence.minFrameMs = snapshot.minFrameMs;
            evidence.maxFrameMs = snapshot.maxFrameMs;
            evidence.p95FrameMs = snapshot.p95FrameMs;
            evidence.p99FrameMs = snapshot.p99FrameMs;
            evidence.rawMaxFrameMs = snapshot.rawMaxFrameMs;
            evidence.estimatedFps = snapshot.estimatedFps;
            evidence.framesOver72HzBudget = snapshot.framesOver72HzBudget;
            evidence.framesOver72HzBudgetRatio = snapshot.framesOver72HzBudgetRatio;
            evidence.generatedUtc = DateTime.UtcNow.ToString("O");

            EnsureEvidenceDirectory();
            string file = Path.Combine(evidenceDirectory, $"quest_splat_runtime_{Sanitize(evidence.launchMode)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            evidence.evidencePath = file;
            File.WriteAllText(file, JsonUtility.ToJson(evidence, true));
            evidenceWritten = true;
            Debug.Log($"{LogPrefix} Evidence written: {file}");
        }

        private void EnsureEvidenceDirectory()
        {
            if (!string.IsNullOrEmpty(evidenceDirectory)) return;
            evidenceDirectory = Path.Combine(Application.persistentDataPath, "QuestFlightLab", "scenery_runtime");
            Directory.CreateDirectory(evidenceDirectory);
        }

        private static string ReadLaunchMode()
        {
            string mode = ReadAndroidIntentExtra(IntentExtraKey);
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = System.Environment.GetEnvironmentVariable("QFL_SCENERY_MODE");
            }

            if (string.IsNullOrWhiteSpace(mode))
            {
                foreach (string arg in System.Environment.GetCommandLineArgs())
                {
                    if (arg.StartsWith("qfl_scenery_mode=", StringComparison.OrdinalIgnoreCase))
                    {
                        mode = arg.Substring("qfl_scenery_mode=".Length);
                    }
                    else if (arg.StartsWith("-qfl_scenery_mode=", StringComparison.OrdinalIgnoreCase))
                    {
                        mode = arg.Substring("-qfl_scenery_mode=".Length);
                    }
                }
            }

            return string.IsNullOrWhiteSpace(mode) ? "mesh" : mode.Trim().ToLowerInvariant();
        }

        private static string ReadAndroidIntentExtra(string key)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    return intent.Call<string>("getStringExtra", key);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Could not read Android intent extra {key}: {ex.Message}");
            }
#endif
            return string.Empty;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "mesh";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c.ToString(), "_");
            }

            return value.Replace(' ', '_');
        }

        private struct RuntimeSplatLaunchProfile
        {
            public string sampleKey;
            public string budgetProfile;
            public int splatCount;

            public static RuntimeSplatLaunchProfile FromLaunchMode(string mode)
            {
                string normalized = string.IsNullOrWhiteSpace(mode) ? "mesh" : mode.Trim().ToLowerInvariant().Replace("-", "_");
                if (normalized == "splat_5k" || normalized == "splat_5000")
                {
                    return new RuntimeSplatLaunchProfile { sampleKey = QuestSplatRuntimeConfig.SyntheticProfile, budgetProfile = "synthetic_5k", splatCount = 5000 };
                }

                if (normalized == "splat_50k" || normalized == "splat_50000")
                {
                    return new RuntimeSplatLaunchProfile { sampleKey = QuestSplatRuntimeConfig.SyntheticProfile, budgetProfile = "synthetic_50k", splatCount = 50000 };
                }

                if (normalized == "splat_100k" || normalized == "splat_100000")
                {
                    return new RuntimeSplatLaunchProfile { sampleKey = QuestSplatRuntimeConfig.SyntheticProfile, budgetProfile = "synthetic_100k", splatCount = 100000 };
                }

                if (normalized == "scenic_splat_low")
                {
                    return new RuntimeSplatLaunchProfile { sampleKey = QuestSplatRuntimeConfig.ScenicProfile, budgetProfile = "scenic_splat_low", splatCount = 25000 };
                }

                if (normalized == "scenic_splat_medium")
                {
                    return new RuntimeSplatLaunchProfile { sampleKey = QuestSplatRuntimeConfig.ScenicProfile, budgetProfile = "scenic_splat_medium", splatCount = 50000 };
                }

                if (normalized == "scenic_splat_high")
                {
                    return new RuntimeSplatLaunchProfile { sampleKey = QuestSplatRuntimeConfig.ScenicProfile, budgetProfile = "scenic_splat_high", splatCount = 100000 };
                }

                if (normalized == "playable_demo" || normalized == "visual_fidelity_demo" || normalized == "playable_visual_baseline" || normalized == "scenic_mesh_enhanced")
                {
                    return new RuntimeSplatLaunchProfile { sampleKey = "mesh", budgetProfile = "playable_visual_baseline", splatCount = 0 };
                }

                return new RuntimeSplatLaunchProfile { sampleKey = QuestSplatRuntimeConfig.SyntheticProfile, budgetProfile = "mesh", splatCount = 0 };
            }
        }
    }
}
