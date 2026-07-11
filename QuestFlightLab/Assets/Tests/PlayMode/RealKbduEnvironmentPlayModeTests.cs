#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using QuestFlightLab.Environment;
using UnityEngine;

namespace QuestFlightLab.Tests
{
    public class RealKbduEnvironmentPlayModeTests
    {
        [Test]
        public void RealKbduAssetsBuildIntoBoundedBatchesAndSlopedFaaRunway()
        {
            Assert.That(Resources.Load<TextAsset>(RealKbduEnvironmentBuilder.TerrainResourcePath), Is.Not.Null);
            Assert.That(Resources.Load<TextAsset>(RealKbduEnvironmentBuilder.ContextResourcePath), Is.Not.Null);
            GameObject airport = KbduApproxAirport.Build(null);
            try
            {
                GameObject world = KbduInspiredWorldBuilder.AddWorld(airport.transform);
                Assert.That(world, Is.Not.Null, RealKbduEnvironmentBuilder.LastBuildMessage);
                Assert.That(world.name, Is.EqualTo(RealKbduEnvironmentBuilder.RootName));
                RealKbduEnvironmentStatus status = world.GetComponent<RealKbduEnvironmentStatus>();
                Assert.That(status, Is.Not.Null);
                Assert.That(status.dataValidated, Is.True);
                Assert.That(status.profileName, Is.EqualTo("real_kbdu_usgs_faa_osm_v1"));
                Assert.That(status.terrainLayerCount, Is.EqualTo(4));
                Assert.That(status.terrainHeightSamples, Is.EqualTo(30304));
                Assert.That(status.sourceVectorFeatures, Is.GreaterThan(5000));
                Assert.That(status.renderedVectorFeatures, Is.GreaterThan(1000));
                Assert.That(status.runtimeBatchCount, Is.LessThanOrEqualTo(RealKbduEnvironmentBuilder.MaximumVectorBatches));
                Assert.That(status.rendererCount, Is.LessThanOrEqualTo(RealKbduEnvironmentBuilder.MaximumExpectedRenderers));
                Assert.That(status.rendererCount * 20, Is.LessThan(status.sourceVectorFeatures));
                Assert.That(status.triangleCount, Is.LessThanOrEqualTo(RealKbduEnvironmentBuilder.MaximumExpectedTriangles));
                Assert.That(status.materialCount, Is.LessThanOrEqualTo(20));
                Assert.That(status.textureCount, Is.EqualTo(1));
                Assert.That(status.osmAttribution, Is.EqualTo("© OpenStreetMap contributors"));
                Assert.That(status.imageryStatus, Is.EqualTo("available_but_not_committed"));

                AssertTerrainSeamIsSharedAndViewStable(world.transform, "inner_4km", "mid_12km", 2000f);
                AssertTerrainSeamIsSharedAndViewStable(world.transform, "mid_12km", "far_24km", 6000f);
                foreach (Transform terrain in world.GetComponentsInChildren<Transform>(true))
                {
                    if (!terrain.name.StartsWith("RealTerrain_", StringComparison.Ordinal)) continue;
                    Assert.That(terrain.GetComponent<LODGroup>(), Is.Null,
                        $"{terrain.name} must remain a single fixed mesh, not a view-dependent LOD swap.");
                    Assert.That(terrain.GetComponent<RealKbduBatchDistanceCuller>(), Is.Null,
                        $"{terrain.name} must not toggle with camera distance.");
                }

                Vector3 end08 = RealKbduEnvironmentBuilder.LastPavedRunway08EndLocal;
                Vector3 end26 = RealKbduEnvironmentBuilder.LastPavedRunway26EndLocal;
                Assert.That(Vector2.Distance(new Vector2(end08.x, end08.z), new Vector2(end26.x, end26.z)), Is.EqualTo(1250.39f).Within(3f));
                Assert.That(end26.y - end08.y, Is.EqualTo(-2.919f).Within(0.05f));
                Assert.That(RealKbduEnvironmentBuilder.LastPavedRunwayCenterLocal.z, Is.EqualTo(-30.36f).Within(0.2f));
                Assert.That(
                    RealKbduEnvironmentBuilder.TryGetRecommendedPavedRunwayStart(
                        out Vector3 runwayStart,
                        out Quaternion runwayHeading),
                    Is.True);
                Assert.That(runwayStart.x, Is.EqualTo(-559.76f).Within(0.05f));
                Assert.That(runwayStart.y, Is.EqualTo(1.10f).Within(0.03f));
                Assert.That(runwayStart.z, Is.EqualTo(-30.64f).Within(0.05f));
                Assert.That(runwayHeading.eulerAngles.y, Is.EqualTo(89.97f).Within(0.05f));

                Transform legacyRunway = airport.transform.Find("Runway_08_26_Approx_4100x75ft");
                Assert.That(legacyRunway, Is.Not.Null);
                Assert.That(legacyRunway.GetComponent<Renderer>().enabled, Is.False, "Legacy flat pavement must not z-fight the FAA/USGS slope.");
                Assert.That(legacyRunway.GetComponent<Collider>().enabled, Is.True, "Prototype ground-physics fallback collider remains available.");

                WorldPerformanceBudget budget = world.GetComponent<WorldPerformanceBudget>();
                Assert.That(budget, Is.Not.Null);
                Assert.That(budget.profileName, Is.EqualTo(status.profileName));
                Assert.That(budget.worldSizeMeters, Is.EqualTo(new Vector2(24000f, 24000f)));
                TestContext.WriteLine(status.Summary);
                TestContext.WriteLine(status.faaRunwaySummary);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(airport);
            }
        }

        [Test]
        public void ProceduralKbduFallbackRemainsExplicitlySelectable()
        {
            string previous = System.Environment.GetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU");
            System.Environment.SetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU", "1");
            GameObject airport = KbduApproxAirport.Build(null);
            try
            {
                GameObject world = KbduInspiredWorldBuilder.AddWorld(airport.transform);
                Assert.That(world, Is.Not.Null);
                Assert.That(world.name, Is.EqualTo("KBDU_Inspired_Expanded_World_NotForNavigation"));
                Assert.That(world.GetComponent<WorldPerformanceBudget>(), Is.Not.Null);
                Assert.That(RealKbduEnvironmentBuilder.LastBuildUsedRealData, Is.False);

                // A fallback can exist before real resources become available in a long-running
                // editor session. Promoting to real data must not leave its synthetic Front Range
                // ridges rendering over the USGS terrain.
                System.Environment.SetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU", null);
                GameObject realWorld = KbduInspiredWorldBuilder.AddWorld(airport.transform);
                Assert.That(realWorld.name, Is.EqualTo(RealKbduEnvironmentBuilder.RootName));
                Assert.That(world.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
                Assert.That(world.activeSelf, Is.False,
                    "The preserved procedural fallback must be inactive whenever real terrain is active.");
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU", previous);
                UnityEngine.Object.DestroyImmediate(airport);
            }
        }

        private static void AssertTerrainSeamIsSharedAndViewStable(
            Transform world,
            string innerId,
            string outerId,
            float radius)
        {
            Transform inner = world.Find("RealTerrain_" + innerId);
            Transform outer = world.Find("RealTerrain_" + outerId);
            Assert.That(inner, Is.Not.Null);
            Assert.That(outer, Is.Not.Null);

            Vector3[] innerVertices = inner.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3[] outerVertices = outer.GetComponent<MeshFilter>().sharedMesh.vertices;
            Assert.That(outer.GetComponent<MeshFilter>().sharedMesh.name, Does.Contain("StitchedMesh"));

            foreach (Vector3 vertex in outerVertices)
            {
                Assert.That(Mathf.Max(Mathf.Abs(vertex.x), Mathf.Abs(vertex.z)),
                    Is.GreaterThanOrEqualTo(radius - 0.02f),
                    $"{outerId} contains geometry underneath {innerId} at {vertex}.");
            }

            int sharedBoundaryVertices = 0;
            foreach (Vector3 innerVertex in innerVertices)
            {
                bool onBoundary = Mathf.Abs(Mathf.Abs(innerVertex.x) - radius) <= 0.02f &&
                                  Mathf.Abs(innerVertex.z) <= radius + 0.02f ||
                                  Mathf.Abs(Mathf.Abs(innerVertex.z) - radius) <= 0.02f &&
                                  Mathf.Abs(innerVertex.x) <= radius + 0.02f;
                if (!onBoundary) continue;

                bool shared = Array.Exists(outerVertices, outerVertex =>
                    Mathf.Abs(outerVertex.x - innerVertex.x) <= 0.02f &&
                    Mathf.Abs(outerVertex.y - innerVertex.y) <= 0.02f &&
                    Mathf.Abs(outerVertex.z - innerVertex.z) <= 0.02f);
                Assert.That(shared, Is.True,
                    $"Missing shared seam vertex for {innerId}->{outerId}: {innerVertex}");
                sharedBoundaryVertices++;
            }

            Assert.That(sharedBoundaryVertices, Is.GreaterThan(100),
                $"Expected a densely shared, fixed seam for {innerId}->{outerId}.");
        }
    }
}
#endif
