using System;
using UnityEngine;

namespace QuestFlightLab.Environment
{
    public static class KbduInspiredWorldBuilder
    {
        private const string RootName = "KBDU_Inspired_Expanded_World_NotForNavigation";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject airport = GameObject.Find("KBDU_Approx_Airport_NotForNavigation");
            if (airport == null) return;
            AddWorld(airport.transform);
        }

        public static GameObject AddWorld(Transform airportRoot)
        {
            if (airportRoot == null) return null;
            if (RealKbduEnvironmentBuilder.TryBuild(airportRoot, out GameObject realWorld, out string realDataError))
            {
                return realWorld;
            }
            if (!string.IsNullOrWhiteSpace(realDataError))
            {
                Debug.LogWarning($"[QuestFlightLab][KBDU] Real-data path unavailable ({realDataError}); using preserved procedural fallback.");
            }
            Transform existing = airportRoot.Find(RootName);
            if (existing != null) return existing.gameObject;

            Material prairie = Material("Dry Prairie Chunk", new Color(0.37f, 0.40f, 0.23f), 0f, 0.54f, 18f);
            Material darkerGrass = Material("Mowed Field Chunk", new Color(0.22f, 0.33f, 0.17f), 0f, 0.50f, 20f);
            Material sage = Material("High Plains Sage Variation", new Color(0.28f, 0.34f, 0.22f), 0f, 0.56f, 14f);
            Material tanGrass = Material("Sun Cured Grass Wash", new Color(0.46f, 0.42f, 0.22f), 0f, 0.62f, 12f);
            Material dirt = Material("Dry Dirt Gravel", new Color(0.40f, 0.32f, 0.23f), 0f, 0.72f, 12f);
            Material road = Material("Local Road Asphalt", new Color(0.055f, 0.058f, 0.06f), 0f, 0.42f);
            Material concrete = Material("Light Industrial Concrete", new Color(0.42f, 0.41f, 0.37f), 0f, 0.36f, 12f);
            Material roof = Material("Industrial Roof Varied", new Color(0.18f, 0.2f, 0.22f), 0.05f, 0.48f, 6f);
            Material water = Material("Reservoir Water Muted", new Color(0.12f, 0.26f, 0.34f, 0.78f), 0f, 0.15f);
            Material foothill = Material("Front Range Foothill Far", new Color(0.27f, 0.31f, 0.25f), 0f, 0.72f, 9f);
            Material mountain = Material("Front Range Mountain Far", new Color(0.34f, 0.37f, 0.37f), 0f, 0.84f, 8f);
            Material snow = Material("High Ridge Snow Hint", new Color(0.76f, 0.78f, 0.75f), 0f, 0.7f);
            Material fence = Material("Airport Perimeter Fence", new Color(0.34f, 0.35f, 0.32f), 0.15f, 0.28f);
            Material fieldGold = Material("Harvest Field Gold", new Color(0.47f, 0.43f, 0.20f), 0f, 0.58f, 14f);
            Material fieldGreen = Material("Irrigated Field Green", new Color(0.20f, 0.36f, 0.16f), 0f, 0.5f, 14f);
            Material treeLine = Material("Cottonwood Tree Line", new Color(0.09f, 0.22f, 0.10f), 0f, 0.42f, 5f);
            Material scrub = Material("Foothill Scrub Brush", new Color(0.22f, 0.28f, 0.16f), 0f, 0.56f, 7f);
            Material ridgeHaze = Material("Front Range Atmospheric Haze", new Color(0.56f, 0.65f, 0.72f, 0.32f), 0f, 0.15f);

            GameObject root = new GameObject(RootName);
            root.transform.SetParent(airportRoot, false);

            int terrainChunkCount = AddTerrainChunks(root.transform, prairie, darkerGrass, dirt);
            AddLargeScaleTerrainColorCues(root.transform, sage, tanGrass, fieldGold, fieldGreen, dirt);
            AddFieldParcels(root.transform, fieldGold, fieldGreen, dirt);
            AddRoadNetwork(root.transform, road, concrete);
            AddAirportNeighborhood(root.transform, concrete, roof, dirt);
            AddReservoirAndDrainage(root.transform, water, dirt);
            AddPerimeterAndFieldCues(root.transform, fence, dirt, concrete, treeLine, scrub);
            AddFarFoothills(root.transform, foothill, mountain, snow, ridgeHaze);
            AddObjectLodGroups(root.transform);

            WorldPerformanceBudget budget = root.AddComponent<WorldPerformanceBudget>();
            PopulateBudget(root, budget, terrainChunkCount);
            return root;
        }

        private static int AddTerrainChunks(Transform parent, Material prairie, Material darkerGrass, Material dirt)
        {
            const int radiusX = 6;
            const int radiusZ = 6;
            const float chunk = 1120f;
            int count = 0;
            for (int ix = -radiusX; ix <= radiusX; ix++)
            {
                for (int iz = -radiusZ; iz <= radiusZ; iz++)
                {
                    float x = ix * chunk;
                    float z = iz * chunk;
                    float prairieBias = Mathf.PerlinNoise((ix + 19) * 0.23f, (iz + 31) * 0.19f);
                    Material material = prairieBias > 0.44f ? prairie : darkerGrass;
                    int ring = Mathf.Max(Mathf.Abs(ix), Mathf.Abs(iz));
                    int resolution = ring <= 1 ? 20 : ring <= 3 ? 12 : ring <= 5 ? 7 : 4;
                    GameObject tile = TerrainChunk(parent, $"TerrainChunk_{ix}_{iz}", new Vector3(x, 0f, z), chunk + 8f, resolution, material);
                    tile.isStatic = true;
                    count++;
                }
            }

            for (int i = 0; i < 70; i++)
            {
                float x = -5200f + (i % 35) * 305f;
                float z = -3150f + (i / 35) * 760f + Mathf.Sin(i * 1.7f) * 120f;
                float y = TerrainHeight(x, z) + 0.035f;
                Cube(parent, $"DryFieldPatch_{i}", new Vector3(x, y, z), Quaternion.Euler(0f, i * 11f, 0f), new Vector3(210f + (i % 4) * 32f, 0.014f, 38f + (i % 3) * 12f), dirt);
            }

            return count;
        }

        private static void AddLargeScaleTerrainColorCues(
            Transform parent,
            Material sage,
            Material tanGrass,
            Material fieldGold,
            Material fieldGreen,
            Material dirt)
        {
            Material[] palette = { sage, tanGrass, fieldGold, fieldGreen, dirt };
            for (int i = 0; i < 54; i++)
            {
                float x = -6200f + (i % 18) * 720f + Mathf.Sin(i * 0.71f) * 160f;
                float z = -3320f + (i / 18) * 1680f + Mathf.Cos(i * 0.43f) * 260f;
                float radiusX = 260f + (i % 5) * 85f;
                float radiusZ = 105f + (i % 4) * 60f;
                float yaw = -14f + (i % 9) * 4.5f;
                IrregularGroundPatch(parent, $"NativePrairieColorPatch_{i}", new Vector3(x, 0f, z), radiusX, radiusZ, yaw, palette[i % palette.Length], 700 + i);
            }

            for (int i = 0; i < 18; i++)
            {
                float x = -5400f + i * 640f;
                float z = 2860f + Mathf.Sin(i * 0.55f) * 180f;
                IrregularGroundPatch(parent, $"FoothillSlopeColorBreak_{i}", new Vector3(x, 0f, z), 360f, 95f, 6f + i * 1.6f, i % 2 == 0 ? sage : tanGrass, 900 + i);
            }
        }

        private static void AddFieldParcels(Transform parent, Material fieldGold, Material fieldGreen, Material dirt)
        {
            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    float x = -5700f + col * 1050f + Mathf.Sin((row + 1) * (col + 3)) * 80f;
                    float z = 1180f + row * 790f + Mathf.Cos((row + 2) * (col + 1)) * 96f;
                    Material mat = (row + col) % 3 == 0 ? fieldGreen : fieldGold;
                    Cube(parent, $"BoulderValleyFieldParcel_{row}_{col}", new Vector3(x, TerrainHeight(x, z) + 0.045f, z), Quaternion.Euler(0f, -8f + row * 3f, 0f), new Vector3(720f, 0.014f, 290f), mat);
                    if ((row + col) % 2 == 0)
                    {
                        Cube(parent, $"BoulderValleyFieldFurrow_{row}_{col}", new Vector3(x, TerrainHeight(x, z) + 0.058f, z), Quaternion.Euler(0f, -8f + row * 3f, 0f), new Vector3(690f, 0.012f, 4.2f), dirt);
                    }
                }
            }
        }

        private static void AddRoadNetwork(Transform parent, Material road, Material concrete)
        {
            Road(parent, "DiagonalAirportAccessRoad", -330f, -520f, -14f, 1600f, 8f, road);
            Road(parent, "NorthernArterialRoad", 140f, 505f, 6f, 3200f, 10f, road);
            Road(parent, "EasternServiceRoad", 785f, -290f, 87f, 1500f, 7f, road);
            Road(parent, "WestRampAccessRoad", -705f, -190f, 84f, 860f, 6f, road);
            Road(parent, "BoulderValleyNorthSouthRoad", -2200f, 850f, 88f, 3600f, 8f, road);
            Road(parent, "BoulderReservoirAccessRoad", -1420f, 1350f, -31f, 1760f, 6.5f, road);
            Road(parent, "AirportIndustrialLoopRoad", -520f, -710f, 4f, 820f, 7f, road);
            Road(parent, "EastCountyRoad", 2550f, 1380f, 91f, 3600f, 8f, road);
            Road(parent, "NorthReservoirRoad", -2450f, 2850f, -7f, 3300f, 7f, road);
            Road(parent, "EastFarmGridRoad", 3540f, 2480f, 2f, 3000f, 6.5f, road);
            Road(parent, "SouthAirportServiceRoad", 120f, -1840f, 10f, 2800f, 6f, road);

            for (int i = 0; i < 22; i++)
            {
                Cube(parent, $"RoadCenterDash_{i}", new Vector3(-1120f + i * 170f, 0.032f, -387f + i * 9f), Quaternion.Euler(0f, -14f, 0f), new Vector3(26f, 0.01f, 0.55f), concrete);
            }
        }

        private static void AddAirportNeighborhood(Transform parent, Material concrete, Material roof, Material dirt)
        {
            for (int i = 0; i < 18; i++)
            {
                float x = -820f + (i % 6) * 86f;
                float z = -620f - (i / 6) * 82f;
                GameObject building = Cube(parent, $"AirportIndustrial_{i}_Body", new Vector3(x, 5.3f, z), Quaternion.Euler(0f, (i % 3 - 1) * 3f, 0f), new Vector3(52f + (i % 3) * 12f, 10.5f, 35f + (i % 2) * 14f), concrete);
                building.isStatic = true;
                Cube(parent, $"AirportIndustrial_{i}_Roof", new Vector3(x, 11.2f, z), Quaternion.Euler(0f, (i % 3 - 1) * 3f, 0f), new Vector3(56f + (i % 3) * 12f, 1.5f, 39f + (i % 2) * 14f), roof);
                Cube(parent, $"AirportIndustrial_{i}_Yard", new Vector3(x, 0.005f, z + 38f), Quaternion.identity, new Vector3(68f, 0.012f, 34f), dirt);
            }

            for (int i = 0; i < 54; i++)
            {
                float x = 920f + (i % 11) * 78f;
                float z = -860f + (i / 11) * 88f;
                Cube(parent, $"LowBuildingFar_{i}", new Vector3(x, 3.0f, z), Quaternion.Euler(0f, (i % 4) * 2f, 0f), new Vector3(38f + (i % 3) * 8f, 6f, 28f + (i % 2) * 10f), i % 2 == 0 ? concrete : roof);
            }
        }

        private static void AddReservoirAndDrainage(Transform parent, Material water, Material dirt)
        {
            Cube(parent, "SmallReservoirWest", new Vector3(-1280f, TerrainHeight(-1280f, 740f) + 0.018f, 740f), Quaternion.Euler(0f, -8f, 0f), new Vector3(620f, 0.018f, 260f), water);
            Cube(parent, "ReservoirShoreWest", new Vector3(-1280f, TerrainHeight(-1280f, 740f) + 0.012f, 740f), Quaternion.Euler(0f, -8f, 0f), new Vector3(680f, 0.014f, 306f), dirt);
            Cube(parent, "DrainageSwaleNorth", new Vector3(-120f, TerrainHeight(-120f, 850f) + 0.018f, 850f), Quaternion.Euler(0f, 5f, 0f), new Vector3(1350f, 0.012f, 34f), dirt);
            Cube(parent, "IrrigationDitchNorthwest", new Vector3(-2200f, TerrainHeight(-2200f, 1520f) + 0.016f, 1520f), Quaternion.Euler(0f, 18f, 0f), new Vector3(1900f, 0.012f, 18f), dirt);
            Cube(parent, "BoulderReservoirHintNorth", new Vector3(-3150f, TerrainHeight(-3150f, 3050f) + 0.018f, 3050f), Quaternion.Euler(0f, 5f, 0f), new Vector3(1250f, 0.016f, 420f), water);
            Cube(parent, "BoulderReservoirHintShore", new Vector3(-3150f, TerrainHeight(-3150f, 3050f) + 0.012f, 3050f), Quaternion.Euler(0f, 5f, 0f), new Vector3(1360f, 0.012f, 484f), dirt);
        }

        private static void AddPerimeterAndFieldCues(Transform parent, Material fence, Material dirt, Material concrete, Material treeLine, Material scrub)
        {
            Cube(parent, "AirportPerimeterFenceNorth", new Vector3(20f, 1.15f, 356f), Quaternion.identity, new Vector3(1480f, 1.3f, 0.18f), fence);
            Cube(parent, "AirportPerimeterFenceSouth", new Vector3(20f, 1.15f, -426f), Quaternion.identity, new Vector3(1480f, 1.3f, 0.18f), fence);
            Cube(parent, "AirportPerimeterFenceWest", new Vector3(-728f, 1.15f, -36f), Quaternion.identity, new Vector3(0.18f, 1.3f, 780f), fence);
            Cube(parent, "AirportPerimeterFenceEast", new Vector3(748f, 1.15f, -36f), Quaternion.identity, new Vector3(0.18f, 1.3f, 780f), fence);

            for (int i = 0; i < 12; i++)
            {
                float x = -620f + i * 112f;
                Cube(parent, $"RampParkingStripe_{i}", new Vector3(x, 0.12f, -171f), Quaternion.Euler(0f, 18f, 0f), new Vector3(1.1f, 0.011f, 22f), concrete);
            }

            for (int i = 0; i < 28; i++)
            {
                float x = -4200f + (i % 14) * 560f;
                float z = 1180f + (i / 14) * 510f;
                Cube(parent, $"DistantFarmTrack_{i}", new Vector3(x, TerrainHeight(x, z) + 0.032f, z), Quaternion.Euler(0f, (i % 4) * 7f, 0f), new Vector3(360f, 0.012f, 5.5f), dirt);
            }

            for (int i = 0; i < 28; i++)
            {
                float x = -5200f + i * 420f;
                float z = 2050f + Mathf.Sin(i * 0.77f) * 220f;
                Cube(parent, $"CottonwoodTreeLineNorth_{i}", new Vector3(x, TerrainHeight(x, z) + 4.5f, z), Quaternion.Euler(0f, i * 4f, 0f), new Vector3(145f, 9f + i % 4 * 1.5f, 12f), treeLine);
            }

            for (int i = 0; i < 34; i++)
            {
                float x = -6100f + (i % 17) * 720f;
                float z = 3720f + Mathf.Sin(i * 0.63f) * 360f;
                Cube(parent, $"FoothillScrubPatch_{i}", new Vector3(x, TerrainHeight(x, z) + 1.2f, z), Quaternion.Euler(0f, i * 9f, 0f), new Vector3(180f + (i % 3) * 70f, 2.2f, 22f), scrub);
            }
        }

        private static void AddFarFoothills(Transform parent, Material foothill, Material mountain, Material snow, Material ridgeHaze)
        {
            Ridge(parent, "FrontRangeFoothillLowRidge", -7600f, 15200f, 4300f, 980f, 18f, 148f, 38, 11, foothill);
            Ridge(parent, "FrontRangeFoothillMiddleRidge", -8300f, 16600f, 5050f, 1080f, 34f, 205f, 42, 19, foothill);
            Ridge(parent, "FrontRangeFoothillBackRidge", -8800f, 17600f, 6100f, 1260f, 58f, 285f, 46, 29, foothill);
            Ridge(parent, "FrontRangeMountainBackRidge", -9600f, 19200f, 8200f, 1680f, 130f, 640f, 52, 47, mountain);
            Cube(parent, "FrontRangeLowAtmosphericHazeBand", new Vector3(0f, 118f, 4850f), Quaternion.identity, new Vector3(15600f, 96f, 18f), ridgeHaze);
            Cube(parent, "FrontRangeBackAtmosphericHazeBand", new Vector3(0f, 250f, 6900f), Quaternion.identity, new Vector3(18400f, 150f, 18f), ridgeHaze);

            for (int i = 0; i < 11; i++)
            {
                float x = -7200f + i * 1440f;
                float y = 530f + Mathf.Sin(i * 0.8f) * 58f;
                Cube(parent, $"FrontRangeSnowCap_{i}", new Vector3(x, y, 8200f + Mathf.Cos(i * 0.51f) * 130f), Quaternion.Euler(0f, i * 6f, -7f + i % 3 * 5f), new Vector3(520f, 12f, 58f), snow);
            }
        }

        private static void AddObjectLodGroups(Transform root)
        {
            foreach (Transform child in root)
            {
                if (!child.name.StartsWith("AirportIndustrial_", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("FrontRange", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("LowBuildingFar_", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("BoulderValleyFieldParcel_", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("BoulderValleyFieldFurrow_", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("CottonwoodTreeLine", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("FoothillScrubPatch_", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("NativePrairieColorPatch_", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("FoothillSlopeColorBreak_", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("DistantFarmTrack_", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("DryFieldPatch_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer == null) continue;
                LODGroup lodGroup = child.gameObject.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.18f, new[] { renderer }),
                    new LOD(0.04f, new[] { renderer })
                });
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.RecalculateBounds();
            }
        }

        private static GameObject Cube(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            return Primitive(PrimitiveType.Cube, parent, name, position, rotation, scale, material);
        }

        private static GameObject Sphere(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            return Primitive(PrimitiveType.Sphere, parent, name, position, rotation, scale, material);
        }

        private static GameObject TerrainChunk(Transform parent, string name, Vector3 center, float size, int resolution, Material material)
        {
            Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[resolution * resolution * 6];

            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    float localX = -size * 0.5f + size * x / resolution;
                    float localZ = -size * 0.5f + size * z / resolution;
                    float worldX = center.x + localX;
                    float worldZ = center.z + localZ;
                    int index = z * (resolution + 1) + x;
                    vertices[index] = new Vector3(localX, TerrainHeight(worldX, worldZ), localZ);
                    uvs[index] = new Vector2(worldX * 0.012f, worldZ * 0.012f);
                }
            }

            int t = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = z * (resolution + 1) + x;
                    triangles[t++] = i;
                    triangles[t++] = i + resolution + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + resolution + 1;
                    triangles[t++] = i + resolution + 2;
                }
            }

            Mesh mesh = new Mesh { name = name + "_Mesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(center.x, 0f, center.z);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        private static GameObject IrregularGroundPatch(
            Transform parent,
            string name,
            Vector3 center,
            float radiusX,
            float radiusZ,
            float yawDeg,
            Material material,
            int seed)
        {
            const int segments = 11;
            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 3];
            Quaternion rotation = Quaternion.Euler(0f, yawDeg, 0f);
            vertices[0] = new Vector3(center.x, TerrainHeight(center.x, center.z) + 0.042f, center.z);
            uvs[0] = Vector2.one * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float n = 0.74f + Noise01(seed, i) * 0.38f;
                Vector3 local = new Vector3(Mathf.Cos(angle) * radiusX * n, 0f, Mathf.Sin(angle) * radiusZ * (0.76f + Noise01(seed + 37, i) * 0.34f));
                Vector3 world = center + rotation * local;
                vertices[i + 1] = new Vector3(world.x, TerrainHeight(world.x, world.z) + 0.044f, world.z);
                uvs[i + 1] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                int t = i * 3;
                triangles[t] = 0;
                triangles[t + 1] = i + 1;
                triangles[t + 2] = i == segments - 1 ? 1 : i + 2;
            }

            Mesh mesh = new Mesh { name = name + "_Mesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            go.isStatic = true;
            return go;
        }

        private static GameObject Road(Transform parent, string name, float x, float z, float yaw, float length, float width, Material material)
        {
            return Cube(parent, name, new Vector3(x, TerrainHeight(x, z) + 0.035f, z), Quaternion.Euler(0f, yaw, 0f), new Vector3(length, 0.018f, width), material);
        }

        private static GameObject Ridge(
            Transform parent,
            string name,
            float startX,
            float length,
            float z,
            float depth,
            float baseY,
            float peakY,
            int segments,
            int seed,
            Material material)
        {
            int pointCount = segments + 1;
            Vector3[] vertices = new Vector3[pointCount * 3];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 12];

            for (int i = 0; i < pointCount; i++)
            {
                float x = startX + length * i / segments;
                float ridgeNoise = Noise01(seed, i) * 0.42f + Mathf.Sin(i * 0.83f + seed) * 0.18f;
                float topY = Mathf.Lerp(baseY + 26f, peakY, Mathf.Clamp01(0.38f + ridgeNoise));
                float frontBase = Mathf.Max(TerrainHeight(x, z - depth * 0.5f), baseY);
                float rearBase = Mathf.Max(TerrainHeight(x, z + depth * 0.5f), baseY + 12f);
                int b = i * 3;
                vertices[b] = new Vector3(x, frontBase, z - depth * 0.5f);
                vertices[b + 1] = new Vector3(x, topY, z);
                vertices[b + 2] = new Vector3(x, rearBase, z + depth * 0.5f);
                uvs[b] = new Vector2(i / (float)segments, 0f);
                uvs[b + 1] = new Vector2(i / (float)segments, 1f);
                uvs[b + 2] = new Vector2(i / (float)segments, 0f);
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i * 3;
                int b = (i + 1) * 3;
                triangles[t++] = a;
                triangles[t++] = b + 1;
                triangles[t++] = b;
                triangles[t++] = a;
                triangles[t++] = a + 1;
                triangles[t++] = b + 1;
                triangles[t++] = a + 1;
                triangles[t++] = a + 2;
                triangles[t++] = b + 2;
                triangles[t++] = a + 1;
                triangles[t++] = b + 2;
                triangles[t++] = b + 1;
            }

            Mesh mesh = new Mesh { name = name + "_Mesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.isStatic = true;
            return go;
        }

        private static GameObject Primitive(PrimitiveType primitive, Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
                else UnityEngine.Object.DestroyImmediate(collider);
            }

            return go;
        }

        private static Material Material(string name, Color color, float metallic, float smoothness, float textureScale = 8f)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            if (color.a >= 0.999f)
            {
                Texture2D noise = NoiseTexture(name, color);
                material.mainTexture = noise;
                material.mainTextureScale = new Vector2(textureScale, textureScale);
            }

            if (color.a < 1f)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return material;
        }

        private static void PopulateBudget(GameObject root, WorldPerformanceBudget budget, int terrainChunkCount)
        {
            MeshFilter[] meshes = root.GetComponentsInChildren<MeshFilter>(true);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            int triangles = 0;
            foreach (MeshFilter filter in meshes)
            {
                if (filter.sharedMesh != null) triangles += filter.sharedMesh.triangles.Length / 3;
            }

            budget.profileName = "visual_fidelity_demo_medium";
            budget.worldSizeMeters = new Vector2(14560f, 14560f);
            budget.terrainChunkCount = terrainChunkCount;
            budget.lodGroupCount = root.GetComponentsInChildren<LODGroup>(true).Length;
            budget.rendererCount = renderers.Length;
            budget.meshCount = meshes.Length;
            budget.approxTriangleCount = triangles;
            budget.materialCount = CountUniqueMaterials(renderers);
            budget.textureCount = CountUniqueTextures(renderers);
            budget.nearDetailRadiusMeters = 2200f;
            budget.midDetailRadiusMeters = 5200f;
            budget.farDrawRadiusMeters = 9200f;
            budget.notes = "Procedural KBDU-inspired world with mesh terrain detail rings, OSM-referenced road/airport cues, field parcels, and ridge impostors; not navigation-accurate.";
        }

        private static int CountUniqueMaterials(Renderer[] renderers)
        {
            var materials = new System.Collections.Generic.HashSet<Material>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null) materials.Add(material);
                }
            }

            return materials.Count;
        }

        private static int CountUniqueTextures(Renderer[] renderers)
        {
            var textures = new System.Collections.Generic.HashSet<Texture>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.mainTexture != null) textures.Add(material.mainTexture);
                }
            }

            return textures.Count;
        }

        private static float TerrainHeight(float x, float z)
        {
            float runwayFlat = Mathf.Clamp01(1f - Mathf.Max(Mathf.Abs(x) / 1300f, Mathf.Abs(z) / 720f));
            float northRise = Mathf.Clamp01((z - 840f) / 4200f) * 42f;
            float westRise = Mathf.Clamp01((-x - 1320f) / 3900f) * 16f;
            float foothillLift = Mathf.Clamp01((z - 2850f) / 4300f) * Mathf.Clamp01((-x + 1200f) / 5600f) * 58f;
            float broadValleyTilt = Mathf.Sin((x - 900f) * 0.00072f) * 4.5f + Mathf.Cos((z + 1100f) * 0.00058f) * 5.2f;
            float ripple = Mathf.Sin(x * 0.0036f + z * 0.0024f) * 3.4f +
                           Mathf.Sin(x * 0.0072f - z * 0.0017f) * 2.0f +
                           Mathf.Sin((x + z) * 0.0011f) * 4.4f +
                           Mathf.Sin((x * 0.0018f) + Mathf.Sin(z * 0.0011f) * 2.2f) * 3.0f;
            float height = -0.18f + northRise + westRise + foothillLift + broadValleyTilt + ripple;
            return Mathf.Lerp(height, -0.18f, runwayFlat);
        }

        private static Texture2D NoiseTexture(string name, Color color)
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name + "_Noise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };

            int seed = name.GetHashCode();
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Noise01(seed + y * 17, x);
                    float n2 = Noise01(seed + x * 23, y);
                    float band = Mathf.Sin((x + seed % 13) * 0.31f + y * 0.11f) * 0.045f;
                    float broad = Mathf.Sin(x * 0.065f + seed * 0.001f) * Mathf.Cos(y * 0.052f) * 0.055f;
                    float scale = 0.82f + n * 0.18f + n2 * 0.12f + band + broad;
                    Color c = new Color(
                        Mathf.Clamp01(color.r * scale),
                        Mathf.Clamp01(color.g * scale),
                        Mathf.Clamp01(color.b * scale),
                        color.a);
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply(true, true);
            return texture;
        }

        private static float Noise01(int seed, int index)
        {
            unchecked
            {
                uint n = (uint)(seed * 374761393 + index * 668265263);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0x00FFFFFF) / 16777215f;
            }
        }
    }

    public class WorldPerformanceBudget : MonoBehaviour
    {
        public string profileName;
        public Vector2 worldSizeMeters;
        public int terrainChunkCount;
        public int lodGroupCount;
        public int rendererCount;
        public int meshCount;
        public int approxTriangleCount;
        public int materialCount;
        public int textureCount;
        public float nearDetailRadiusMeters;
        public float midDetailRadiusMeters;
        public float farDrawRadiusMeters;
        public string notes;
    }
}
