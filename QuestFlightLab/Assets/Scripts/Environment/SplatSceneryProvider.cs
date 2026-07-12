using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using QuestFlightLab.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace QuestFlightLab.Environment
{
    public class SplatSceneryProvider : SceneryVisualProvider
    {
        public const string QuestXrStereoWorldLockWarning = "Real Gaussian splat renderer disabled: XR stereo/world-lock check failed";

        public bool enableExperimentalProxy;
        public int syntheticSplatCount = 5000;
        public int maxProxyPointCount = 5000;
        public string sampleAssetPath;
        public string runtimeSampleKey = QuestSplatRuntimeConfig.SyntheticProfile;
        public string runtimeBudgetProfile = "synthetic";
        public bool enableRealRenderer = true;
        public string runtimeConfigResourcePath = QuestSplatRuntimeConfig.ResourcePath;

        private GameObject activeRuntimeRenderer;

        public override SceneryMode Mode => SceneryMode.ExperimentalSplatRenderer;

        public override SceneryProviderStatus ActivateProvider(Transform parent)
        {
            if (ProductionEnvironmentActivation.IsProductionVerticalSliceActive())
            {
                return ProductionEnvironmentActivation.BakedProductionStatus(nameof(SplatSceneryProvider), Mode);
            }
            bool rendererAvailable = IsGaussianSplatRendererAvailable();
            SceneryProviderStatus status = Status(nameof(SplatSceneryProvider), Mode, SceneryMode.MeshFallback);
            status.rendererAvailable = rendererAvailable;
            status.sampleAssetPath = sampleAssetPath ?? string.Empty;
            status.sampleKey = QuestSplatRuntimeConfig.NormalizeProfile(runtimeSampleKey);
            status.budgetProfile = string.IsNullOrWhiteSpace(runtimeBudgetProfile) ? status.sampleKey : runtimeBudgetProfile;
            status.splatCount = Mathf.Max(0, syntheticSplatCount);
            status.assetBytes = File.Exists(sampleAssetPath) ? new FileInfo(sampleAssetPath).Length : 0L;
            status.estimatedGpuBytes = SceneryPerformanceProbe.EstimateGpuBytes(status.splatCount);

            if (!rendererAvailable)
            {
                status.fallbackUsed = true;
                status.warnings.Add("No Unity Gaussian splat renderer package was detected. Mesh fallback must remain active.");
            }

            if (rendererAvailable && enableRealRenderer)
            {
                if (ShouldDisableRealRendererForCurrentRuntime())
                {
                    status.fallbackUsed = true;
                    status.activeMode = SceneryMode.MeshFallback.ToString();
                    status.loadError = "blocked_xr_stereo_composite";
                    status.placementNotes = "UnityGaussianSplatting v1.1.1 rendered as a one-eye/headset-locked composite in Quest captures; normal playtest mode gates it off until a stereo/world-lock renderer fix is proven.";
                    status.warnings.Add(QuestXrStereoWorldLockWarning);
                    status.warnings.Add("Use qfl_splat_diagnostic=true only for bounded developer XR splat renderer experiments.");
                    Debug.LogWarning($"[QuestFlightLab][Splats] {QuestXrStereoWorldLockWarning}. Mesh fallback remains active.");
                    return status;
                }

                if (QuestLaunchOptions.SplatDiagnosticEnabled())
                {
                    status.warnings.Add("Developer splat diagnostic mode is enabled; real Gaussian renderer may be visually unsafe in Quest XR.");
                }

                if (TryCreateRuntimeRenderer(parent, status))
                {
                    return status;
                }
            }

            if (enableExperimentalProxy)
            {
                CreateProxyPointCloud(parent, status);
            }

            Debug.Log("[QuestFlightLab][Splats] Splat renderer unavailable. Mesh fallback remains required.");
            return status;
        }

        public static bool ShouldDisableRealRendererForQuestXr(bool isAndroid, string deviceModel, bool diagnosticEnabled)
        {
            if (!isAndroid || diagnosticEnabled) return false;
            return LooksLikeQuestDevice(deviceModel);
        }

        private static bool ShouldDisableRealRendererForCurrentRuntime()
        {
            return ShouldDisableRealRendererForQuestXr(
                Application.platform == RuntimePlatform.Android,
                SystemInfo.deviceModel,
                QuestLaunchOptions.SplatDiagnosticEnabled());
        }

        private static bool LooksLikeQuestDevice(string deviceModel)
        {
            if (string.IsNullOrWhiteSpace(deviceModel)) return false;
            string normalized = deviceModel.Trim().ToLowerInvariant();
            return normalized.Contains("quest") ||
                   normalized.Contains("oculus") ||
                   normalized.Contains("meta");
        }

        public override void DeactivateProvider()
        {
            if (activeRuntimeRenderer != null)
            {
                DestroyRuntimeObject(activeRuntimeRenderer);
                activeRuntimeRenderer = null;
            }
        }

        public static bool IsGaussianSplatRendererAvailable()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    string name = type.FullName ?? type.Name;
                    if (name.IndexOf("Gaussian", StringComparison.OrdinalIgnoreCase) < 0 ||
                        name.IndexOf("Splat", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (name.IndexOf("Renderer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Asset", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryCreateRuntimeRenderer(Transform parent, SceneryProviderStatus status)
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                QuestSplatRuntimeConfig config = Resources.Load<QuestSplatRuntimeConfig>(runtimeConfigResourcePath);
                if (config == null)
                {
                    status.fallbackUsed = true;
                    status.warnings.Add($"Runtime splat config missing from Resources path '{runtimeConfigResourcePath}'.");
                    return false;
                }

                UnityEngine.Object sample = config.AssetForProfile(status.sampleKey, status.splatCount);
                if (sample == null)
                {
                    status.fallbackUsed = true;
                    status.warnings.Add($"No runtime GaussianSplatAsset configured for profile {status.sampleKey} budget {status.splatCount}.");
                    return false;
                }

                Type rendererType = FindGaussianSplatRendererType();
                if (rendererType == null)
                {
                    status.fallbackUsed = true;
                    status.warnings.Add("GaussianSplatRenderer type was not found at runtime.");
                    return false;
                }

                if (!ComputeKernelsSupported(config.splatUtilitiesCompute, status))
                {
                    status.fallbackUsed = true;
                    return false;
                }

                DestroyExistingRuntimeRenderer();

                activeRuntimeRenderer = new GameObject($"QuestRuntimeGaussianSplat_{status.sampleKey}_{status.splatCount}");
                activeRuntimeRenderer.transform.SetParent(parent, false);
                activeRuntimeRenderer.transform.position = config.WorldPositionForProfile(status.sampleKey);
                activeRuntimeRenderer.transform.rotation = Quaternion.Euler(config.EulerAnglesForProfile(status.sampleKey));
                RecordPlacementStatus(status, config, sample, activeRuntimeRenderer.transform);

                Component renderer = activeRuntimeRenderer.AddComponent(rendererType);
                SetField(rendererType, renderer, "m_Asset", sample);
                SetField(rendererType, renderer, "m_ShaderSplats", config.renderSplatsShader);
                SetField(rendererType, renderer, "m_ShaderComposite", config.compositeShader);
                SetField(rendererType, renderer, "m_ShaderDebugPoints", config.debugPointsShader);
                SetField(rendererType, renderer, "m_ShaderDebugBoxes", config.debugBoxesShader);
                SetField(rendererType, renderer, "m_CSSplatUtilities", config.splatUtilitiesCompute);
                SetField(rendererType, renderer, "m_SplatScale", config.SplatScaleForProfile(status.sampleKey));
                SetField(rendererType, renderer, "m_OpacityScale", config.OpacityScaleForProfile(status.sampleKey));
                SetField(rendererType, renderer, "m_SHOrder", config.sphericalHarmonicsOrder);
                SetField(rendererType, renderer, "m_SortNthFrame", Mathf.Max(1, config.SortNthFrameForProfile(status.sampleKey)));

                MethodInfo onEnable = rendererType.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                onEnable?.Invoke(renderer, null);

                status.activeMode = SceneryMode.ExperimentalSplatRenderer.ToString();
                status.sampleName = sample.name;
                status.sampleAssetPath = $"Resources/{runtimeConfigResourcePath}/{sample.name}";
                status.rendererInstantiated = true;
                status.hasValidAsset = ReadBoolProperty(rendererType, renderer, "HasValidAsset");
                status.hasValidRenderSetup = ReadBoolProperty(rendererType, renderer, "HasValidRenderSetup");
                status.assetBytes = EstimatePackedAssetBytes(sample);
                status.estimatedGpuBytes = SceneryPerformanceProbe.EstimateGpuBytes(status.splatCount);
                status.fallbackUsed = !(status.hasValidAsset && status.hasValidRenderSetup);

                if (status.fallbackUsed)
                {
                    status.warnings.Add("Gaussian splat renderer instantiated but did not report a valid asset/render setup.");
                }
                else
                {
                    Debug.Log($"[QuestFlightLab][Splats] Runtime Gaussian splat renderer active: {status.sampleKey}/{sample.name}, budget {status.splatCount}, pos={status.rendererWorldPosition}, euler={status.rendererEulerAngles}.");
                }

                return !status.fallbackUsed;
            }
            catch (Exception ex)
            {
                DestroyExistingRuntimeRenderer();
                status.fallbackUsed = true;
                status.loadError = ex.GetType().Name + ": " + ex.Message;
                status.warnings.Add(status.loadError);
                Debug.LogWarning($"[QuestFlightLab][Splats] Runtime renderer failed: {status.loadError}");
                return false;
            }
            finally
            {
                sw.Stop();
                status.loadMs = (float)sw.Elapsed.TotalMilliseconds;
            }
        }

        private void DestroyExistingRuntimeRenderer()
        {
            if (activeRuntimeRenderer == null) return;
            DestroyRuntimeObject(activeRuntimeRenderer);
            activeRuntimeRenderer = null;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }

        private static Type FindGaussianSplatRendererType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("GaussianSplatting.Runtime.GaussianSplatRenderer");
                if (type != null) return type;
            }

            return null;
        }

        private static void SetField(Type type, object instance, string name, object value)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            field.SetValue(instance, value);
        }

        private static bool ReadBoolProperty(Type type, object instance, string name)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null) return false;
            object value = property.GetValue(instance);
            return value is bool result && result;
        }

        private static long EstimatePackedAssetBytes(UnityEngine.Object sample)
        {
            if (sample == null) return 0L;
            Type type = sample.GetType();
            long total = 0L;
            foreach (string propertyName in new[] { "posData", "otherData", "shData", "colorData", "chunkData" })
            {
                PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property?.GetValue(sample) is TextAsset textAsset)
                {
                    total += textAsset.dataSize;
                }
            }

            return total;
        }

        private static void RecordPlacementStatus(
            SceneryProviderStatus status,
            QuestSplatRuntimeConfig config,
            UnityEngine.Object sample,
            Transform rendererTransform)
        {
            status.rendererWorldPosition = rendererTransform.position;
            status.rendererEulerAngles = rendererTransform.eulerAngles;
            status.rendererSplatScale = config.SplatScaleForProfile(status.sampleKey);

            if (TryReadVector3Field(sample, "m_BoundsMin", out Vector3 localMin) &&
                TryReadVector3Field(sample, "m_BoundsMax", out Vector3 localMax))
            {
                status.rendererLocalBoundsMin = localMin;
                status.rendererLocalBoundsMax = localMax;
                TransformLocalBounds(rendererTransform, localMin, localMax, out Vector3 worldMin, out Vector3 worldMax);
                status.rendererWorldBoundsMin = worldMin;
                status.rendererWorldBoundsMax = worldMax;
            }

            if (QuestSplatRuntimeConfig.IsScenicProfile(status.sampleKey))
            {
                status.placementNotes = "Procedural scenic patch is yaw-calibrated to the mesh runway and kept downfield as a distant runway-edge/background layer so it does not carpet the cockpit or runway start. It is visual-only, synthetic, and not surveyed terrain registration.";
            }
        }

        private static bool TryReadVector3Field(UnityEngine.Object obj, string fieldName, out Vector3 value)
        {
            value = Vector3.zero;
            if (obj == null) return false;

            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return false;
            object raw = field.GetValue(obj);
            if (!(raw is Vector3 vector)) return false;

            value = vector;
            return true;
        }

        private static void TransformLocalBounds(Transform transform, Vector3 localMin, Vector3 localMax, out Vector3 worldMin, out Vector3 worldMax)
        {
            worldMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            worldMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 local = new Vector3(
                            x == 0 ? localMin.x : localMax.x,
                            y == 0 ? localMin.y : localMax.y,
                            z == 0 ? localMin.z : localMax.z);
                        Vector3 world = transform.TransformPoint(local);
                        worldMin = Vector3.Min(worldMin, world);
                        worldMax = Vector3.Max(worldMax, world);
                    }
                }
            }
        }

        private static bool ComputeKernelsSupported(ComputeShader computeShader, SceneryProviderStatus status)
        {
            if (computeShader == null)
            {
                status.warnings.Add("Gaussian splat compute shader reference is missing.");
                return false;
            }

            if (!SystemInfo.supportsComputeShaders)
            {
                status.warnings.Add("Current platform reports no compute shader support.");
                return false;
            }

            MethodInfo isSupported = typeof(ComputeShader).GetMethod("IsSupported", new[] { typeof(int) });
            if (isSupported == null)
            {
                status.warnings.Add("ComputeShader.IsSupported is unavailable; proceeding without per-kernel preflight.");
                return true;
            }

            foreach (string kernelName in new[] { "CSSetIndices", "CSCalcDistances", "CSCalcViewData", "InitDeviceRadixSort", "Upsweep", "Scan", "Downsweep" })
            {
                int kernelIndex;
                try
                {
                    kernelIndex = computeShader.FindKernel(kernelName);
                }
                catch (Exception ex)
                {
                    status.warnings.Add($"Gaussian splat compute kernel '{kernelName}' missing: {ex.Message}");
                    return false;
                }

                bool supported;
                try
                {
                    supported = (bool)isSupported.Invoke(computeShader, new object[] { kernelIndex });
                }
                catch (Exception ex)
                {
                    status.warnings.Add($"Could not preflight compute kernel '{kernelName}': {ex.Message}");
                    return false;
                }

                if (!supported)
                {
                    status.warnings.Add($"Gaussian splat compute kernel '{kernelName}' is unsupported on {SystemInfo.graphicsDeviceType}; mesh fallback required.");
                    return false;
                }
            }

            return true;
        }

        private void CreateProxyPointCloud(Transform parent, SceneryProviderStatus status)
        {
            int pointCount = Mathf.Clamp(status.splatCount, 1, Mathf.Max(1, maxProxyPointCount));
            GameObject proxy = new GameObject($"ExperimentalSplatProxy_{pointCount}_points");
            proxy.transform.SetParent(parent, false);
            proxy.transform.localPosition = new Vector3(0f, 18f, 180f);
            proxy.transform.localRotation = Quaternion.identity;

            Mesh mesh = new Mesh { name = "Experimental Splat Proxy Points" };
            if (pointCount > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            Vector3[] vertices = new Vector3[pointCount];
            Color32[] colors = new Color32[pointCount];
            int[] indices = new int[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                float t = i / Mathf.Max(1f, pointCount - 1f);
                float angle = t * Mathf.PI * 2f * 17f;
                float radius = 12f + 18f * Mathf.Sin(t * Mathf.PI);
                vertices[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(t * Mathf.PI) * 12f, Mathf.Sin(angle) * radius);
                colors[i] = new Color32((byte)(80 + 120 * t), (byte)(130 + 80 * (1f - t)), 180, 170);
                indices[i] = i;
            }

            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.SetIndices(indices, MeshTopology.Points, 0);
            mesh.RecalculateBounds();

            MeshFilter filter = proxy.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = proxy.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateProxyMaterial();

            status.activeMode = SceneryMode.ExperimentalSplatProxy.ToString();
            status.proxyUsed = true;
            status.fallbackUsed = true;
            status.warnings.Add("Editor proxy point cloud created for budget plumbing only; it is not a Gaussian splat renderer or Quest visual proof.");
        }

        private static Material CreateProxyMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader);
            material.name = "Experimental Splat Proxy Material";
            material.color = new Color(0.35f, 0.65f, 0.9f, 0.7f);
            return material;
        }
    }
}
