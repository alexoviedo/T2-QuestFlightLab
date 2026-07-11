using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace QuestFlightLab.Runtime
{
    [Serializable]
    public class RenderBudgetSnapshot
    {
        public string generatedUtc;
        public string scope;
        public string platform;
        public string deviceModel;
        public string graphicsDeviceType;
        public string graphicsDeviceName;
        public string renderPipeline;
        public bool supportsInstancing;
        public int rendererCount;
        public int enabledRendererCount;
        public int estimatedFrustumRendererCount;
        public int materialSlotCount;
        public int estimatedFrustumMaterialSlots;
        public int uniqueMeshCount;
        public int uniqueMaterialCount;
        public int uniqueShaderCount;
        public int materialVariantSignatureCount;
        public long totalRendererTriangles;
        public long estimatedFrustumTriangles;
        public long uniqueMeshTriangles;
        public int lodGroupCount;
        public int renderersManagedByLod;
        public int renderersWithoutLod;
        public int instancingEnabledMaterialCount;
        public int instancingEligibleRendererCount;
        public int estimatedDrawCallsWithoutBatching;
        public int estimatedInstancedDrawCalls;
        public int shadowCasterCount;
        public int shadowReceiverCount;
        public int textureCount;
        public int mipmappedTextureCount;
        public int anisotropicTextureCount;
        public long estimatedTextureRuntimeBytes;
        public int terrainCount;
        public long terrainTriangles;
        public int drawCallTarget;
        public long visibleTriangleTarget;
        public bool drawCallBudgetPlausible;
        public bool visibleTriangleBudgetPlausible;
        public bool countsAreEstimates;
        public string notes;
    }

    public static class QuestRenderBudgetAudit
    {
        public const int QuestDrawCallTarget = 300;
        public const long QuestVisibleTriangleTarget = 1300000L;

        public static RenderBudgetSnapshot Capture(Camera camera = null, Transform scope = null)
        {
            Renderer[] renderers = scope != null
                ? scope.GetComponentsInChildren<Renderer>(true)
                : UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Terrain[] terrains = scope != null
                ? scope.GetComponentsInChildren<Terrain>(true)
                : UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            LODGroup[] lodGroups = scope != null
                ? scope.GetComponentsInChildren<LODGroup>(true)
                : UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Plane[] frustum = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            HashSet<Mesh> uniqueMeshes = new HashSet<Mesh>();
            HashSet<Material> uniqueMaterials = new HashSet<Material>();
            HashSet<Shader> uniqueShaders = new HashSet<Shader>();
            HashSet<Texture> uniqueTextures = new HashSet<Texture>();
            HashSet<string> materialVariants = new HashSet<string>(StringComparer.Ordinal);
            HashSet<int> lodRendererIds = CollectLodRendererIds(lodGroups);
            HashSet<int> sceneRendererIds = new HashSet<int>(renderers.Where(renderer => renderer != null).Select(renderer => renderer.GetInstanceID()));
            Dictionary<DrawKey, int> visibleDrawGroups = new Dictionary<DrawKey, int>();

            int enabledRenderers = 0;
            int frustumRenderers = 0;
            int materialSlots = 0;
            int frustumMaterialSlots = 0;
            int instancingEligibleRenderers = 0;
            int shadowCasters = 0;
            int shadowReceivers = 0;
            long totalTriangles = 0;
            long frustumTriangles = 0;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                bool enabled = renderer.enabled && renderer.gameObject.activeInHierarchy;
                bool inFrustum = enabled && (frustum == null || GeometryUtility.TestPlanesAABB(frustum, renderer.bounds));
                if (enabled) enabledRenderers++;
                if (inFrustum) frustumRenderers++;

                Mesh mesh = MeshFor(renderer);
                long triangles = TriangleCount(mesh);
                totalTriangles += triangles;
                if (inFrustum) frustumTriangles += triangles;
                if (mesh != null) uniqueMeshes.Add(mesh);

                Material[] sharedMaterials = renderer.sharedMaterials;
                materialSlots += sharedMaterials.Length;
                if (inFrustum) frustumMaterialSlots += sharedMaterials.Length;
                if (renderer.shadowCastingMode != ShadowCastingMode.Off) shadowCasters++;
                if (renderer.receiveShadows) shadowReceivers++;

                bool rendererInstancingEligible = false;
                for (int submesh = 0; submesh < sharedMaterials.Length; submesh++)
                {
                    Material material = sharedMaterials[submesh];
                    if (material == null) continue;
                    uniqueMaterials.Add(material);
                    if (material.shader != null) uniqueShaders.Add(material.shader);
                    materialVariants.Add(MaterialVariantSignature(material));
                    CollectTextures(material, uniqueTextures);

                    bool canInstance = material.enableInstancing && mesh != null;
                    rendererInstancingEligible |= canInstance;
                    if (!inFrustum) continue;
                    DrawKey key = new DrawKey(mesh, material, submesh, canInstance);
                    visibleDrawGroups.TryGetValue(key, out int count);
                    visibleDrawGroups[key] = count + 1;
                }

                if (rendererInstancingEligible) instancingEligibleRenderers++;
            }

            long uniqueTriangles = uniqueMeshes.Sum(TriangleCount);
            long terrainTriangles = 0;
            int activeTerrains = 0;
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                int resolution = terrain.terrainData.heightmapResolution;
                long triangles = Math.Max(0, resolution - 1L) * Math.Max(0, resolution - 1L) * 2L;
                terrainTriangles += triangles;
                if (terrain.enabled && terrain.gameObject.activeInHierarchy)
                {
                    activeTerrains++;
                    totalTriangles += triangles;
                    frustumTriangles += triangles;
                }
            }

            int drawCallsWithoutBatching = visibleDrawGroups.Values.Sum();
            int instancedDrawCalls = visibleDrawGroups.Sum(pair => pair.Key.instanced
                ? Mathf.CeilToInt(pair.Value / 1023f)
                : pair.Value);
            int mipmappedTextures = uniqueTextures.Count(texture => texture != null && texture.mipmapCount > 1);
            int anisotropicTextures = uniqueTextures.Count(texture => texture != null && texture.mipmapCount > 1 && texture.anisoLevel > 1);
            long textureBytes = uniqueTextures.Where(texture => texture != null).Sum(Profiler.GetRuntimeMemorySizeLong);
            int managedRendererCount = lodRendererIds.Count(sceneRendererIds.Contains);

            string pipeline = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.GetType().Name
                : "Built-in";
            RenderBudgetSnapshot snapshot = new RenderBudgetSnapshot
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                scope = scope != null ? scope.name : "scene",
                platform = Application.platform.ToString(),
                deviceModel = SystemInfo.deviceModel,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                renderPipeline = pipeline,
                supportsInstancing = SystemInfo.supportsInstancing,
                rendererCount = renderers.Length,
                enabledRendererCount = enabledRenderers,
                estimatedFrustumRendererCount = frustumRenderers,
                materialSlotCount = materialSlots,
                estimatedFrustumMaterialSlots = frustumMaterialSlots,
                uniqueMeshCount = uniqueMeshes.Count,
                uniqueMaterialCount = uniqueMaterials.Count,
                uniqueShaderCount = uniqueShaders.Count,
                materialVariantSignatureCount = materialVariants.Count,
                totalRendererTriangles = totalTriangles,
                estimatedFrustumTriangles = frustumTriangles,
                uniqueMeshTriangles = uniqueTriangles,
                lodGroupCount = lodGroups.Length,
                renderersManagedByLod = managedRendererCount,
                renderersWithoutLod = Math.Max(0, renderers.Length - managedRendererCount),
                instancingEnabledMaterialCount = uniqueMaterials.Count(material => material != null && material.enableInstancing),
                instancingEligibleRendererCount = instancingEligibleRenderers,
                estimatedDrawCallsWithoutBatching = drawCallsWithoutBatching,
                estimatedInstancedDrawCalls = instancedDrawCalls,
                shadowCasterCount = shadowCasters,
                shadowReceiverCount = shadowReceivers,
                textureCount = uniqueTextures.Count,
                mipmappedTextureCount = mipmappedTextures,
                anisotropicTextureCount = anisotropicTextures,
                estimatedTextureRuntimeBytes = textureBytes,
                terrainCount = activeTerrains,
                terrainTriangles = terrainTriangles,
                drawCallTarget = QuestDrawCallTarget,
                visibleTriangleTarget = QuestVisibleTriangleTarget,
                countsAreEstimates = true,
                notes = "Frustum renderer/triangle and instanced draw-call values are conservative scene-audit estimates, not Quest GPU counters. Validate final frame timing and draw calls on-device."
            };
            snapshot.drawCallBudgetPlausible = snapshot.estimatedInstancedDrawCalls <= snapshot.drawCallTarget;
            snapshot.visibleTriangleBudgetPlausible = snapshot.estimatedFrustumTriangles <= snapshot.visibleTriangleTarget;
            return snapshot;
        }

        public static string BuildMarkdown(RenderBudgetSnapshot snapshot, string title = "Quest Render Performance Budget")
        {
            if (snapshot == null) return $"# {title}\n\nNo render-budget snapshot was captured.\n";
            return
                $"# {title}\n\n" +
                $"- Generated UTC: {snapshot.generatedUtc}\n" +
                $"- Scope: `{snapshot.scope}`\n" +
                $"- Platform/GPU: {snapshot.platform}; {snapshot.graphicsDeviceType}; {snapshot.graphicsDeviceName}\n" +
                $"- Pipeline: {snapshot.renderPipeline}\n" +
                $"- Renderers: {snapshot.rendererCount} total; {snapshot.estimatedFrustumRendererCount} estimated in frustum\n" +
                $"- Triangles: {snapshot.totalRendererTriangles:N0} scene; {snapshot.estimatedFrustumTriangles:N0} estimated in frustum; target <= {snapshot.visibleTriangleTarget:N0}\n" +
                $"- Draw calls: {snapshot.estimatedDrawCallsWithoutBatching:N0} unbatched estimate; {snapshot.estimatedInstancedDrawCalls:N0} instancing estimate; target <= {snapshot.drawCallTarget}\n" +
                $"- Materials/shaders/variants: {snapshot.uniqueMaterialCount}/{snapshot.uniqueShaderCount}/{snapshot.materialVariantSignatureCount}\n" +
                $"- Instancing: {snapshot.instancingEnabledMaterialCount} materials; {snapshot.instancingEligibleRendererCount} renderers\n" +
                $"- LOD: {snapshot.lodGroupCount} groups; {snapshot.renderersManagedByLod}/{snapshot.rendererCount} renderers covered\n" +
                $"- Shadows: {snapshot.shadowCasterCount} casters; {snapshot.shadowReceiverCount} receivers\n" +
                $"- Textures: {snapshot.textureCount}; {snapshot.mipmappedTextureCount} mipmapped; {snapshot.anisotropicTextureCount} anisotropic; ~{snapshot.estimatedTextureRuntimeBytes / (1024f * 1024f):0.0} MiB runtime estimate\n" +
                $"- Budget plausibility: draw calls {(snapshot.drawCallBudgetPlausible ? "PASS" : "FAIL")}; visible triangles {(snapshot.visibleTriangleBudgetPlausible ? "PASS" : "FAIL")}\n\n" +
                $"Limitations: {snapshot.notes}\n";
        }

        private static HashSet<int> CollectLodRendererIds(IEnumerable<LODGroup> groups)
        {
            HashSet<int> ids = new HashSet<int>();
            foreach (LODGroup group in groups)
            {
                foreach (LOD lod in group.GetLODs())
                {
                    foreach (Renderer renderer in lod.renderers)
                    {
                        if (renderer != null) ids.Add(renderer.GetInstanceID());
                    }
                }
            }

            return ids;
        }

        private static Mesh MeshFor(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static long TriangleCount(Mesh mesh)
        {
            if (mesh == null) return 0;
            long indices = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++) indices += (long)mesh.GetIndexCount(submesh);
            return indices / 3L;
        }

        private static void CollectTextures(Material material, ISet<Texture> textures)
        {
            foreach (string propertyName in material.GetTexturePropertyNames())
            {
                Texture texture = material.GetTexture(propertyName);
                if (texture != null) textures.Add(texture);
            }
        }

        private static string MaterialVariantSignature(Material material)
        {
            string shader = material.shader != null ? material.shader.name : "missing";
            string keywords = string.Join(";", material.shaderKeywords.OrderBy(keyword => keyword, StringComparer.Ordinal));
            return $"{shader}|{material.renderQueue}|{keywords}";
        }

        private readonly struct DrawKey : IEquatable<DrawKey>
        {
            private readonly int meshId;
            private readonly int materialId;
            private readonly int submesh;
            public readonly bool instanced;

            public DrawKey(Mesh mesh, Material material, int submesh, bool instanced)
            {
                meshId = mesh != null ? mesh.GetInstanceID() : 0;
                materialId = material != null ? material.GetInstanceID() : 0;
                this.submesh = submesh;
                this.instanced = instanced;
            }

            public bool Equals(DrawKey other)
            {
                return meshId == other.meshId && materialId == other.materialId && submesh == other.submesh && instanced == other.instanced;
            }

            public override bool Equals(object obj)
            {
                return obj is DrawKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = meshId;
                    hash = (hash * 397) ^ materialId;
                    hash = (hash * 397) ^ submesh;
                    hash = (hash * 397) ^ (instanced ? 1 : 0);
                    return hash;
                }
            }
        }
    }
}
