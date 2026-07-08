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
            QualitySettings.lodBias = android ? 1.2f : 1.55f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.shadowDistance = android ? 55f : 95f;
            QualitySettings.shadowResolution = android ? ShadowResolution.Low : ShadowResolution.Medium;
            QualitySettings.vSyncCount = 0;
            GL.sRGBWrite = false;

            XRSettings.eyeTextureResolutionScale = android ? 1.05f : 1.0f;

            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.58f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.62f, 0.72f, 0.84f);
            RenderSettings.fogDensity = 0.00022f;

            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.allowMSAA = true;
                camera.allowHDR = false;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 6500f);
                camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, 0.03f);
            }

            RenderQualityEvidence evidence = CaptureEvidence(profileName);
            if (!applied)
            {
                applied = true;
                Debug.Log($"{LogPrefix} Applied profile={profileName} aa={evidence.antiAliasing} aniso={evidence.anisotropicFiltering} lodBias={evidence.lodBias:0.00} shadowDistance={evidence.shadowDistance:0} eyeScale={evidence.eyeTextureResolutionScale:0.00}");
            }

            return evidence;
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
                fogEnabled = RenderSettings.fog,
                fogColor = RenderSettings.fogColor,
                fogDensity = RenderSettings.fogDensity
            };
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
