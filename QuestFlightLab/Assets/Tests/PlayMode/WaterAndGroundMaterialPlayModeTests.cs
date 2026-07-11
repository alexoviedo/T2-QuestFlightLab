#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using QuestFlightLab.Environment;
using UnityEngine;

namespace QuestFlightLab.Tests
{
    public class WaterAndGroundMaterialPlayModeTests
    {
        [Test]
        public void LinearWaterwayIsSmoothDeterministicSeparatedAndNonIntersecting()
        {
            Vector2[] source =
            {
                new Vector2(-80f, -15f), new Vector2(-20f, 5f), new Vector2(15f, 38f),
                new Vector2(70f, 48f), new Vector2(130f, 28f), new Vector2(190f, 46f)
            };
            WaterwayMeshBuilder.MeshBuffers firstWater = NewBuffers();
            WaterwayMeshBuilder.MeshBuffers firstBanks = NewBuffers();
            WaterwayMeshBuilder.MeshBuffers secondWater = NewBuffers();
            WaterwayMeshBuilder.MeshBuffers secondBanks = NewBuffers();
            float Terrain(float x, float z) => x * 0.001f + z * 0.0005f;

            Assert.That(WaterwayMeshBuilder.TryAppendLinearWaterway(source, 5f, Terrain, firstWater, firstBanks, out var first), Is.True);
            Assert.That(WaterwayMeshBuilder.TryAppendLinearWaterway(source, 5f, Terrain, secondWater, secondBanks, out var second), Is.True);
            Assert.That(first.outputPointCount, Is.GreaterThan(source.Length));
            Assert.That(first.maximumTurnDegrees, Is.LessThanOrEqualTo(WaterwayMeshBuilder.MaximumSegmentTurnDegrees + 0.1f));
            Assert.That(first.minimumTerrainSeparationMeters, Is.GreaterThanOrEqualTo(WaterwayMeshBuilder.MinimumAcceptedTerrainSeparationMeters));
            Assert.That(first.bankTriangleCount, Is.GreaterThan(0));
            Assert.That(firstWater.Vertices, Is.EqualTo(secondWater.Vertices));
            Assert.That(firstWater.Uvs, Is.EqualTo(secondWater.Uvs));
            Assert.That(firstWater.Triangles, Is.EqualTo(secondWater.Triangles));
            Assert.That(firstBanks.Vertices, Is.EqualTo(secondBanks.Vertices));
            Assert.That(first.waterTriangleCount, Is.EqualTo(second.waterTriangleCount));

            List<Vector2> centerline = WaterwayMeshBuilder.SmoothAndResampleCenterline(source, WaterwayMeshBuilder.CenterlineSampleSpacingMeters);
            Assert.That(centerline[0], Is.EqualTo(source[0]));
            Assert.That(centerline[centerline.Count - 1], Is.EqualTo(source[source.Length - 1]));
            Assert.That(WaterwayMeshBuilder.HasSelfIntersection(centerline, false), Is.False);
            for (int index = 1; index < centerline.Count - 1; index++)
                Assert.That(Vector2.Distance(centerline[index - 1], centerline[index]), Is.InRange(8f, 15.01f));
        }

        [Test]
        public void CrossingWaterwayIsRejectedBeforeMeshCreation()
        {
            Vector2[] crossing =
            {
                new Vector2(-20f, -20f), new Vector2(20f, 20f),
                new Vector2(-20f, 20f), new Vector2(20f, -20f)
            };
            WaterwayMeshBuilder.MeshBuffers water = NewBuffers();
            Assert.That(WaterwayMeshBuilder.TryAppendLinearWaterway(crossing, 3f, (x, z) => 0f, water, null, out var statistics), Is.False);
            Assert.That(statistics.rejectedSelfIntersection, Is.True);
            Assert.That(water.Vertices, Is.Empty);
        }

        [Test]
        public void ConcaveReservoirUsesHorizontalEarClippedSurfaceWithUpwardWinding()
        {
            Vector2[] polygon =
            {
                new Vector2(-30f, -20f), new Vector2(30f, -20f), new Vector2(30f, 20f),
                new Vector2(5f, 5f), new Vector2(-30f, 20f), new Vector2(-30f, -20f)
            };
            WaterwayMeshBuilder.MeshBuffers water = NewBuffers();
            Assert.That(WaterwayMeshBuilder.TryAppendReservoir(polygon, (x, z) => x * 0.003f, water, out var statistics), Is.True);
            Assert.That(statistics.horizontalSurface, Is.True);
            Assert.That(statistics.waterTriangleCount, Is.EqualTo(3));
            Assert.That(statistics.minimumTerrainSeparationMeters, Is.GreaterThanOrEqualTo(WaterwayMeshBuilder.MinimumAcceptedTerrainSeparationMeters));
            float level = water.Vertices[0].y;
            Assert.That(water.Vertices, Has.All.Matches<Vector3>(vertex => Mathf.Abs(vertex.y - level) < 0.0001f));
            for (int index = 0; index < water.Triangles.Count; index += 3)
            {
                Vector3 a = water.Vertices[water.Triangles[index]];
                Vector3 b = water.Vertices[water.Triangles[index + 1]];
                Vector3 c = water.Vertices[water.Triangles[index + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void GroundMaterialUsesThreeWorldMappedMipFilteredTexturesAndStableOpaqueWater()
        {
            Texture2D dry = Resources.Load<Texture2D>(QuestEnvironmentMaterialFactory.WitheredGrassResourcePath);
            Texture2D green = Resources.Load<Texture2D>(QuestEnvironmentMaterialFactory.SparseGrassResourcePath);
            Texture2D soil = Resources.Load<Texture2D>(QuestEnvironmentMaterialFactory.DrySoilResourcePath);
            Assert.That(new[] { dry, green, soil }, Has.All.Not.Null);
            foreach (Texture2D texture in new[] { dry, green, soil })
            {
                Assert.That(texture.width, Is.EqualTo(QuestEnvironmentMaterialFactory.GroundTextureResolution));
                Assert.That(texture.height, Is.EqualTo(QuestEnvironmentMaterialFactory.GroundTextureResolution));
                Assert.That(texture.mipmapCount, Is.GreaterThan(1));
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
                Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(texture.anisoLevel, Is.GreaterThanOrEqualTo(4));
            }
            Assert.That(QuestEnvironmentMaterialFactory.GroundTextureSampleBudget, Is.EqualTo(3));
            Material ground = QuestEnvironmentMaterialFactory.CreateGroundMaterial("Test Prairie", "dry_prairie", Color.white);
            Material water = QuestEnvironmentMaterialFactory.CreateStableWaterMaterial("Test Water", new Color(0.1f, 0.2f, 0.3f));
            GameObject rendererObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try
            {
                Assert.That(ground.shader.name, Is.EqualTo(QuestEnvironmentMaterialFactory.GroundShaderName));
                Assert.That(ground.GetTexture("_DryGrassTex"), Is.SameAs(dry));
                Assert.That(ground.GetTexture("_GreenGrassTex"), Is.SameAs(green));
                Assert.That(ground.GetTexture("_SoilTex"), Is.SameAs(soil));
                Assert.That(ground.enableInstancing, Is.True);
                Assert.That(water.shader.name, Is.EqualTo(QuestEnvironmentMaterialFactory.WaterShaderName));
                Assert.That(water.GetTag("RenderType", false), Is.EqualTo("Opaque"));
                Assert.That(water.GetFloat("_ZWrite"), Is.EqualTo(1f));
                Assert.That(water.renderQueue, Is.LessThan((int)UnityEngine.Rendering.RenderQueue.Transparent));

                Vector3 sameWorldPoint = new Vector3(1120f, 0f, -560f);
                Assert.That(QuestEnvironmentMaterialFactory.WorldUv(sameWorldPoint, 0),
                    Is.EqualTo(QuestEnvironmentMaterialFactory.WorldUv(sameWorldPoint, 0)),
                    "World-space mapping must not reset at a terrain-chunk boundary.");
                Assert.That(QuestEnvironmentMaterialFactory.WorldUv(sameWorldPoint, 0),
                    Is.Not.EqualTo(QuestEnvironmentMaterialFactory.WorldUv(sameWorldPoint, 1)),
                    "Independent fixed rotations break single-grid repetition without per-renderer materials.");

                Renderer renderer = rendererObject.GetComponent<Renderer>();
                renderer.sharedMaterial = ground;
                QuestEnvironmentMaterialFactory.ApplyDeterministicBatchVariation(renderer, "stable-batch-17", false);
                MaterialPropertyBlock firstBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(firstBlock);
                Vector4 firstTransform = firstBlock.GetVector("_BatchUvTransform");
                QuestEnvironmentMaterialFactory.ApplyDeterministicBatchVariation(renderer, "stable-batch-17", false);
                MaterialPropertyBlock secondBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(secondBlock);
                Assert.That(secondBlock.GetVector("_BatchUvTransform"), Is.EqualTo(firstTransform));
                Assert.That(renderer.sharedMaterial, Is.SameAs(ground), "Per-batch variation must not clone materials.");
            }
            finally
            {
                Object.DestroyImmediate(rendererObject);
                Object.DestroyImmediate(ground);
                Object.DestroyImmediate(water);
            }
        }

        private static WaterwayMeshBuilder.MeshBuffers NewBuffers()
        {
            return new WaterwayMeshBuilder.MeshBuffers(new List<Vector3>(), new List<Vector2>(), new List<int>());
        }
    }
}
#endif
