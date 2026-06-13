using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.Environment
{
    public class SplatSceneryProvider : SceneryVisualProvider
    {
        public bool enableExperimentalProxy;
        public int syntheticSplatCount = 5000;
        public int maxProxyPointCount = 5000;
        public string sampleAssetPath;

        public override SceneryMode Mode => SceneryMode.ExperimentalSplatRenderer;

        public override SceneryProviderStatus ActivateProvider(Transform parent)
        {
            bool rendererAvailable = IsGaussianSplatRendererAvailable();
            SceneryProviderStatus status = Status(nameof(SplatSceneryProvider), Mode, SceneryMode.MeshFallback);
            status.rendererAvailable = rendererAvailable;
            status.sampleAssetPath = sampleAssetPath ?? string.Empty;
            status.splatCount = Mathf.Max(0, syntheticSplatCount);
            status.assetBytes = File.Exists(sampleAssetPath) ? new FileInfo(sampleAssetPath).Length : 0L;
            status.estimatedGpuBytes = SceneryPerformanceProbe.EstimateGpuBytes(status.splatCount);

            if (!rendererAvailable)
            {
                status.fallbackUsed = true;
                status.warnings.Add("No Unity Gaussian splat renderer package was detected. Mesh fallback must remain active.");
            }

            if (rendererAvailable)
            {
                status.activeMode = SceneryMode.ExperimentalSplatRenderer.ToString();
                status.warnings.Add("Renderer-like Gaussian splat types were detected, but this spike does not bind a production renderer automatically.");
                Debug.Log("[QuestFlightLab][Splats] Renderer-like package detected. Manual binding is still required before production use.");
                return status;
            }

            if (enableExperimentalProxy)
            {
                CreateProxyPointCloud(parent, status);
            }

            Debug.Log("[QuestFlightLab][Splats] Splat renderer unavailable. Mesh fallback remains required.");
            return status;
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
