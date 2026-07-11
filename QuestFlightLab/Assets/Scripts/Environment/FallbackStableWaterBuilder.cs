using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.Environment
{
    /// <summary>Stable mesh fallback for sessions where the bounded OSM environment is unavailable.</summary>
    public static class FallbackStableWaterBuilder
    {
        public static void Build(
            Transform parent,
            WaterwayMeshBuilder.TerrainHeightSampler terrainHeight,
            Material waterMaterial,
            Material bankMaterial)
        {
            if (parent == null || terrainHeight == null || waterMaterial == null) return;

            AddReservoir(parent, "SmallReservoirWest", new Vector2(-1280f, 740f), new Vector2(310f, 130f), -8f, terrainHeight, waterMaterial);
            AddReservoir(parent, "BoulderReservoirHintNorth", new Vector2(-3150f, 3050f), new Vector2(625f, 210f), 5f, terrainHeight, waterMaterial);

            AddWaterway(
                parent,
                "DrainageSwaleNorth",
                new[]
                {
                    new Vector2(-790f, 810f), new Vector2(-510f, 825f), new Vector2(-230f, 874f),
                    new Vector2(40f, 842f), new Vector2(310f, 886f), new Vector2(560f, 930f)
                },
                5.5f,
                terrainHeight,
                waterMaterial,
                bankMaterial);
            AddWaterway(
                parent,
                "IrrigationDitchNorthwest",
                new[]
                {
                    new Vector2(-3100f, 1250f), new Vector2(-2800f, 1380f), new Vector2(-2460f, 1480f),
                    new Vector2(-2140f, 1540f), new Vector2(-1810f, 1660f), new Vector2(-1480f, 1810f)
                },
                4f,
                terrainHeight,
                waterMaterial,
                bankMaterial);
        }

        private static void AddReservoir(
            Transform parent,
            string name,
            Vector2 center,
            Vector2 radius,
            float yawDegrees,
            WaterwayMeshBuilder.TerrainHeightSampler terrainHeight,
            Material waterMaterial)
        {
            const int segments = 30;
            Quaternion rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            List<Vector2> polygon = new List<Vector2>(segments);
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                Vector3 local = rotation * new Vector3(Mathf.Cos(angle) * radius.x, 0f, Mathf.Sin(angle) * radius.y);
                polygon.Add(center + new Vector2(local.x, local.z));
            }
            WaterwayMeshBuilder.MeshBuffers water = NewBuffers();
            if (WaterwayMeshBuilder.TryAppendReservoir(polygon, terrainHeight, water, out _))
                CreateObject(parent, "FallbackStableWater_" + name, water, waterMaterial);
        }

        private static void AddWaterway(
            Transform parent,
            string name,
            IReadOnlyList<Vector2> points,
            float widthMeters,
            WaterwayMeshBuilder.TerrainHeightSampler terrainHeight,
            Material waterMaterial,
            Material bankMaterial)
        {
            WaterwayMeshBuilder.MeshBuffers water = NewBuffers();
            WaterwayMeshBuilder.MeshBuffers banks = bankMaterial != null ? NewBuffers() : null;
            if (!WaterwayMeshBuilder.TryAppendLinearWaterway(points, widthMeters, terrainHeight, water, banks, out _)) return;
            CreateObject(parent, "FallbackStableWater_" + name, water, waterMaterial);
            if (banks != null && banks.Triangles.Count > 0)
                CreateObject(parent, "FallbackStableWaterBank_" + name, banks, bankMaterial);
        }

        private static WaterwayMeshBuilder.MeshBuffers NewBuffers()
        {
            return new WaterwayMeshBuilder.MeshBuffers(new List<Vector3>(), new List<Vector2>(), new List<int>());
        }

        private static void CreateObject(
            Transform parent,
            string name,
            WaterwayMeshBuilder.MeshBuffers source,
            Material material)
        {
            Mesh mesh = new Mesh { name = name + "_Mesh" };
            mesh.SetVertices(source.Vertices);
            mesh.SetUVs(0, source.Uvs);
            mesh.SetTriangles(source.Triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.isStatic = true;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }
}
