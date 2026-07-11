using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.Environment
{
    [Serializable]
    public class EnvironmentRenderOptimizationReport
    {
        public string generatedUtc;
        public string rootName;
        public int rendererCount;
        public int materialCount;
        public int instancedMaterialCountBefore;
        public int instancedMaterialCountAfter;
        public int mipmappedTextureCount;
        public int anisotropicTexturesUpgraded;
        public int shadowCastersDisabled;
        public int shadowReceiversDisabled;
        public int motionVectorRenderersDisabled;
        public int lodGroupsBefore;
        public int lodGroupsAfter;
        public int duplicateLodGroupsRepaired;
        public int treeLodGroupsAdded;
        public int distanceCullingGroupsAdded;
        public int renderersCoveredByLod;
        public List<string> warnings = new List<string>();
    }

    /// <summary>
    /// Applies reversible-at-load Quest rendering policy to static environment geometry.
    /// It never touches the aircraft, cockpit, XR rig, camera, or experimental splat renderer.
    /// </summary>
    public static class QuestEnvironmentRenderOptimizer
    {
        public const int MinimumAnisotropy = 4;
        public const float TreeHighDetailScreenHeight = 0.08f;
        public const float TreeLowDetailScreenHeight = 0.025f;

        private static readonly string[] ShadowCasterTokens =
        {
            "hangar", "industrial", "building", "windsock", "fuelisland", "fuelpump", "taxisign"
        };

        private static readonly string[] DistanceCullTokens =
        {
            "hairlinecrack", "expansionjoint", "concretejoint", "tiedownring", "tiedownstripe",
            "taxicone", "qualitygatecone", "ramptiedownnumber", "ramptaxileaddash", "parkingt_",
            "runwaypatch_", "touchdownrubber", "grassvariation", "runwayshoulderwear"
        };

        public static GameObject FindPrimaryEnvironmentRoot()
        {
            GameObject airport = GameObject.Find(MeshSceneryProvider.AirportRootName);
            if (airport != null) return airport;

            WorldPerformanceBudget budget = UnityEngine.Object.FindFirstObjectByType<WorldPerformanceBudget>();
            return budget != null ? budget.gameObject : null;
        }

        public static EnvironmentRenderOptimizationReport OptimizeScene()
        {
            GameObject root = FindPrimaryEnvironmentRoot();
            if (root != null) return OptimizeRoot(root);

            return new EnvironmentRenderOptimizationReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                rootName = "missing",
                warnings = { "No mesh/environment root was available when optimization ran." }
            };
        }

        public static EnvironmentRenderOptimizationReport OptimizeRoot(GameObject root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            HashSet<Material> materials = CollectMaterials(renderers);
            EnvironmentRenderOptimizationReport report = new EnvironmentRenderOptimizationReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                rootName = root.name,
                rendererCount = renderers.Length,
                materialCount = materials.Count,
                instancedMaterialCountBefore = materials.Count(material => material != null && material.enableInstancing),
                lodGroupsBefore = root.GetComponentsInChildren<LODGroup>(true).Length
            };

            ConfigureMaterials(materials, report);
            ConfigureRenderers(renderers, report);
            RepairDuplicateLodGroups(root, report);
            AddTreeLodGroups(root, report);
            AddDistanceCullingGroups(root, report);

            report.instancedMaterialCountAfter = materials.Count(material => material != null && material.enableInstancing);
            LODGroup[] lodGroups = root.GetComponentsInChildren<LODGroup>(true);
            report.lodGroupsAfter = lodGroups.Length;
            report.renderersCoveredByLod = CollectLodRendererIds(lodGroups).Count;

            Debug.Log(
                $"[QuestFlightLab][RenderOptimizer] root={report.rootName} renderers={report.rendererCount} " +
                $"instancedMaterials={report.instancedMaterialCountBefore}->{report.instancedMaterialCountAfter} " +
                $"lodGroups={report.lodGroupsBefore}->{report.lodGroupsAfter} shadowsDisabled={report.shadowCastersDisabled}");
            return report;
        }

        public static float RecommendedCullScreenHeight(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) return 0f;
            string normalized = objectName.ToLowerInvariant();
            if (normalized.Contains("hairlinecrack") || normalized.Contains("tiedownring")) return 0.015f;
            if (normalized.Contains("taxicone") || normalized.Contains("qualitygatecone")) return 0.010f;
            if (normalized.Contains("expansionjoint") || normalized.Contains("concretejoint")) return 0.006f;
            if (normalized.Contains("touchdownrubber") || normalized.Contains("runwaypatch_")) return 0.003f;
            return DistanceCullTokens.Any(normalized.Contains) ? 0.002f : 0f;
        }

        private static HashSet<Material> CollectMaterials(IEnumerable<Renderer> renderers)
        {
            HashSet<Material> materials = new HashSet<Material>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null) materials.Add(material);
                }
            }

            return materials;
        }

        private static void ConfigureMaterials(IEnumerable<Material> materials, EnvironmentRenderOptimizationReport report)
        {
            HashSet<Texture> textures = new HashSet<Texture>();
            foreach (Material material in materials)
            {
                if (material == null) continue;
                try
                {
                    if (material.shader != null && material.shader.isSupported) material.enableInstancing = true;
                }
                catch (Exception ex)
                {
                    report.warnings.Add($"Could not enable instancing for {material.name}: {ex.Message}");
                }

                foreach (string propertyName in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(propertyName);
                    if (texture != null) textures.Add(texture);
                }
            }

            foreach (Texture texture in textures)
            {
                if (texture.mipmapCount <= 1) continue;
                report.mipmappedTextureCount++;
                if (texture.anisoLevel < MinimumAnisotropy)
                {
                    texture.anisoLevel = MinimumAnisotropy;
                    report.anisotropicTexturesUpgraded++;
                }

                if (texture.filterMode != FilterMode.Trilinear) texture.filterMode = FilterMode.Trilinear;
            }
        }

        private static void ConfigureRenderers(IEnumerable<Renderer> renderers, EnvironmentRenderOptimizationReport report)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.allowOcclusionWhenDynamic = true;
                if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
                {
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    report.motionVectorRenderersDisabled++;
                }

                if (!ShouldCastQuestShadow(renderer) && renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    report.shadowCastersDisabled++;
                }

                if (IsFlatDetail(renderer) && renderer.receiveShadows)
                {
                    renderer.receiveShadows = false;
                    report.shadowReceiversDisabled++;
                }
            }
        }

        private static bool ShouldCastQuestShadow(Renderer renderer)
        {
            string normalized = renderer.name.ToLowerInvariant();
            if (normalized.Contains("far") || normalized.Contains("ridge") || normalized.Contains("terrain")) return false;
            return ShadowCasterTokens.Any(normalized.Contains);
        }

        private static bool IsFlatDetail(Renderer renderer)
        {
            Bounds bounds = renderer.bounds;
            if (bounds.size.y <= 0.30f) return true;
            string normalized = renderer.name.ToLowerInvariant();
            return normalized.Contains("light") || normalized.Contains("mark") || normalized.Contains("paint") ||
                   normalized.Contains("patch") || normalized.Contains("joint") || normalized.Contains("rubber");
        }

        private static void RepairDuplicateLodGroups(GameObject root, EnvironmentRenderOptimizationReport report)
        {
            foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                if (lods.Length < 2) continue;

                HashSet<int> first = RendererIds(lods[0].renderers);
                if (first.Count == 0 || lods.Skip(1).Any(lod => !first.SetEquals(RendererIds(lod.renderers)))) continue;

                float cullHeight = lods[lods.Length - 1].screenRelativeTransitionHeight;
                group.SetLODs(new[] { new LOD(cullHeight, lods[0].renderers) });
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                report.duplicateLodGroupsRepaired++;
            }
        }

        private static void AddTreeLodGroups(GameObject root, EnvironmentRenderOptimizationReport report)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == root.transform || candidate.GetComponent<LODGroup>() != null) continue;
                Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length < 2 || !LooksLikeTree(candidate, renderers)) continue;
                if (HasLodAncestor(candidate, root.transform)) continue;

                Renderer lowDetail = renderers.FirstOrDefault(r => r.name.IndexOf("CanopyLower", StringComparison.OrdinalIgnoreCase) >= 0) ??
                                     renderers.OrderByDescending(r => r.bounds.size.sqrMagnitude).First();
                LODGroup group = candidate.gameObject.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(TreeHighDetailScreenHeight, renderers),
                    new LOD(TreeLowDetailScreenHeight, new[] { lowDetail })
                });
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                report.treeLodGroupsAdded++;
            }
        }

        private static void AddDistanceCullingGroups(GameObject root, EnvironmentRenderOptimizationReport report)
        {
            HashSet<int> managed = CollectLodRendererIds(root.GetComponentsInChildren<LODGroup>(true));
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // Real-data activation hides superseded legacy detail renderers.
                // An enabled LODGroup for an already-hidden renderer is pure culling overhead.
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (managed.Contains(renderer.GetInstanceID()) || renderer.GetComponent<LODGroup>() != null) continue;
                float cullHeight = RecommendedCullScreenHeight(renderer.name);
                if (cullHeight <= 0f) continue;

                LODGroup group = renderer.gameObject.AddComponent<LODGroup>();
                group.SetLODs(new[] { new LOD(cullHeight, new[] { renderer }) });
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                managed.Add(renderer.GetInstanceID());
                report.distanceCullingGroupsAdded++;
            }
        }

        private static bool LooksLikeTree(Transform candidate, IEnumerable<Renderer> renderers)
        {
            if (candidate.name.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return renderers.Any(renderer =>
                renderer.transform.parent == candidate &&
                renderer.name.IndexOf("canopy", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool HasLodAncestor(Transform candidate, Transform stopAt)
        {
            Transform current = candidate.parent;
            while (current != null && current != stopAt)
            {
                if (current.GetComponent<LODGroup>() != null) return true;
                current = current.parent;
            }

            return false;
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

        private static HashSet<int> RendererIds(IEnumerable<Renderer> renderers)
        {
            HashSet<int> ids = new HashSet<int>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null) ids.Add(renderer.GetInstanceID());
            }

            return ids;
        }
    }
}
