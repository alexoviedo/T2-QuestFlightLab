using System;
using System.IO;
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
        public bool fogEnabled;
        public Color fogColor;
        public float fogDensity;
        public string evidencePath;
    }

    public class QuestRenderQualityConfigurator : MonoBehaviour
    {
        private const string LogPrefix = "[QuestFlightLab][RenderQuality]";
        private static bool applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            ApplyProfile("visual_fidelity_demo");
            GameObject go = new GameObject("Quest Render Quality Configurator");
            DontDestroyOnLoad(go);
            go.AddComponent<QuestRenderQualityConfigurator>();
        }

        public static RenderQualityEvidence ApplyProfile(string profileName)
        {
            bool android = Application.platform == RuntimePlatform.Android;
            bool visualDemo = QuestLaunchOptions.VisualFidelityDemoRequested() || QuestLaunchOptions.PlaytestHudEnabled();
            int msaa = android ? 2 : 4;
            if (visualDemo) msaa = android ? 4 : 4;

            QualitySettings.antiAliasing = msaa;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.lodBias = android ? 1.45f : 1.90f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.shadowDistance = android ? 72f : 135f;
            QualitySettings.shadowResolution = android ? ShadowResolution.Low : ShadowResolution.Medium;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = android ? 72 : -1;
            GL.sRGBWrite = false;

            XRSettings.eyeTextureResolutionScale = android ? 1.15f : 1.0f;

            RenderSettings.ambientLight = new Color(0.46f, 0.50f, 0.54f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.58f, 0.68f, 0.80f);
            RenderSettings.fogDensity = 0.00016f;
            ApplyProceduralSkybox();

            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.allowMSAA = true;
                camera.allowHDR = false;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 12000f);
                camera.nearClipPlane = Mathf.Clamp(camera.nearClipPlane, 0.04f, 0.08f);
            }

            RenderQualityEvidence evidence = CaptureEvidence(profileName);
            if (!applied)
            {
                applied = true;
                Debug.Log($"{LogPrefix} Applied profile={profileName} aa={evidence.antiAliasing} aniso={evidence.anisotropicFiltering} lodBias={evidence.lodBias:0.00} shadowDistance={evidence.shadowDistance:0} eyeScale={evidence.eyeTextureResolutionScale:0.00}");
            }

            return evidence;
        }

        public static void ApplyProceduralSkybox()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return;
            Material sky = new Material(shader)
            {
                name = "QuestFlightLab Colorado Daylight Procedural Sky"
            };
            sky.SetColor("_SkyTint", new Color(0.50f, 0.64f, 0.82f));
            sky.SetColor("_GroundColor", new Color(0.47f, 0.50f, 0.45f));
            sky.SetFloat("_AtmosphereThickness", 0.92f);
            sky.SetFloat("_Exposure", 1.08f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 4.0f);
            RenderSettings.skybox = sky;
            DynamicGI.UpdateEnvironment();
        }

        private void Start()
        {
            RenderQualityEvidence evidence = ApplyProfile(QuestLaunchOptions.SceneryMode());
            WriteEvidence(evidence);
        }

        public static RenderQualityEvidence CaptureEvidence(string profileName)
        {
            return new RenderQualityEvidence
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion,
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
                fogEnabled = RenderSettings.fog,
                fogColor = RenderSettings.fogColor,
                fogDensity = RenderSettings.fogDensity
            };
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
