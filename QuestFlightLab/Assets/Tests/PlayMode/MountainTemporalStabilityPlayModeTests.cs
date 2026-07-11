#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using QuestFlightLab.Environment;
using UnityEngine;

namespace QuestFlightLab.Tests
{
    public sealed class MountainTemporalStabilityPlayModeTests
    {
        [Test]
        public void RealTerrainUsesOneFixedRefinedSourceDuringHeadSweep()
        {
            GameObject airport = KbduApproxAirport.Build(null);
            GameObject cameraObject = new GameObject("MountainProbeTestCamera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 18000f;
            cameraObject.transform.position = new Vector3(0f, 2f, 0f);
            try
            {
                GameObject world = KbduInspiredWorldBuilder.AddWorld(airport.transform);
                Assert.That(world.name, Is.EqualTo(RealKbduEnvironmentBuilder.RootName));
                MountainTemporalStabilityProbe probe = world.GetComponent<MountainTemporalStabilityProbe>();
                Assert.That(probe, Is.Not.Null);

                Transform far = world.transform.Find("RealTerrain_far_24km");
                Assert.That(far, Is.Not.Null);
                Mesh farMesh = far.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(farMesh.name, Does.Contain("Stable200m"));
                Assert.That(farMesh.vertexCount, Is.GreaterThan(10000),
                    "The original 400 m far grid is deterministically refined to a stable 200 m render mesh.");
                Assert.That(farMesh.triangles.Length / 3, Is.EqualTo(21680));
                Assert.That(far.GetComponent<LODGroup>(), Is.Null);
                Assert.That(far.GetComponent<RealKbduBatchDistanceCuller>(), Is.Null);

                probe.automaticCaptureSeconds = 1f;
                probe.BeginCapture(camera);
                for (int index = 0; index < 9; index++)
                {
                    cameraObject.transform.rotation = Quaternion.Euler(
                        Mathf.Lerp(-35f, 35f, index / 8f),
                        Mathf.Lerp(-45f, 45f, index / 8f),
                        0f);
                    probe.CaptureFrameNow();
                }
                MountainTemporalStabilityReport report = probe.EndCapture(false);

                Assert.That(report.passed, Is.True, string.Join("\n", report.violations));
                Assert.That(report.sampledFrameCount, Is.EqualTo(9));
                Assert.That(report.expectedRendererCount, Is.EqualTo(4));
                Assert.That(report.rendererSampleCount, Is.EqualTo(36));
                Assert.That(report.immutableTransforms, Is.True);
                Assert.That(report.immutableMeshes, Is.True);
                Assert.That(report.immutableRendererSet, Is.True);
                Assert.That(report.noTerrainLodOrDither, Is.True);
                Assert.That(report.noCameraFacingOrDistanceScaling, Is.True);
                Assert.That(report.oneMountainSource, Is.True);

                TestContext.WriteLine(
                    $"Mountain machine sweep PASS frames={report.sampledFrameCount} samples={report.rendererSampleCount} " +
                    $"farMeshVertices={farMesh.vertexCount} farMeshTriangles={farMesh.triangles.Length / 3} " +
                    $"classification={report.classification}");

                AssertSharedStableNormals(world.transform, "mid_12km", "far_24km", 6000f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(airport);
            }
        }

        [Test]
        public void ProbeFailsTransformMeshAndRendererMutations()
        {
            GameObject environment = new GameObject("MountainMutationEnvironment");
            GameObject realRoot = new GameObject(RealKbduEnvironmentBuilder.RootName);
            realRoot.transform.SetParent(environment.transform, false);
            GameObject mid = CreateTerrainRenderer(realRoot.transform, "RealTerrain_mid_12km", 10f);
            GameObject far = CreateTerrainRenderer(realRoot.transform, "RealTerrain_far_24km", 20f);
            GameObject cameraObject = new GameObject("MountainMutationCamera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            try
            {
                MountainTemporalStabilityProbe probe = realRoot.AddComponent<MountainTemporalStabilityProbe>();
                probe.Initialize(realRoot.transform, environment.transform);
                probe.BeginCapture(camera);
                probe.CaptureFrameNow();

                far.transform.localScale = new Vector3(1.1f, 1f, 1f);
                far.GetComponent<MeshFilter>().sharedMesh = BuildQuadMesh("ReplacementFarMesh", 25f);
                mid.GetComponent<Renderer>().enabled = false;
                probe.CaptureFrameNow();
                MountainTemporalStabilityReport report = probe.EndCapture(false);

                Assert.That(report.passed, Is.False);
                Assert.That(report.immutableTransforms, Is.False);
                Assert.That(report.immutableMeshes, Is.False);
                Assert.That(report.immutableRendererSet, Is.False);
                Assert.That(report.violations, Has.Some.Contains("Transform/matrix changed"));
                Assert.That(report.violations, Has.Some.Contains("Mesh identity/vertex count changed"));
                Assert.That(report.violations, Has.Some.Contains("Enabled/active renderer state changed"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(environment);
            }
        }

        [Test]
        public void RealAndProceduralMountainRootsAreMutuallyExclusiveAndFallbackRidgesAreFixed()
        {
            string previous = System.Environment.GetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU");
            GameObject airport = KbduApproxAirport.Build(null);
            GameObject rogueLegacy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rogueLegacy.name = "FrontRangeLegacyRidgeOutsideFallback";
            rogueLegacy.transform.SetParent(airport.transform, false);
            try
            {
                System.Environment.SetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU", "1");
                GameObject fallback = KbduInspiredWorldBuilder.AddWorld(airport.transform);
                Assert.That(fallback.name, Is.EqualTo(RealKbduEnvironmentBuilder.ProceduralFallbackRootName));
                Assert.That(fallback.activeSelf, Is.True);
                Assert.That(fallback.transform.Find("FrontRangeLowAtmosphericHazeBand"), Is.Null);
                Assert.That(fallback.transform.Find("FrontRangeBackAtmosphericHazeBand"), Is.Null);
                foreach (Transform child in fallback.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.name.StartsWith("FrontRange", StringComparison.Ordinal)) continue;
                    Assert.That(child.GetComponent<LODGroup>(), Is.Null,
                        $"Fallback ridge must remain a fixed mesh: {child.name}");
                }

                System.Environment.SetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU", null);
                GameObject real = KbduInspiredWorldBuilder.AddWorld(airport.transform);
                Assert.That(real.name, Is.EqualTo(RealKbduEnvironmentBuilder.RootName));
                Assert.That(real.activeSelf, Is.True);
                Assert.That(fallback.activeSelf, Is.False);
                Assert.That(rogueLegacy.activeSelf, Is.False);
                Assert.That(rogueLegacy.GetComponent<Renderer>().enabled, Is.False);

                System.Environment.SetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU", "1");
                GameObject selectedFallback = KbduInspiredWorldBuilder.AddWorld(airport.transform);
                Assert.That(selectedFallback, Is.SameAs(fallback));
                Assert.That(fallback.activeSelf, Is.True);
                Assert.That(real.activeSelf, Is.False);

                System.Environment.SetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU", null);
                GameObject selectedReal = KbduInspiredWorldBuilder.AddWorld(airport.transform);
                Assert.That(selectedReal, Is.SameAs(real));
                Assert.That(real.activeSelf, Is.True);
                Assert.That(fallback.activeSelf, Is.False);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU", previous);
                UnityEngine.Object.DestroyImmediate(airport);
            }
        }

        private static GameObject CreateTerrainRenderer(Transform parent, string name, float size)
        {
            GameObject terrain = new GameObject(name);
            terrain.transform.SetParent(parent, false);
            terrain.isStatic = true;
            terrain.AddComponent<MeshFilter>().sharedMesh = BuildQuadMesh(name + "Mesh", size);
            MeshRenderer renderer = terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Standard"));
            return terrain;
        }

        private static Mesh BuildQuadMesh(string name, float size)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(-size, 0f, -size),
                new Vector3(-size, 0f, size),
                new Vector3(size, 0f, -size),
                new Vector3(size, 0f, size)
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AssertSharedStableNormals(
            Transform world,
            string innerId,
            string outerId,
            float radius)
        {
            Mesh inner = world.Find("RealTerrain_" + innerId).GetComponent<MeshFilter>().sharedMesh;
            Mesh outer = world.Find("RealTerrain_" + outerId).GetComponent<MeshFilter>().sharedMesh;
            Vector3[] innerVertices = inner.vertices;
            Vector3[] innerNormals = inner.normals;
            Vector3[] outerVertices = outer.vertices;
            Vector3[] outerNormals = outer.normals;
            Dictionary<string, Vector3> outerBoundaryNormals = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            for (int index = 0; index < outerVertices.Length; index++)
            {
                Vector3 vertex = outerVertices[index];
                if (!OnSquareBoundary(vertex, radius)) continue;
                outerBoundaryNormals[PositionKey(vertex)] = outerNormals[index];
            }

            int checkedNormals = 0;
            for (int index = 0; index < innerVertices.Length; index++)
            {
                Vector3 vertex = innerVertices[index];
                if (!OnSquareBoundary(vertex, radius)) continue;
                if (!outerBoundaryNormals.TryGetValue(PositionKey(vertex), out Vector3 outerNormal)) continue;
                Assert.That(Vector3.Angle(innerNormals[index], outerNormal), Is.LessThan(0.01f),
                    $"Stable seam normal mismatch at {vertex}");
                checkedNormals++;
            }
            Assert.That(checkedNormals, Is.GreaterThan(100));
        }

        private static bool OnSquareBoundary(Vector3 vertex, float radius)
        {
            return Mathf.Abs(Mathf.Abs(vertex.x) - radius) <= 0.02f && Mathf.Abs(vertex.z) <= radius + 0.02f ||
                   Mathf.Abs(Mathf.Abs(vertex.z) - radius) <= 0.02f && Mathf.Abs(vertex.x) <= radius + 0.02f;
        }

        private static string PositionKey(Vector3 position)
        {
            return $"{Mathf.RoundToInt(position.x * 10f)}:{Mathf.RoundToInt(position.y * 10f)}:{Mathf.RoundToInt(position.z * 10f)}";
        }
    }
}
#endif
