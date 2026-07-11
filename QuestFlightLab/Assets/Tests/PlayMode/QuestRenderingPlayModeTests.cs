#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using QuestFlightLab.Environment;
using QuestFlightLab.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.Tests.PlayMode
{
    public class QuestRenderingPlayModeTests
    {
        [Test]
        public void EnvironmentOptimizerEnablesInstancingAndAddsRealLodAndCulling()
        {
            GameObject root = new GameObject("QuestRenderOptimizerTestRoot");
            GameObject cameraObject = new GameObject("QuestRenderOptimizerTestCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 3f, -12f), Quaternion.identity);
            Material material = new Material(Shader.Find("Standard"));
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, true);
            texture.Apply(true, false);
            texture.anisoLevel = 1;
            material.mainTexture = texture;

            try
            {
                GameObject crackA = Primitive(root.transform, "RunwayHairlineCrack_A", new Vector3(-1f, 0f, 2f), material);
                Primitive(root.transform, "RunwayHairlineCrack_B", new Vector3(1f, 0f, 2f), material);
                GameObject hiddenCrack = Primitive(root.transform, "RunwayHairlineCrack_Hidden", new Vector3(2f, 0f, 2f), material);
                hiddenCrack.GetComponent<Renderer>().enabled = false;
                GameObject inactiveCrack = Primitive(root.transform, "RunwayHairlineCrack_Inactive", new Vector3(3f, 0f, 2f), material);
                inactiveCrack.SetActive(false);
                GameObject duplicateLodObject = Primitive(root.transform, "DryFieldPatch_Test", new Vector3(0f, 0f, 5f), material);
                Renderer duplicateRenderer = duplicateLodObject.GetComponent<Renderer>();
                LODGroup duplicateLod = duplicateLodObject.AddComponent<LODGroup>();
                duplicateLod.SetLODs(new[]
                {
                    new LOD(0.18f, new[] { duplicateRenderer }),
                    new LOD(0.04f, new[] { duplicateRenderer })
                });
                duplicateLod.RecalculateBounds();

                GameObject tree = new GameObject("NorthTree_Test");
                tree.transform.SetParent(root.transform, false);
                Primitive(tree.transform, "Trunk", new Vector3(4f, 1f, 5f), material, PrimitiveType.Cylinder);
                Primitive(tree.transform, "CanopyLower", new Vector3(4f, 3f, 5f), material, PrimitiveType.Sphere);
                Primitive(tree.transform, "CanopyUpper", new Vector3(4f, 4.5f, 5f), material, PrimitiveType.Sphere);

                RenderBudgetSnapshot before = QuestRenderBudgetAudit.Capture(camera, root.transform);
                EnvironmentRenderOptimizationReport optimization = QuestEnvironmentRenderOptimizer.OptimizeRoot(root);
                RenderBudgetSnapshot after = QuestRenderBudgetAudit.Capture(camera, root.transform);

                Assert.That(material.enableInstancing, Is.True);
                Assert.That(texture.anisoLevel, Is.GreaterThanOrEqualTo(QuestEnvironmentRenderOptimizer.MinimumAnisotropy));
                Assert.That(optimization.duplicateLodGroupsRepaired, Is.EqualTo(1));
                Assert.That(optimization.treeLodGroupsAdded, Is.EqualTo(1));
                Assert.That(optimization.distanceCullingGroupsAdded, Is.EqualTo(2));
                Assert.That(tree.GetComponent<LODGroup>().GetLODs().Length, Is.EqualTo(2));
                Assert.That(crackA.GetComponent<LODGroup>(), Is.Not.Null);
                Assert.That(hiddenCrack.GetComponent<LODGroup>(), Is.Null);
                Assert.That(inactiveCrack.GetComponent<LODGroup>(), Is.Null);
                Assert.That(after.instancingEnabledMaterialCount, Is.GreaterThan(before.instancingEnabledMaterialCount));
                Assert.That(after.estimatedInstancedDrawCalls, Is.LessThan(after.estimatedDrawCallsWithoutBatching));
                Assert.That(after.renderersManagedByLod, Is.GreaterThanOrEqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void PerformanceProbePercentileUsesNearestRank()
        {
            float[] values = { 8f, 10f, 12f, 14f, 30f };
            Assert.That(SceneryPerformanceProbe.CalculatePercentile(values, values.Length, 0.50f), Is.EqualTo(12f));
            Assert.That(SceneryPerformanceProbe.CalculatePercentile(values, values.Length, 0.95f), Is.EqualTo(30f));
            Assert.That(SceneryPerformanceProbe.CalculatePercentile(null, 0, 0.95f), Is.Zero);
        }

        [Test]
        public void DistanceCullPolicyPreservesLargeFeaturesAndCullsTinyDetail()
        {
            Assert.That(QuestEnvironmentRenderOptimizer.RecommendedCullScreenHeight("RunwayHairlineCrack_3"), Is.EqualTo(0.015f));
            Assert.That(QuestEnvironmentRenderOptimizer.RecommendedCullScreenHeight("QualityGateCone_2"), Is.EqualTo(0.010f));
            Assert.That(QuestEnvironmentRenderOptimizer.RecommendedCullScreenHeight("BaselineHangar_0_Body"), Is.Zero);
            Assert.That(QuestEnvironmentRenderOptimizer.RecommendedCullScreenHeight("FrontRangeMountainBackRidge"), Is.Zero);
        }

        [Test]
        public void CockpitLightingNeverUsesRealtimeShadowMapAndNeutralizesCoarseInteriorMaps()
        {
            GameObject root = new GameObject("CockpitLightingPolicyTestRoot");
            Material opaque = new Material(Shader.Find("Standard")) { name = "Interior_Body_MAT" };
            Material detail = new Material(Shader.Find("Standard")) { name = "ButtonPlate_MAT" };
            Material glass = new Material(Shader.Find("Standard"))
            {
                name = "MI_Glass1",
                color = new Color(0f, 0f, 0f, 1f)
            };
            Material configuredGlass = null;
            Material configuredOpaque = null;
            Texture2D occlusion = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            Texture2D coarseNormal = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            occlusion.Apply(true, false);
            coarseNormal.Apply(true, false);
            opaque.SetTexture("_OcclusionMap", occlusion);
            opaque.SetFloat("_OcclusionStrength", 1f);
            opaque.EnableKeyword("_OCCLUSIONMAP");
            opaque.SetTexture("_BumpMap", coarseNormal);
            opaque.EnableKeyword("_NORMALMAP");

            try
            {
                Renderer body = Primitive(root.transform, "Cessna_Interior_Body", Vector3.zero, opaque)
                    .GetComponent<Renderer>();
                Renderer button = Primitive(root.transform, "Dynamic_Switch_001", Vector3.right, detail)
                    .GetComponent<Renderer>();
                Renderer windowA = Primitive(root.transform, "LeftWindowGlass", Vector3.left, glass)
                    .GetComponent<Renderer>();
                Renderer windowB = Primitive(root.transform, "RightWindowGlass", Vector3.up, glass)
                    .GetComponent<Renderer>();

                CockpitLightingReport report = QuestCockpitLightingPolicy.ConfigureImportedAircraft(root);
                configuredGlass = windowA.sharedMaterial;
                configuredOpaque = body.sharedMaterial;

                Assert.That(body.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(body.receiveShadows, Is.False);
                Assert.That(button.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(button.receiveShadows, Is.False);
                Assert.That(windowA.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(windowA.receiveShadows, Is.False);
                Assert.That(windowB.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(windowB.receiveShadows, Is.False);
                Assert.That(body.reflectionProbeUsage, Is.EqualTo(ReflectionProbeUsage.BlendProbesAndSkybox));
                Assert.That(windowA.reflectionProbeUsage, Is.EqualTo(ReflectionProbeUsage.BlendProbesAndSkybox));
                Assert.That(configuredOpaque, Is.Not.SameAs(opaque));
                Assert.That(configuredOpaque.GetTexture("_OcclusionMap"), Is.Null);
                Assert.That(configuredOpaque.GetFloat("_OcclusionStrength"), Is.Zero);
                Assert.That(configuredOpaque.IsKeywordEnabled("_OCCLUSIONMAP"), Is.False);
                Assert.That(configuredOpaque.GetTexture("_BumpMap"), Is.Null);
                Assert.That(configuredOpaque.IsKeywordEnabled("_NORMALMAP"), Is.False);
                Assert.That(QuestCockpitLightingPolicy.HasActiveOcclusion(configuredOpaque), Is.False);
                Assert.That(configuredGlass, Is.Not.SameAs(glass));
                Assert.That(windowB.sharedMaterial, Is.SameAs(configuredGlass));
                Assert.That(configuredGlass.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
                Assert.That(configuredGlass.color.a, Is.LessThanOrEqualTo(QuestCockpitLightingPolicy.MaximumGlassAlpha));
                Assert.That(report.realtimeShadowCasterCountDisabled, Is.EqualTo(4));
                Assert.That(report.realtimeShadowReceiverCountDisabled, Is.EqualTo(4));
                Assert.That(report.opaqueMaterialCloneCount, Is.EqualTo(1));
                Assert.That(report.occlusionMaterialCountNeutralized, Is.EqualTo(1));
                Assert.That(report.coarseNormalMaterialCountNeutralized, Is.EqualTo(1));
                Assert.That(report.remainingRealtimeShadowCasterCount, Is.Zero);
                Assert.That(report.remainingRealtimeShadowReceiverCount, Is.Zero);
                Assert.That(report.remainingActiveOcclusionMaterialCount, Is.Zero);
                Assert.That(report.stableMajorShadowCasterCount, Is.Zero);
                Assert.That(report.shadowReceiverCount, Is.Zero);
                Assert.That(report.microDetailShadowCasterCountDisabled, Is.EqualTo(1));
                Assert.That(report.glassShadowCasterCountDisabled, Is.EqualTo(2));
                Assert.That(report.glassMaterialCloneCount, Is.EqualTo(1));

                Material firstOpaque = configuredOpaque;
                Material firstGlass = configuredGlass;
                CockpitLightingReport secondReport = QuestCockpitLightingPolicy.ConfigureImportedAircraft(root);
                Assert.That(body.sharedMaterial, Is.SameAs(firstOpaque));
                Assert.That(windowA.sharedMaterial, Is.SameAs(firstGlass));
                Assert.That(secondReport.opaqueMaterialCloneCount, Is.Zero);
                Assert.That(secondReport.glassMaterialCloneCount, Is.Zero);
                Assert.That(secondReport.remainingRealtimeShadowCasterCount, Is.Zero);
                Assert.That(secondReport.remainingRealtimeShadowReceiverCount, Is.Zero);
                Assert.That(secondReport.remainingActiveOcclusionMaterialCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (configuredGlass != null && configuredGlass != glass) Object.DestroyImmediate(configuredGlass);
                if (configuredOpaque != null && configuredOpaque != opaque) Object.DestroyImmediate(configuredOpaque);
                Object.DestroyImmediate(opaque);
                Object.DestroyImmediate(detail);
                Object.DestroyImmediate(glass);
                Object.DestroyImmediate(occlusion);
                Object.DestroyImmediate(coarseNormal);
            }
        }

        [Test]
        public void CockpitStaticVertexDepthIsBoundedDeterministicAndAddsNoMaterial()
        {
            Shader gltfShader = Shader.Find("glTF/PbrMetallicRoughness");
            Assert.That(gltfShader, Is.Not.Null, "The imported cockpit's built-in glTF shader must be available.");

            GameObject root = new GameObject("CockpitStaticDepthTestRoot");
            GameObject seatObject = new GameObject("Cessna_Interior_Seats_MAT_0");
            seatObject.transform.SetParent(root.transform, false);
            Mesh mesh = new Mesh { name = "CockpitStaticDepthTestMesh" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 0.5f),
                new Vector3(-0.5f, 2f, 0f), new Vector3(0.5f, 2f, 0f), new Vector3(0f, 2f, 0.5f)
            };
            mesh.normals = new[]
            {
                Vector3.down, Vector3.down, Vector3.down,
                Vector3.up, Vector3.up, Vector3.up
            };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            mesh.RecalculateBounds();
            seatObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = seatObject.AddComponent<MeshRenderer>();
            Material material = new Material(gltfShader) { name = "Seats_MAT" };
            renderer.sharedMaterial = material;

            try
            {
                CockpitLightingReport report = QuestCockpitLightingPolicy.ConfigureImportedAircraft(
                    root,
                    QuestCockpitLightingPolicy.DefaultStaticDepthStrength);
                Color32[] colors = mesh.colors32;

                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                Assert.That(colors.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(colors[0].r, Is.LessThan(colors[3].r),
                    "Lower/downward cabin vertices should receive stronger static depth than upper/upward vertices.");
                Assert.That(report.staticDepthRendererCount, Is.EqualTo(1));
                Assert.That(report.staticDepthMeshCount, Is.EqualTo(1));
                Assert.That(report.staticDepthMeshWriteCount, Is.EqualTo(1));
                Assert.That(report.staticDepthVertexCount, Is.EqualTo(6));
                Assert.That(report.staticDepthUnreadableMeshCount, Is.Zero);
                Assert.That(report.staticDepthMinimumVertexFactor,
                    Is.InRange(1f - QuestCockpitLightingPolicy.MaximumStaticDepthStrength, 1f));
                Assert.That(report.remainingRealtimeShadowCasterCount, Is.Zero);
                Assert.That(report.remainingRealtimeShadowReceiverCount, Is.Zero);

                Color32[] firstColors = (Color32[])colors.Clone();
                CockpitLightingReport second = QuestCockpitLightingPolicy.ConfigureImportedAircraft(
                    root,
                    QuestCockpitLightingPolicy.DefaultStaticDepthStrength);
                Assert.That(second.staticDepthMeshWriteCount, Is.Zero);
                Assert.That(mesh.colors32, Is.EqualTo(firstColors), "The one-time vertex bake must be idempotent.");

                float lowerFactor = QuestCockpitLightingPolicy.CalculateStaticDepthVertexFactor(
                    0f, -1f, 1f, QuestCockpitLightingPolicy.DefaultStaticDepthStrength);
                float upperFactor = QuestCockpitLightingPolicy.CalculateStaticDepthVertexFactor(
                    1f, 1f, 0f, QuestCockpitLightingPolicy.DefaultStaticDepthStrength);
                Assert.That(lowerFactor, Is.LessThan(upperFactor));
                Assert.That(QuestCockpitLightingPolicy.CalculateStaticDepthVertexFactor(0f, -1f, 1f, 0f),
                    Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void RenderProfileUsesStableCascadeSkyReflectionAndFullTerrainFarClip()
        {
            GameObject cameraObject = new GameObject("QuestRenderProfileTestCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 1000f;

            try
            {
                RenderQualityEvidence evidence = QuestRenderQualityConfigurator.ApplyProfile("render_profile_test");

                Assert.That(QualitySettings.shadowCascades, Is.EqualTo(2));
                Assert.That(QualitySettings.shadowCascade2Split,
                    Is.EqualTo(QuestRenderQualityConfigurator.EditorNearShadowCascadeFraction).Within(0.0001f));
                Assert.That(QualitySettings.shadowProjection, Is.EqualTo(ShadowProjection.StableFit));
                Assert.That(camera.farClipPlane,
                    Is.GreaterThanOrEqualTo(QuestRenderQualityConfigurator.MinimumCameraFarClipMeters));
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Skybox));
                Assert.That(RenderSettings.defaultReflectionMode, Is.EqualTo(DefaultReflectionMode.Skybox));
                Assert.That(RenderSettings.defaultReflectionResolution,
                    Is.EqualTo(QuestRenderQualityConfigurator.StableSkyReflectionResolution));
                Assert.That(QualitySettings.realtimeReflectionProbes, Is.False);
                Assert.That(evidence.shadowCascade2Split,
                    Is.EqualTo(QuestRenderQualityConfigurator.EditorNearShadowCascadeFraction).Within(0.0001f));
                Assert.That(evidence.cameraFarClipMeters,
                    Is.GreaterThanOrEqualTo(QuestRenderQualityConfigurator.MinimumCameraFarClipMeters));

                Assert.That(QuestRenderQualityConfigurator.QuestEyeTextureResolutionScale, Is.EqualTo(1f));
                Assert.That(QuestRenderQualityConfigurator.QuestLodBias, Is.EqualTo(1.25f));
                Assert.That(QuestRenderQualityConfigurator.QuestShadowDistanceMeters, Is.InRange(35f, 45f));
                Assert.That(QuestRenderQualityConfigurator.QuestFixedFoveationLevel, Is.EqualTo(0.45f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static GameObject Primitive(
            Transform parent,
            string name,
            Vector3 position,
            Material material,
            PrimitiveType primitiveType = PrimitiveType.Cube)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return go;
        }
    }
}
#endif
