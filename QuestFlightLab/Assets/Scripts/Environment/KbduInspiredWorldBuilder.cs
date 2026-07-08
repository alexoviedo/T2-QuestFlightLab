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
            Transform existing = airportRoot.Find(RootName);
            if (existing != null) return existing.gameObject;

            Material prairie = Material("Dry Prairie Chunk", new Color(0.32f, 0.40f, 0.22f), 0f, 0.5f);
            Material darkerGrass = Material("Mowed Field Chunk", new Color(0.19f, 0.32f, 0.16f), 0f, 0.48f);
            Material dirt = Material("Dry Dirt Gravel", new Color(0.38f, 0.31f, 0.22f), 0f, 0.68f);
            Material road = Material("Local Road Asphalt", new Color(0.055f, 0.058f, 0.06f), 0f, 0.42f);
            Material concrete = Material("Light Industrial Concrete", new Color(0.42f, 0.41f, 0.37f), 0f, 0.36f);
            Material roof = Material("Industrial Roof Varied", new Color(0.18f, 0.2f, 0.22f), 0.05f, 0.48f);
            Material water = Material("Reservoir Water Muted", new Color(0.12f, 0.26f, 0.34f, 0.78f), 0f, 0.15f);
            Material foothill = Material("Front Range Foothill Far", new Color(0.27f, 0.31f, 0.25f), 0f, 0.72f);
            Material mountain = Material("Front Range Mountain Far", new Color(0.34f, 0.37f, 0.37f), 0f, 0.84f);
            Material snow = Material("High Ridge Snow Hint", new Color(0.76f, 0.78f, 0.75f), 0f, 0.7f);
            Material fence = Material("Airport Perimeter Fence", new Color(0.34f, 0.35f, 0.32f), 0.15f, 0.28f);

            GameObject root = new GameObject(RootName);
            root.transform.SetParent(airportRoot, false);

            int terrainChunkCount = AddTerrainChunks(root.transform, prairie, darkerGrass, dirt);
            AddRoadNetwork(root.transform, road, concrete);
            AddAirportNeighborhood(root.transform, concrete, roof, dirt);
            AddReservoirAndDrainage(root.transform, water, dirt);
            AddPerimeterAndFieldCues(root.transform, fence, dirt, concrete);
            AddFarFoothills(root.transform, foothill, mountain, snow);
            AddObjectLodGroups(root.transform);

            WorldPerformanceBudget budget = root.AddComponent<WorldPerformanceBudget>();
            budget.profileName = "visual_fidelity_demo_medium";
            budget.worldSizeMeters = new Vector2(8800f, 7800f);
            budget.terrainChunkCount = terrainChunkCount;
            budget.lodGroupCount = root.GetComponentsInChildren<LODGroup>(true).Length;
            budget.notes = "Procedural KBDU-inspired world with mesh terrain chunks and ridge impostors; not navigation-accurate.";
            return root;
        }

        private static int AddTerrainChunks(Transform parent, Material prairie, Material darkerGrass, Material dirt)
        {
            const int radiusX = 4;
            const int radiusZ = 4;
            const float chunk = 980f;
            int count = 0;
            for (int ix = -radiusX; ix <= radiusX; ix++)
            {
                for (int iz = -radiusZ; iz <= radiusZ; iz++)
                {
                    float x = ix * chunk;
                    float z = iz * chunk;
                    Material material = ((ix + iz) & 1) == 0 ? prairie : darkerGrass;
                    GameObject tile = TerrainChunk(parent, $"TerrainChunk_{ix}_{iz}", new Vector3(x, 0f, z), chunk + 8f, material);
                    tile.isStatic = true;
                    count++;
                }
            }

            for (int i = 0; i < 46; i++)
            {
                float x = -3700f + (i % 23) * 330f;
                float z = -2300f + (i / 23) * 620f + Mathf.Sin(i * 1.7f) * 90f;
                float y = TerrainHeight(x, z) + 0.035f;
                Cube(parent, $"DryFieldPatch_{i}", new Vector3(x, y, z), Quaternion.Euler(0f, i * 11f, 0f), new Vector3(210f + (i % 4) * 32f, 0.014f, 38f + (i % 3) * 12f), dirt);
            }

            return count;
        }

        private static void AddRoadNetwork(Transform parent, Material road, Material concrete)
        {
            Cube(parent, "DiagonalAirportAccessRoad", new Vector3(-330f, 0.015f, -520f), Quaternion.Euler(0f, -14f, 0f), new Vector3(1220f, 0.018f, 8f), road);
            Cube(parent, "NorthernArterialRoad", new Vector3(140f, 0.012f, 505f), Quaternion.Euler(0f, 6f, 0f), new Vector3(2460f, 0.018f, 10f), road);
            Cube(parent, "EasternServiceRoad", new Vector3(785f, 0.013f, -290f), Quaternion.Euler(0f, 87f, 0f), new Vector3(1320f, 0.018f, 7f), road);
            Cube(parent, "WestRampAccessRoad", new Vector3(-705f, 0.014f, -190f), Quaternion.Euler(0f, 84f, 0f), new Vector3(760f, 0.018f, 6f), road);

            for (int i = 0; i < 9; i++)
            {
                Cube(parent, $"RoadCenterDash_{i}", new Vector3(-860f + i * 185f, 0.032f, -387f + i * 9f), Quaternion.Euler(0f, -14f, 0f), new Vector3(26f, 0.01f, 0.55f), concrete);
            }
        }

        private static void AddAirportNeighborhood(Transform parent, Material concrete, Material roof, Material dirt)
        {
            for (int i = 0; i < 11; i++)
            {
                float x = -760f + (i % 6) * 85f;
                float z = -640f - (i / 6) * 96f;
                GameObject building = Cube(parent, $"AirportIndustrial_{i}_Body", new Vector3(x, 5.8f, z), Quaternion.identity, new Vector3(52f + (i % 3) * 12f, 11.5f, 35f + (i % 2) * 14f), concrete);
                building.isStatic = true;
                Cube(parent, $"AirportIndustrial_{i}_Roof", new Vector3(x, 12f, z), Quaternion.identity, new Vector3(56f + (i % 3) * 12f, 1.5f, 39f + (i % 2) * 14f), roof);
                Cube(parent, $"AirportIndustrial_{i}_Yard", new Vector3(x, 0.005f, z + 38f), Quaternion.identity, new Vector3(68f, 0.012f, 34f), dirt);
            }

            for (int i = 0; i < 18; i++)
            {
                float x = 940f + (i % 6) * 68f;
                float z = -710f + (i / 6) * 72f;
                Cube(parent, $"LowBuildingFar_{i}", new Vector3(x, 3.2f, z), Quaternion.identity, new Vector3(38f, 6.4f, 28f), i % 2 == 0 ? concrete : roof);
            }
        }

        private static void AddReservoirAndDrainage(Transform parent, Material water, Material dirt)
        {
            Cube(parent, "SmallReservoirWest", new Vector3(-1280f, TerrainHeight(-1280f, 740f) + 0.018f, 740f), Quaternion.Euler(0f, -8f, 0f), new Vector3(360f, 0.018f, 160f), water);
            Cube(parent, "ReservoirShoreWest", new Vector3(-1280f, TerrainHeight(-1280f, 740f) + 0.012f, 740f), Quaternion.Euler(0f, -8f, 0f), new Vector3(394f, 0.014f, 188f), dirt);
            Cube(parent, "DrainageSwaleNorth", new Vector3(-120f, TerrainHeight(-120f, 850f) + 0.018f, 850f), Quaternion.Euler(0f, 5f, 0f), new Vector3(1350f, 0.012f, 34f), dirt);
        }

        private static void AddPerimeterAndFieldCues(Transform parent, Material fence, Material dirt, Material concrete)
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

            for (int i = 0; i < 16; i++)
            {
                float x = -2600f + (i % 8) * 520f;
                float z = 1180f + (i / 8) * 430f;
                Cube(parent, $"DistantFarmTrack_{i}", new Vector3(x, TerrainHeight(x, z) + 0.032f, z), Quaternion.Euler(0f, (i % 4) * 7f, 0f), new Vector3(360f, 0.012f, 5.5f), dirt);
            }
        }

        private static void AddFarFoothills(Transform parent, Material foothill, Material mountain, Material snow)
        {
            Ridge(parent, "FrontRangeFoothillLowRidge", -4200f, 8300f, 2750f, 620f, 12f, 92f, 18, 11, foothill);
            Ridge(parent, "FrontRangeFoothillBackRidge", -4550f, 9000f, 3350f, 720f, 36f, 155f, 20, 29, foothill);
            Ridge(parent, "FrontRangeMountainBackRidge", -4700f, 9400f, 4300f, 940f, 82f, 330f, 22, 47, mountain);

            for (int i = 0; i < 9; i++)
            {
                float x = -3600f + i * 820f;
                float y = 285f + Mathf.Sin(i * 0.8f) * 34f;
                Cube(parent, $"FrontRangeSnowCap_{i}", new Vector3(x, y, 4300f + Mathf.Cos(i * 0.51f) * 70f), Quaternion.Euler(0f, i * 6f, -7f + i % 3 * 5f), new Vector3(260f, 11f, 42f), snow);
            }
        }

        private static void AddObjectLodGroups(Transform root)
        {
            foreach (Transform child in root)
            {
                if (!child.name.StartsWith("AirportIndustrial_", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("FrontRange", StringComparison.OrdinalIgnoreCase) &&
                    !child.name.StartsWith("LowBuildingFar_", StringComparison.OrdinalIgnoreCase))
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

        private static GameObject TerrainChunk(Transform parent, string name, Vector3 center, float size, Material material)
        {
            const int resolution = 8;
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

        private static Material Material(string name, Color color, float metallic, float smoothness)
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
                material.mainTextureScale = new Vector2(8f, 8f);
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

        private static float TerrainHeight(float x, float z)
        {
            float runwayFlat = Mathf.Clamp01(1f - Mathf.Max(Mathf.Abs(x) / 1300f, Mathf.Abs(z) / 720f));
            float northRise = Mathf.Clamp01((z - 980f) / 2700f) * 20f;
            float westRise = Mathf.Clamp01((-x - 1450f) / 2600f) * 6f;
            float ripple = Mathf.Sin(x * 0.0041f + z * 0.0028f) * 2.2f +
                           Mathf.Sin(x * 0.0082f - z * 0.0019f) * 1.1f;
            float height = -0.18f + northRise + westRise + ripple;
            return Mathf.Lerp(height, -0.18f, runwayFlat);
        }

        private static Texture2D NoiseTexture(string name, Color color)
        {
            const int size = 64;
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
                    float band = Mathf.Sin((x + seed % 13) * 0.43f + y * 0.17f) * 0.035f;
                    float scale = 0.86f + n * 0.22f + band;
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
        public string notes;
    }
}
