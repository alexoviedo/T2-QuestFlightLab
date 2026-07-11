using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using QuestFlightLab.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace QuestFlightLab.Runtime
{
    [Serializable]
    public class RenderQualityEvidence
    {
        public string generatedUtc;
        public string platform;
        public string unityVersion;
        public string deviceModel;
        public string graphicsDeviceType;
        public string graphicsDeviceName;
        public string renderPipeline;
        public string stereoRenderingMode;
        public string sceneryMode;
        public string profileName;
        public int antiAliasing;
        public AnisotropicFiltering anisotropicFiltering;
        public float lodBias;
        public float shadowDistance;
        public float eyeTextureResolutionScale;
        public int globalTextureMipmapLimit;
        public int targetFrameRate;
        public float cameraFarClipMeters;
        public int runningXrDisplayCount;
        public bool fixedFoveatedRenderingRequested;
        public bool fixedFoveatedRenderingApplied;
        public float foveatedRenderingLevel;
        public bool dynamicResolutionEnabled;
        public float dynamicResolutionScaleX;
        public float dynamicResolutionScaleY;
        public int directionalShadowLightCount;
        public int shadowCascades;
        public float shadowCascade2Split;
        public ShadowResolution shadowResolution;
        public ShadowProjection shadowProjection;
        public AmbientMode ambientMode;
        public float ambientIntensity;
        public DefaultReflectionMode defaultReflectionMode;
        public int defaultReflectionResolution;
        public float reflectionIntensity;
        public bool supportsInstancing;
        public bool fogEnabled;
        public Color fogColor;
        public float fogDensity;
        public string evidencePath;
    }

    public class QuestRenderQualityConfigurator : MonoBehaviour
    {
        private const string LogPrefix = "[QuestFlightLab][RenderQuality]";
        public const float QuestFixedFoveationLevel = 0.45f;
        public const float QuestEyeTextureResolutionScale = 1.00f;
        public const float QuestLodBias = 1.25f;
        public const float QuestShadowDistanceMeters = 40f;
        public const float QuestNearShadowCascadeFraction = 0.15f;
        public const float EditorNearShadowCascadeFraction = 0.22f;
        public const float MinimumCameraFarClipMeters = 18000f;
        public const int StableSkyReflectionResolution = 128;
        public const float StableSkyReflectionIntensity = 0.72f;
        private static bool applied;
        private static bool foveationApplied;
        private static Material proceduralSkybox;

        public static EnvironmentRenderOptimizationReport LastEnvironmentOptimization { get; private set; }

        public static EnvironmentRenderOptimizationReport ApplyEnvironmentOptimization()
        {
            LastEnvironmentOptimization = QuestEnvironmentRenderOptimizer.OptimizeScene();
            return LastEnvironmentOptimization;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            ApplyProfile("visual_fidelity_demo");
            GameObject go = new GameObject("Quest Render Quality Configurator");
            DontDestroyOnLoad(go);
            go.AddComponent<QuestRenderQualityConfigurator>();
            go.AddComponent<QuestRenderBudgetReporter>();
        }

        public static RenderQualityEvidence ApplyProfile(string profileName)
        {
            bool android = Application.platform == RuntimePlatform.Android;
            int msaa = 4;

            QualitySettings.antiAliasing = msaa;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.lodBias = android ? QuestLodBias : 1.90f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.shadowDistance = android ? QuestShadowDistanceMeters : 135f;
            QualitySettings.shadowResolution = android ? ShadowResolution.Low : ShadowResolution.Medium;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowCascade2Split = android
                ? QuestNearShadowCascadeFraction
                : EditorNearShadowCascadeFraction;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.pixelLightCount = android ? 1 : 2;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.softParticles = false;
            QualitySettings.softVegetation = false;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = android ? 72 : -1;

            XRSettings.eyeTextureResolutionScale = android ? QuestEyeTextureResolutionScale : 1.0f;

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.82f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = StableSkyReflectionResolution;
            RenderSettings.reflectionBounces = 1;
            RenderSettings.reflectionIntensity = StableSkyReflectionIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.58f, 0.68f, 0.80f);
            RenderSettings.fogDensity = 0.00016f;
            ApplyProceduralSkybox();
            ConfigureDirectionalLights();

            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.allowMSAA = true;
                camera.allowHDR = false;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, MinimumCameraFarClipMeters);
                camera.nearClipPlane = Mathf.Clamp(camera.nearClipPlane, 0.04f, 0.08f);
            }

            RenderQualityEvidence evidence = CaptureEvidence(profileName);
            if (!applied)
            {
                applied = true;
                Debug.Log($"{LogPrefix} Applied profile={profileName} aa={evidence.antiAliasing} aniso={evidence.anisotropicFiltering} lodBias={evidence.lodBias:0.00} shadowDistance={evidence.shadowDistance:0} shadowCascades={evidence.shadowCascades} nearCascade={evidence.shadowCascade2Split:0.00} eyeScale={evidence.eyeTextureResolutionScale:0.00} foveationRequest={QuestFixedFoveationLevel:0.00} farClip={evidence.cameraFarClipMeters:0} reflection={evidence.defaultReflectionMode}/{evidence.defaultReflectionResolution}");
            }

            return evidence;
        }

        public static void ApplyProceduralSkybox()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return;
            if (proceduralSkybox == null)
            {
                proceduralSkybox = new Material(shader)
                {
                    name = "QuestFlightLab Colorado Daylight Procedural Sky"
                };
            }

            proceduralSkybox.SetColor("_SkyTint", new Color(0.50f, 0.64f, 0.82f));
            proceduralSkybox.SetColor("_GroundColor", new Color(0.47f, 0.50f, 0.45f));
            proceduralSkybox.SetFloat("_AtmosphereThickness", 0.92f);
            proceduralSkybox.SetFloat("_Exposure", 1.08f);
            proceduralSkybox.SetFloat("_SunSize", 0.035f);
            proceduralSkybox.SetFloat("_SunSizeConvergence", 4.0f);
            RenderSettings.skybox = proceduralSkybox;
            DynamicGI.UpdateEnvironment();
        }

        public static void ConfigureDirectionalLights()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light primary = null;
            foreach (Light light in lights)
            {
                if (light == null || light.type != LightType.Directional || !light.enabled) continue;
                if (primary == null || light.intensity > primary.intensity) primary = light;
            }

            foreach (Light light in lights)
            {
                if (light == null || light.type != LightType.Directional || !light.enabled) continue;
                if (light == primary)
                {
                    light.shadows = LightShadows.Hard;
                    light.shadowStrength = 0.64f;
                    light.shadowBias = 0.08f;
                    light.shadowNormalBias = 0.32f;
                }
                else
                {
                    light.shadows = LightShadows.None;
                }
            }
        }

        private IEnumerator Start()
        {
            ApplyProfile(QuestLaunchOptions.SceneryMode());
            ApplyEnvironmentOptimization();
            yield return ConfigureFixedFoveation();
            RenderQualityEvidence evidence = CaptureEvidence(QuestLaunchOptions.SceneryMode());
            WriteEvidence(evidence);
        }

        public static RenderQualityEvidence CaptureEvidence(string profileName)
        {
            List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            XRDisplaySubsystem runningDisplay = displays.Find(display => display != null && display.running);
            int dynamicResolutionCameras = 0;
            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera.allowDynamicResolution) dynamicResolutionCameras++;
            }

            return new RenderQualityEvidence
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
                deviceModel = SystemInfo.deviceModel,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                renderPipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.GetType().Name : "Built-in",
                stereoRenderingMode = XRSettings.stereoRenderingMode.ToString(),
                sceneryMode = QuestLaunchOptions.SceneryMode(),
                profileName = profileName,
                antiAliasing = QualitySettings.antiAliasing,
                anisotropicFiltering = QualitySettings.anisotropicFiltering,
                lodBias = QualitySettings.lodBias,
                shadowDistance = QualitySettings.shadowDistance,
                eyeTextureResolutionScale = XRSettings.eyeTextureResolutionScale,
                globalTextureMipmapLimit = QualitySettings.globalTextureMipmapLimit,
                targetFrameRate = Application.targetFrameRate,
                cameraFarClipMeters = MaxCameraFarClip(),
                runningXrDisplayCount = displays.FindAll(display => display != null && display.running).Count,
                fixedFoveatedRenderingRequested = Application.platform == RuntimePlatform.Android,
                fixedFoveatedRenderingApplied = foveationApplied,
                foveatedRenderingLevel = runningDisplay != null ? runningDisplay.foveatedRenderingLevel : 0f,
                dynamicResolutionEnabled = dynamicResolutionCameras > 0,
                dynamicResolutionScaleX = ScalableBufferManager.widthScaleFactor,
                dynamicResolutionScaleY = ScalableBufferManager.heightScaleFactor,
                directionalShadowLightCount = CountDirectionalShadowLights(),
                shadowCascades = QualitySettings.shadowCascades,
                shadowCascade2Split = QualitySettings.shadowCascade2Split,
                shadowResolution = QualitySettings.shadowResolution,
                shadowProjection = QualitySettings.shadowProjection,
                ambientMode = RenderSettings.ambientMode,
                ambientIntensity = RenderSettings.ambientIntensity,
                defaultReflectionMode = RenderSettings.defaultReflectionMode,
                defaultReflectionResolution = RenderSettings.defaultReflectionResolution,
                reflectionIntensity = RenderSettings.reflectionIntensity,
                supportsInstancing = SystemInfo.supportsInstancing,
                fogEnabled = RenderSettings.fog,
                fogColor = RenderSettings.fogColor,
                fogDensity = RenderSettings.fogDensity
            };
        }

        private static IEnumerator ConfigureFixedFoveation()
        {
            foveationApplied = false;
            if (Application.platform != RuntimePlatform.Android) yield break;

            List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
            for (int attempt = 0; attempt < 120; attempt++)
            {
                displays.Clear();
                SubsystemManager.GetSubsystems(displays);
                XRDisplaySubsystem display = displays.Find(candidate => candidate != null && candidate.running);
                if (display == null)
                {
                    yield return null;
                    continue;
                }

                try
                {
                    display.foveatedRenderingLevel = QuestFixedFoveationLevel;
                    foveationApplied = Mathf.Abs(display.foveatedRenderingLevel - QuestFixedFoveationLevel) <= 0.05f;
                    Debug.Log($"{LogPrefix} Fixed foveation requested={QuestFixedFoveationLevel:0.00} actual={display.foveatedRenderingLevel:0.00} applied={foveationApplied}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{LogPrefix} Fixed foveation unavailable: {ex.Message}");
                }

                yield break;
            }

            Debug.LogWarning($"{LogPrefix} Fixed foveation was requested but no running XR display appeared within 120 frames.");
        }

        private static int CountDirectionalShadowLights()
        {
            int count = 0;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light != null && light.enabled && light.type == LightType.Directional && light.shadows != LightShadows.None) count++;
            }

            return count;
        }

        private static float MaxCameraFarClip()
        {
            float farClip = 0f;
            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                farClip = Mathf.Max(farClip, camera.farClipPlane);
            }

            return farClip;
        }

        private static void WriteEvidence(RenderQualityEvidence evidence)
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "QuestFlightLab", "render_quality");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"render_quality_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                evidence.evidencePath = path;
                File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Evidence write failed: {ex.Message}");
            }
        }
    }
}
