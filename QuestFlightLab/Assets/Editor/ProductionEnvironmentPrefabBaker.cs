using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuestFlightLab.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.EditorTools
{
    /// <summary>
    /// Editor-only authoring pipeline. It consumes the pinned FAA/USGS/OSM derivatives and saves
    /// finished Unity assets. Production runtime code never invokes this builder.
    /// </summary>
    public static class ProductionEnvironmentPrefabBaker
    {
        public const string ToolVersion = "production-environment-v2.2";
        public const string RootFolder = "Assets/Production/Environment";
        public const string GeneratedFolder = RootFolder + "/Generated";
        public const string MaterialFolder = RootFolder + "/Materials";
        public const string TextureFolder = RootFolder + "/Textures";
        public const string MacroTexturePath = TextureFolder + "/ProductionKbduMacroAlbedo.png";
        public const string PrefabPath = ProductionEnvironmentRoot.PrefabAssetPath;
        private const string TerrainJsonPath = "Assets/Resources/QuestFlightLab/Environment/KBDU/kbdu_terrain_rings.json";
        private const string ContextJsonPath = "Assets/Resources/QuestFlightLab/Environment/KBDU/kbdu_reference_context.json";
        private const string ProductionGroundShaderPath = "Assets/Resources/QuestFlightLab/Environment/Shaders/ProductionMacroGround.shader";
        private const string RunwayMarkingShaderPath = "Assets/Resources/QuestFlightLab/Environment/Shaders/ProductionRunwayMarking.shader";
        private const string ProductionWaterShaderPath = "Assets/Resources/QuestFlightLab/Environment/Shaders/ProductionStableWater.shader";
        private const string MalformedAirportRoadBatch = "RealContext_0_road_asphalt_021";
        private const float RunwaySurfaceOffsetMeters = 0.004f;
        private const float MarkingSurfaceOffsetMeters = 0.001f;
        private const int MacroResolution = 1024;

        [MenuItem("Quest Flight Lab/Production/Bake Production Environment Prefab")]
        public static void BakeMenu() => Bake();

        public static void BakeProductionEnvironmentBatch()
        {
            try
            {
                Bake();
                Debug.Log("[QuestFlightLab][ProductionEnvironmentBake] SUCCESS " + PrefabPath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static GameObject Bake()
        {
            EnsureFolders();
            DeleteGeneratedOutputs();
            TerrainDocument terrainDocument = ReadJson<TerrainDocument>(TerrainJsonPath);
            ContextDocument contextDocument = ReadJson<ContextDocument>(ContextJsonPath);
            ValidatePinnedSources(terrainDocument, contextDocument);
            TerrainSampler sampler = new TerrainSampler(terrainDocument);

            Texture2D macro = BuildAndImportMacroTexture(contextDocument, sampler, out float repeatedSimilarity);
            MaterialSet materials = CreateMaterials(macro);
            ValidateAndRenderProductionShaders(materials);
            GameObject sourceContainer = null;
            GameObject authoredRoot = null;
            try
            {
                sourceContainer = new GameObject("__ProductionEnvironmentBakeSource");
                if (!RealKbduEnvironmentBuilder.TryBuild(sourceContainer.transform, out GameObject sourceWorld, out string error))
                    throw new InvalidOperationException("Pinned USGS terrain could not be author-baked: " + error);

                authoredRoot = new GameObject("ProductionEnvironmentRoot");
                SetStatic(authoredRoot);
                ProductionEnvironmentRoot contract = authoredRoot.AddComponent<ProductionEnvironmentRoot>();
                contract.validateOnAwake = true;
                contract.macroRepeatedTileSimilarity = repeatedSimilarity;
                contract.uniqueMacroAlbedo = macro;
                contract.bakeUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                contract.bakeToolVersion = ToolVersion;

                Transform near = Child(authoredRoot.transform, "NearProductionZone_4km");
                Transform nearTerrain = Child(near, "USGS_Terrain");
                Transform airportContext = Child(near, "AirportContext");
                Transform mid = Child(authoredRoot.transform, "MidContextZone_12km");
                Transform midTerrain = Child(mid, "USGS_Terrain");
                Transform midVectors = Child(mid, "OSM_Context");
                Transform far = Child(authoredRoot.transform, "ImmutableFarTerrain_24km");
                Transform runway = Child(authoredRoot.transform, "AuthoritativeRunwaySystem");
                Transform water = Child(authoredRoot.transform, "EssentialWater");
                contract.nearProductionZone = near;
                contract.midContextZone = mid;
                contract.immutableFarTerrain = far;
                contract.airportContext = airportContext;
                contract.authoritativeRunwaySystem = runway;
                contract.essentialWater = water;

                contract.nearTerrainMeshes = new[]
                {
                    BakeSourceMesh(sourceWorld.transform.Find("RealTerrain_airport_patch"), nearTerrain, "Terrain_AirportPatch_USGS", materials.macroGround),
                    BakeSourceMesh(sourceWorld.transform.Find("RealTerrain_inner_4km"), nearTerrain, "Terrain_Near4km_USGS", materials.macroGround)
                };
                ApplyAuthoritativeRunwayTerrainBlend(contract.nearTerrainMeshes[0].sharedMesh, terrainDocument, contextDocument);
                contract.midTerrainMesh = BakeSourceMesh(
                    sourceWorld.transform.Find("RealTerrain_mid_12km"), midTerrain, "Terrain_Mid12km_USGS", materials.macroGround);
                contract.farTerrainMesh = BakeSourceMesh(
                    sourceWorld.transform.Find("RealTerrain_far_24km"), far, "Terrain_Far24km_Immutable_USGS", materials.farTerrain);
                SmoothImmutableFarTerrain(contract.farTerrainMesh.sharedMesh, contract);

                BakeSelectedContext(sourceWorld.transform, airportContext, midVectors, materials, contract);
                BakeVegetationContext(airportContext, sampler, materials.vegetation);
                BakeRunway(runway, terrainDocument, contextDocument, materials, contract, contract.nearTerrainMeshes[0].sharedMesh);
                BakeEssentialWater(water, contextDocument, sampler, materials, contract);

                contract.macroWorldMinimumMeters = new Vector2(-6000f, -6000f);
                contract.macroWorldSizeMeters = new Vector2(12000f, 12000f);
                ProductionEnvironmentBudget budget = contract.CalculateBudget();
                contract.bakedRendererCount = budget.rendererCount;
                contract.bakedTriangleCount = budget.triangleCount;
                contract.bakedMaterialCount = budget.materialCount;
                contract.bakedTextureCount = 4;
                EditorUtility.SetDirty(contract);

                if (!contract.TryValidateContract(out string report))
                    throw new InvalidOperationException("Production environment contract rejected before save: " + report);

                PrefabUtility.SaveAsPrefabAsset(authoredRoot, PrefabPath, out bool saved);
                if (!saved) throw new InvalidOperationException("PrefabUtility did not save " + PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Saved production environment prefab could not be reloaded");
                Debug.Log($"[QuestFlightLab][ProductionEnvironmentBake] {report}; macroSimilarity={repeatedSimilarity:0.000}; " +
                          $"asset={PrefabPath}");
                return prefab;
            }
            finally
            {
                if (authoredRoot != null) UnityEngine.Object.DestroyImmediate(authoredRoot);
                if (sourceContainer != null) UnityEngine.Object.DestroyImmediate(sourceContainer);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Production");
            EnsureFolder("Assets/Production", "Environment");
            EnsureFolder(RootFolder, "Generated");
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder, "Textures");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        private static void DeleteGeneratedOutputs()
        {
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { GeneratedFolder, MaterialFolder, TextureFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        private static T ReadJson<T>(string path)
        {
            string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
            if (value == null) throw new InvalidOperationException("Could not deserialize " + path);
            return value;
        }

        private static void ValidatePinnedSources(TerrainDocument terrain, ContextDocument context)
        {
            if (terrain.schema_version != 1 || terrain.layers == null || terrain.layers.Length != 4)
                throw new InvalidOperationException("Pinned USGS terrain schema/layers are invalid");
            if (terrain.source_snapshot == null || terrain.source_snapshot.id != "20260710T214309Z")
                throw new InvalidOperationException("Unexpected USGS source snapshot");
            if (context.faa?.runways == null || !context.faa.runways.Any(item => item.runway_id == "08/26"))
                throw new InvalidOperationException("Pinned FAA runway 08/26 is missing");
            if (context.openstreetmap?.features == null || context.openstreetmap.attribution != "© OpenStreetMap contributors")
                throw new InvalidOperationException("Pinned OSM context/attribution is missing");
        }

        private static MeshFilter BakeSourceMesh(Transform source, Transform parent, string name, Material material)
        {
            if (source == null) throw new InvalidOperationException("Missing author-bake source mesh " + name);
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter?.sharedMesh == null) throw new InvalidOperationException("Source mesh is empty: " + source.name);
            return CreateBakedMeshObject(parent, name, sourceFilter.sharedMesh, material, source.GetComponent<Renderer>());
        }

        private static MeshFilter CreateBakedMeshObject(
            Transform parent,
            string name,
            Mesh sourceMesh,
            Material material,
            Renderer presentationSource = null)
        {
            Mesh mesh = UnityEngine.Object.Instantiate(sourceMesh);
            mesh.name = name + "_Mesh";
            string meshPath = GeneratedFolder + "/" + SafeName(mesh.name) + ".asset";
            AssetDatabase.CreateAsset(mesh, meshPath);
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SetStatic(go);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = presentationSource != null ? presentationSource.shadowCastingMode : ShadowCastingMode.Off;
            renderer.receiveShadows = presentationSource != null && presentationSource.receiveShadows;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            return filter;
        }

        private static void BakeSelectedContext(
            Transform sourceWorld,
            Transform near,
            Transform mid,
            MaterialSet materials,
            ProductionEnvironmentRoot contract)
        {
            int index = 0;
            int omittedLandcover = 0;
            int rejectedMalformedGround = 0;
            foreach (Transform source in sourceWorld.Cast<Transform>().OrderBy(item => item.name, StringComparer.Ordinal))
            {
                bool nearBatch = source.name.StartsWith("RealContext_0_", StringComparison.Ordinal);
                bool midBatch = source.name.StartsWith("RealContext_1_", StringComparison.Ordinal);
                if (!nearBatch && !midBatch) continue;
                if (source.name.IndexOf("_water_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    source.name.IndexOf("water_bank", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    source.name.IndexOf("_barrier_", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
                if (sourceFilter?.sharedMesh == null) continue;
                if (source.name.IndexOf("_landcover_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // The unique production macro atlas already contains this OSM land-cover signal.
                    // Keeping the legacy coplanar polygon batches produced blocky overlays and extra draws.
                    omittedLandcover++;
                    continue;
                }
                if (IsMalformedGroundContextBatch(source.name, sourceFilter.sharedMesh, nearBatch))
                {
                    rejectedMalformedGround++;
                    continue;
                }
                Transform parent = nearBatch ? near : mid;
                string name = "Context_" + index.ToString("000", CultureInfo.InvariantCulture) + "_" + source.name;
                CreateBakedMeshObject(parent, name, sourceFilter.sharedMesh, materials.ForContext(source.name), source.GetComponent<Renderer>());
                index++;
            }
            contract.omittedDuplicatedLandcoverBatchCount = omittedLandcover;
            contract.rejectedMalformedGroundBatchCount = rejectedMalformedGround;
        }

        private static bool IsMalformedGroundContextBatch(string name, Mesh mesh, bool nearBatch)
        {
            // This pinned batch crosses the independently sampled airport-patch/inner-ring seam and
            // forms a tall ribbon wall. Keep the exact rejection deterministic even if source batching
            // changes triangle order, then guard future near-airport road batches by local grade.
            if (string.Equals(name, MalformedAirportRoadBatch, StringComparison.Ordinal)) return true;
            if (!nearBatch || name.IndexOf("_road_", StringComparison.OrdinalIgnoreCase) < 0) return false;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                if (HasImplausibleGroundEdge(vertices[triangles[index]], vertices[triangles[index + 1]]) ||
                    HasImplausibleGroundEdge(vertices[triangles[index + 1]], vertices[triangles[index + 2]]) ||
                    HasImplausibleGroundEdge(vertices[triangles[index + 2]], vertices[triangles[index]]))
                    return true;
            }
            return false;
        }

        private static bool HasImplausibleGroundEdge(Vector3 first, Vector3 second)
        {
            float vertical = Mathf.Abs(second.y - first.y);
            if (vertical <= 1.5f) return false;
            float horizontal = Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));
            return horizontal <= 0.05f || (horizontal < 45f && vertical / horizontal > 0.12f);
        }

        private static void BakeVegetationContext(Transform parent, TerrainSampler sampler, Material material)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int index = 0; index < 128; index++)
            {
                float x = -1850f + Hash01(index * 17 + 3) * 3700f;
                float z = -1750f + Hash01(index * 29 + 7) * 3500f;
                if (Mathf.Abs(z + 30f) < 120f && Mathf.Abs(x) < 850f) continue;
                if (Mathf.Abs(z + 240f) < 100f && x > -1100f && x < 900f) continue;
                float baseY = sampler.Sample(x, z) + 0.02f;
                float radius = 2.5f + Hash01(index * 43 + 13) * 2.2f;
                float height = 7f + Hash01(index * 61 + 19) * 5f;
                int start = vertices.Count;
                vertices.Add(new Vector3(x - radius, baseY + height * 0.45f, z));
                vertices.Add(new Vector3(x + radius, baseY + height * 0.45f, z));
                vertices.Add(new Vector3(x, baseY + height * 0.45f, z - radius));
                vertices.Add(new Vector3(x, baseY + height * 0.45f, z + radius));
                vertices.Add(new Vector3(x, baseY + height, z));
                vertices.Add(new Vector3(x, baseY + 1.1f, z));
                triangles.AddRange(new[]
                {
                    start, start + 4, start + 2, start + 2, start + 4, start + 1,
                    start + 1, start + 4, start + 3, start + 3, start + 4, start,
                    start, start + 2, start + 5, start + 2, start + 1, start + 5,
                    start + 1, start + 3, start + 5, start + 3, start, start + 5
                });
            }
            Mesh mesh = new Mesh { name = "SparseCottonwoodContext_Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            CreateBakedMeshObject(parent, "SparseCottonwoodContext", mesh, material);
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        private static void BakeRunway(
            Transform parent,
            TerrainDocument terrain,
            ContextDocument context,
            MaterialSet materials,
            ProductionEnvironmentRoot contract,
            Mesh flattenedAirportTerrain)
        {
            FaaRunway runway = context.faa.runways.First(item => item.runway_id == "08/26");
            FaaEndpoint first = runway.endpoints[0];
            FaaEndpoint second = runway.endpoints[1];
            float datum = (float)terrain.origin.elevation_msl_meters;
            Vector3 end08 = new Vector3(first.x_east_meters, first.usgs_elevation_msl_meters - datum + RunwaySurfaceOffsetMeters, first.z_north_meters);
            Vector3 end26 = new Vector3(second.x_east_meters, second.usgs_elevation_msl_meters - datum + RunwaySurfaceOffsetMeters, second.z_north_meters);
            Vector3 horizontal = end26 - end08;
            horizontal.y = 0f;
            float length = horizontal.magnitude;
            Vector3 forward = horizontal.normalized;
            Vector3 left = new Vector3(-forward.z, 0f, forward.x);
            float width = runway.width_feet * 0.3048f;
            float halfWidth = width * 0.5f;
            const int pavementSegments = 52;
            List<Vector3> pavementVertices = new List<Vector3>((pavementSegments + 1) * 2);
            List<Vector2> pavementUvs = new List<Vector2>((pavementSegments + 1) * 2);
            List<int> pavementTriangles = new List<int>(pavementSegments * 6);
            for (int segment = 0; segment <= pavementSegments; segment++)
            {
                float t = segment / (float)pavementSegments;
                Vector3 center = Vector3.Lerp(end08, end26, t);
                pavementVertices.Add(center + left * halfWidth);
                pavementVertices.Add(center - left * halfWidth);
                pavementUvs.Add(new Vector2(0f, t * length / 32f));
                pavementUvs.Add(new Vector2(1f, t * length / 32f));
                if (segment == pavementSegments) continue;
                int start = segment * 2;
                pavementTriangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
            }
            Mesh pavement = MeshFrom(
                "FAA_KBDU_Runway_08_26_Authoritative_Mesh",
                pavementVertices.ToArray(),
                pavementTriangles.ToArray(),
                pavementUvs.ToArray());
            MeshFilter surface = CreateBakedMeshObject(parent, "FAA_KBDU_Runway_08_26_Authoritative", pavement, materials.runway);
            UnityEngine.Object.DestroyImmediate(pavement);
            MeshCollider collider = surface.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = surface.sharedMesh;

            List<Vector3> markingVertices = new List<Vector3>();
            List<int> markingTriangles = new List<int>();
            for (int dash = 0; dash < 12; dash++)
                AddRunwayRectangle(markingVertices, markingTriangles, end08, end26, forward, left, 0.09f + dash * 0.074f, 26f, 0.85f);
            int thresholdStripeQuads = AddRunwayThresholdStripes(
                markingVertices, markingTriangles, end08, end26, forward, left, 0.055f, halfWidth);
            thresholdStripeQuads += AddRunwayThresholdStripes(
                markingVertices, markingTriangles, end08, end26, forward, left, 0.945f, halfWidth);
            AddRunwayEdgeLine(markingVertices, markingTriangles, end08, end26, left, halfWidth - 0.45f, 0.55f);
            AddRunwayEdgeLine(markingVertices, markingTriangles, end08, end26, left, -halfWidth + 0.45f, 0.55f);
            Mesh markingMesh = MeshFrom("FAA_KBDU_Runway_08_26_ControlledDepthMarkings_Mesh", markingVertices.ToArray(), markingTriangles.ToArray());
            MeshFilter markings = CreateBakedMeshObject(parent, "FAA_KBDU_Runway_08_26_ControlledDepthMarkings", markingMesh, materials.runwayMarking);
            UnityEngine.Object.DestroyImmediate(markingMesh);

            TerrainLayer airportPatch = terrain.layers.Single(layer => layer.id == "airport_patch");
            float maximumGap = 0f;
            for (int along = 0; along <= 20; along++)
            {
                float t = along / 20f;
                Vector3 center = Vector3.Lerp(end08, end26, t);
                for (int across = -2; across <= 2; across++)
                {
                    Vector3 point = center + left * (halfWidth * 0.45f * across);
                    float terrainY = SampleRegularGridMesh(flattenedAirportTerrain, airportPatch, point.x, point.z);
                    maximumGap = Mathf.Max(maximumGap, Mathf.Abs(point.y - terrainY));
                }
            }
            contract.runwayPavement = surface;
            contract.runwayMarkings = markings.GetComponent<MeshRenderer>();
            contract.runwayCollisionSurface = collider;
            contract.runway08EndLocal = end08;
            contract.runway26EndLocal = end26;
            contract.runwayLengthMeters = length;
            contract.runwayWidthMeters = width;
            contract.runwayGradeDeltaMeters = end26.y - end08.y;
            contract.measuredRunwayToTerrainMaximumGapMeters = maximumGap;
            contract.measuredMarkingToRunwayMaximumGapMeters = MarkingSurfaceOffsetMeters;
            contract.measuredCollisionSurfaceDisagreementMeters = 0f;
            contract.bakedRunwayThresholdStripeQuadCount = thresholdStripeQuads;
            contract.bakedRunwayMarkingQuadCount = markingTriangles.Count / 6;
        }

        private static void ApplyAuthoritativeRunwayTerrainBlend(Mesh terrainMesh, TerrainDocument terrain, ContextDocument context)
        {
            FaaRunway runway = context.faa.runways.First(item => item.runway_id == "08/26");
            FaaEndpoint first = runway.endpoints[0];
            FaaEndpoint second = runway.endpoints[1];
            float datum = (float)terrain.origin.elevation_msl_meters;
            Vector3 firstPlane = new Vector3(first.x_east_meters, first.usgs_elevation_msl_meters - datum, first.z_north_meters);
            Vector3 secondPlane = new Vector3(second.x_east_meters, second.usgs_elevation_msl_meters - datum, second.z_north_meters);
            Vector2 firstXz = new Vector2(firstPlane.x, firstPlane.z);
            Vector2 secondXz = new Vector2(secondPlane.x, secondPlane.z);
            Vector2 direction = (secondXz - firstXz).normalized;
            Vector2 left = new Vector2(-direction.y, direction.x);
            float length = Vector2.Distance(firstXz, secondXz);
            float coreHalfWidth = runway.width_feet * 0.3048f * 0.5f + 4f;
            float outerHalfWidth = coreHalfWidth + 40f;
            const float endBlendMeters = 55f;
            Vector3[] vertices = terrainMesh.vertices;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector2 point = new Vector2(vertices[index].x, vertices[index].z);
                Vector2 relative = point - firstXz;
                float along = Vector2.Dot(relative, direction);
                float lateral = Mathf.Abs(Vector2.Dot(relative, left));
                if (lateral >= outerHalfWidth || along <= -endBlendMeters || along >= length + endBlendMeters) continue;
                float lateralWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coreHalfWidth, outerHalfWidth, lateral));
                float endWeight = 1f;
                if (along < 0f) endWeight = 1f - Mathf.SmoothStep(0f, 1f, -along / endBlendMeters);
                else if (along > length) endWeight = 1f - Mathf.SmoothStep(0f, 1f, (along - length) / endBlendMeters);
                float weight = Mathf.Min(lateralWeight, endWeight);
                float planeY = Mathf.Lerp(firstPlane.y, secondPlane.y, Mathf.Clamp01(along / length));
                vertices[index].y = Mathf.Lerp(vertices[index].y, planeY, weight);
            }
            terrainMesh.vertices = vertices;
            terrainMesh.RecalculateNormals();
            terrainMesh.RecalculateBounds();
            EditorUtility.SetDirty(terrainMesh);
        }

        private static void SmoothImmutableFarTerrain(Mesh mesh, ProductionEnvironmentRoot contract)
        {
            int sourceVertexCount = mesh.vertexCount;
            int sourceIndexCount = (int)mesh.GetIndexCount(0);
            int[] topology = mesh.triangles;
            string topologyHash = TopologyHash(sourceVertexCount, topology);
            Vector3[] vertices = mesh.vertices;
            HashSet<int>[] neighbors = Enumerable.Range(0, vertices.Length).Select(_ => new HashSet<int>()).ToArray();
            for (int index = 0; index + 2 < topology.Length; index += 3)
            {
                int a = topology[index];
                int b = topology[index + 1];
                int c = topology[index + 2];
                neighbors[a].Add(b); neighbors[a].Add(c);
                neighbors[b].Add(a); neighbors[b].Add(c);
                neighbors[c].Add(a); neighbors[c].Add(b);
            }

            const int passes = 2;
            const float blend = 0.18f;
            for (int pass = 0; pass < passes; pass++)
            {
                Vector3[] next = (Vector3[])vertices.Clone();
                for (int index = 0; index < vertices.Length; index++)
                {
                    float squareRadius = Mathf.Max(Mathf.Abs(vertices[index].x), Mathf.Abs(vertices[index].z));
                    if (squareRadius <= 6200f || squareRadius >= 11800f || neighbors[index].Count < 3) continue;
                    float averageY = 0f;
                    foreach (int neighbor in neighbors[index]) averageY += vertices[neighbor].y;
                    averageY /= neighbors[index].Count;
                    next[index].y = Mathf.Lerp(vertices[index].y, averageY, blend);
                }
                vertices = next;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            contract.farTerrainSourceVertexCount = sourceVertexCount;
            contract.farTerrainSourceIndexCount = sourceIndexCount;
            contract.farTerrainSourceTopologyHash = topologyHash;
            contract.farTerrainBakedTopologyHash = TopologyHash(mesh.vertexCount, mesh.triangles);
            contract.farTerrainSmoothingPassCount = passes;
        }

        private static string TopologyHash(int vertexCount, IReadOnlyList<int> indices)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)vertexCount) * 16777619u;
                for (int index = 0; index < indices.Count; index++) hash = (hash ^ (uint)indices[index]) * 16777619u;
                return hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private static float SampleRegularGridMesh(Mesh mesh, TerrainLayer layer, float x, float z)
        {
            Vector3[] vertices = mesh.vertices;
            float column = Mathf.Clamp((x - layer.min_x_meters) / layer.spacing_meters, 0f, layer.width - 1.0001f);
            float row = Mathf.Clamp((z - layer.min_z_meters) / layer.spacing_meters, 0f, layer.height - 1.0001f);
            int x0 = Mathf.FloorToInt(column);
            int z0 = Mathf.FloorToInt(row);
            int x1 = Mathf.Min(x0 + 1, layer.width - 1);
            int z1 = Mathf.Min(z0 + 1, layer.height - 1);
            float tx = column - x0;
            float tz = row - z0;
            float southWest = vertices[z0 * layer.width + x0].y;
            float southEast = vertices[z0 * layer.width + x1].y;
            float northWest = vertices[z1 * layer.width + x0].y;
            float northEast = vertices[z1 * layer.width + x1].y;
            if (tx + tz <= 1f)
                return southWest + tx * (southEast - southWest) + tz * (northWest - southWest);
            return northEast + (1f - tx) * (northWest - northEast) + (1f - tz) * (southEast - northEast);
        }

        private static void AddRunwayRectangle(
            List<Vector3> vertices, List<int> triangles, Vector3 first, Vector3 second,
            Vector3 forward, Vector3 left, float t, float length, float width)
        {
            Vector3 center = Vector3.Lerp(first, second, t) + Vector3.up * MarkingSurfaceOffsetMeters;
            int start = vertices.Count;
            vertices.Add(center - forward * (length * 0.5f) + left * (width * 0.5f));
            vertices.Add(center + forward * (length * 0.5f) + left * (width * 0.5f));
            vertices.Add(center - forward * (length * 0.5f) - left * (width * 0.5f));
            vertices.Add(center + forward * (length * 0.5f) - left * (width * 0.5f));
            triangles.AddRange(new[] { start, start + 1, start + 2, start + 2, start + 1, start + 3 });
        }

        private static int AddRunwayThresholdStripes(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 first,
            Vector3 second,
            Vector3 forward,
            Vector3 left,
            float t,
            float halfWidth)
        {
            // FAA 75-foot runway presentation: three longitudinal stripes on either side of
            // centerline. Keeping all twelve end stripes in the combined marking mesh avoids
            // the prior solid cream slab while preserving one draw call.
            const int stripesPerSide = 3;
            const float stripeLength = 9f;
            const float stripeWidth = 1.6f;
            const float stripeGap = 0.8f;
            const float centerlineClearHalfWidth = 1.35f;
            float firstLateral = centerlineClearHalfWidth + stripeWidth * 0.5f;
            int count = 0;
            for (int side = -1; side <= 1; side += 2)
            for (int stripe = 0; stripe < stripesPerSide; stripe++)
            {
                float lateral = side * (firstLateral + stripe * (stripeWidth + stripeGap));
                if (Mathf.Abs(lateral) + stripeWidth * 0.5f >= halfWidth - 0.8f)
                    throw new InvalidOperationException("Runway threshold stripe layout exceeds the authoritative pavement width");
                Vector3 shiftedFirst = first + left * lateral;
                Vector3 shiftedSecond = second + left * lateral;
                AddRunwayRectangle(vertices, triangles, shiftedFirst, shiftedSecond, forward, left, t, stripeLength, stripeWidth);
                count++;
            }
            return count;
        }

        private static void AddRunwayEdgeLine(
            List<Vector3> vertices, List<int> triangles, Vector3 first, Vector3 second, Vector3 left, float lateral, float width)
        {
            Vector3 shiftedFirst = first + left * lateral;
            Vector3 shiftedSecond = second + left * lateral;
            Vector3 forward = (shiftedSecond - shiftedFirst).normalized;
            AddRunwayRectangle(vertices, triangles, shiftedFirst, shiftedSecond, forward, left, 0.5f,
                Vector3.Distance(shiftedFirst, shiftedSecond) - 2f, width);
        }

        private static void BakeEssentialWater(
            Transform parent,
            ContextDocument context,
            TerrainSampler sampler,
            MaterialSet materials,
            ProductionEnvironmentRoot contract)
        {
            ContextFeature feature = context.openstreetmap.features.Single(item =>
                item.category == "water" && item.geometry_type == "polygon" && item.tags?.name == "Boulder Reservoir");
            List<Vector2> sourcePolygon = DecodePoints(feature.points_q, context.coordinate_quantization_meters);
            List<Vector2> polygon = SmoothClosedPolygon(sourcePolygon);
            int selfIntersections = CountPolygonSelfIntersections(polygon);
            if (selfIntersections != 0)
                throw new InvalidOperationException($"Smoothed Boulder Reservoir shoreline self-intersects {selfIntersections} time(s)");
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            WaterwayMeshBuilder.MeshBuffers buffers = new WaterwayMeshBuilder.MeshBuffers(vertices, uvs, triangles);
            if (!WaterwayMeshBuilder.TryAppendReservoir(polygon, sampler.Sample, buffers, out WaterwayMeshBuilder.BuildStatistics statistics))
                throw new InvalidOperationException("Boulder Reservoir OSM polygon failed stable triangulation");
            Mesh waterMesh = MeshFrom("BoulderReservoir_OSM_AuthoritativeWater_Mesh", vertices.ToArray(), triangles.ToArray(), uvs.ToArray());
            MeshFilter water = CreateBakedMeshObject(parent, "BoulderReservoir_OSM_AuthoritativeWater", waterMesh, materials.water);
            UnityEngine.Object.DestroyImmediate(waterMesh);

            float waterY = vertices[0].y;
            bool clockwise = PolygonSignedArea(polygon) < 0f;
            List<Vector3> bankVertices = new List<Vector3>(polygon.Count * 3);
            List<int> bankTriangles = new List<int>(polygon.Count * 12);
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 point = polygon[index];
                Vector2 outward = ShorelineOutwardNormal(polygon, index, clockwise);
                Vector2 middle = point + outward * 7f;
                Vector2 outer = point + outward * 16f;
                float middleTerrain = sampler.Sample(middle.x, middle.y) + 0.035f;
                bankVertices.Add(new Vector3(point.x, waterY + 0.012f, point.y));
                bankVertices.Add(new Vector3(middle.x, Mathf.Lerp(waterY + 0.008f, middleTerrain, 0.62f), middle.y));
                bankVertices.Add(new Vector3(outer.x, sampler.Sample(outer.x, outer.y) + 0.035f, outer.y));
            }
            for (int index = 0; index < polygon.Count; index++)
            {
                int next = (index + 1) % polygon.Count;
                AddShoreRingQuad(bankTriangles, index * 3, next * 3, index * 3 + 1, next * 3 + 1, clockwise);
                AddShoreRingQuad(bankTriangles, index * 3 + 1, next * 3 + 1, index * 3 + 2, next * 3 + 2, clockwise);
            }
            Mesh bankMesh = MeshFrom("BoulderReservoir_NaturalShoreBank_Mesh", bankVertices.ToArray(), bankTriangles.ToArray());
            MeshFilter bank = CreateBakedMeshObject(parent, "BoulderReservoir_NaturalShoreBank", bankMesh, materials.waterBank);
            UnityEngine.Object.DestroyImmediate(bankMesh);
            contract.boulderReservoirSurface = water;
            contract.boulderReservoirShoreBank = bank;
            contract.retainedWaterBodyCount = 1;
            contract.discardedMinorWaterFeatureCount = context.openstreetmap.features.Count(item => item.category == "water") - 1;
            contract.minimumWaterTerrainSeparationMeters = statistics.minimumTerrainSeparationMeters;
            contract.boulderReservoirSourceShorelineVertexCount = sourcePolygon.Count;
            contract.boulderReservoirSmoothedShorelineVertexCount = polygon.Count;
            contract.boulderReservoirShorelineClosed = polygon.Count >= 3;
            contract.boulderReservoirShorelineSelfIntersectionCount = selfIntersections;
        }

        private static List<Vector2> SmoothClosedPolygon(IReadOnlyList<Vector2> source)
        {
            if (source == null || source.Count < 3)
                throw new InvalidOperationException("A closed shoreline requires at least three source vertices");
            List<Vector2> smoothed = new List<Vector2>(source.Count * 2);
            for (int index = 0; index < source.Count; index++)
            {
                Vector2 current = source[index];
                Vector2 next = source[(index + 1) % source.Count];
                smoothed.Add(Vector2.Lerp(current, next, 0.25f));
                smoothed.Add(Vector2.Lerp(current, next, 0.75f));
            }
            return smoothed;
        }

        private static Vector2 ShorelineOutwardNormal(IReadOnlyList<Vector2> polygon, int index, bool clockwise)
        {
            Vector2 previous = polygon[(index + polygon.Count - 1) % polygon.Count];
            Vector2 current = polygon[index];
            Vector2 next = polygon[(index + 1) % polygon.Count];
            Vector2 incoming = (current - previous).normalized;
            Vector2 outgoing = (next - current).normalized;
            Vector2 incomingLeft = new Vector2(-incoming.y, incoming.x);
            Vector2 outgoingLeft = new Vector2(-outgoing.y, outgoing.x);
            Vector2 outward = clockwise ? incomingLeft + outgoingLeft : -incomingLeft - outgoingLeft;
            if (outward.sqrMagnitude < 0.01f) outward = clockwise ? outgoingLeft : -outgoingLeft;
            return outward.normalized;
        }

        private static void AddShoreRingQuad(
            ICollection<int> triangles,
            int inner,
            int nextInner,
            int outer,
            int nextOuter,
            bool clockwise)
        {
            if (clockwise)
            {
                triangles.Add(inner); triangles.Add(nextInner); triangles.Add(outer);
                triangles.Add(outer); triangles.Add(nextInner); triangles.Add(nextOuter);
            }
            else
            {
                triangles.Add(inner); triangles.Add(outer); triangles.Add(nextInner);
                triangles.Add(outer); triangles.Add(nextOuter); triangles.Add(nextInner);
            }
        }

        private static float PolygonSignedArea(IReadOnlyList<Vector2> polygon)
        {
            float area = 0f;
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 current = polygon[index];
                Vector2 next = polygon[(index + 1) % polygon.Count];
                area += current.x * next.y - next.x * current.y;
            }
            return area * 0.5f;
        }

        private static int CountPolygonSelfIntersections(IReadOnlyList<Vector2> polygon)
        {
            int intersections = 0;
            for (int first = 0; first < polygon.Count; first++)
            {
                int firstNext = (first + 1) % polygon.Count;
                for (int second = first + 1; second < polygon.Count; second++)
                {
                    int secondNext = (second + 1) % polygon.Count;
                    if (first == second || firstNext == second || secondNext == first) continue;
                    if (first == 0 && secondNext == 0) continue;
                    if (SegmentsIntersect(polygon[first], polygon[firstNext], polygon[second], polygon[secondNext])) intersections++;
                }
            }
            return intersections;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float abC = Cross2D(b - a, c - a);
            float abD = Cross2D(b - a, d - a);
            float cdA = Cross2D(d - c, a - c);
            float cdB = Cross2D(d - c, b - c);
            const float epsilon = 0.0001f;
            return ((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon)) &&
                   ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon));
        }

        private static float Cross2D(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x;

        private static Mesh MeshFrom(string name, Vector3[] vertices, int[] triangles, Vector2[] uvs = null)
        {
            Mesh mesh = new Mesh { name = name };
            if (vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            if (uvs != null && uvs.Length == vertices.Length) mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D BuildAndImportMacroTexture(ContextDocument context, TerrainSampler sampler, out float repeatedSimilarity)
        {
            Texture2D texture = new Texture2D(MacroResolution, MacroResolution, TextureFormat.RGB24, true, false)
            {
                name = "ProductionKbduMacroAlbedo",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            Color32[] pixels = new Color32[MacroResolution * MacroResolution];
            Color[] fieldPalette =
            {
                Hex("#718052"), Hex("#778052"), Hex("#817A50"), Hex("#6D7C4E"), Hex("#7B744E"), Hex("#69754D")
            };
            const float worldMinimum = -6000f;
            const float worldSize = 12000f;
            for (int y = 0; y < MacroResolution; y++)
            {
                float z = worldMinimum + (y + 0.5f) / MacroResolution * worldSize;
                for (int x = 0; x < MacroResolution; x++)
                {
                    float east = worldMinimum + (x + 0.5f) / MacroResolution * worldSize;
                    float broad = ValueNoise(east * 0.00072f, z * 0.00072f, 17) * 0.17f;
                    float local = ValueNoise(east * 0.0031f, z * 0.0031f, 43) * 0.08f;
                    float warpX = east + ValueNoise(east * 0.00041f, z * 0.00041f, 71) * 760f;
                    float warpZ = z + ValueNoise(east * 0.00047f, z * 0.00047f, 97) * 690f;
                    IrregularParcel parcel = FindIrregularParcel(warpX, warpZ, 520f, 131);
                    Color parcelColor = fieldPalette[parcel.hash % (uint)fieldPalette.Length];
                    Color continuous = Color.Lerp(Hex("#697550"), Hex("#817658"), Mathf.Clamp01(0.48f + broad * 1.7f));
                    float elevation = sampler.Sample(east, z);
                    float slope = sampler.Slope(east, z, 60f);
                    float slopeSuppression = Mathf.Clamp01(slope / 0.24f);
                    float easternPlains = Mathf.SmoothStep(0.25f, 0.78f, Mathf.InverseLerp(-4600f, 3500f, east));
                    float parcelStrength = Mathf.Lerp(0.20f, 0.57f, easternPlains) * (1f - slopeSuppression * 0.72f);
                    Color color = Color.Lerp(continuous, parcelColor, parcelStrength);
                    float naturalEdge = Mathf.Lerp(0.86f, 1f, Mathf.SmoothStep(0.015f, 0.17f, parcel.edgeSeparation));
                    float modulation = (0.94f + broad * 0.36f + local) * naturalEdge - slopeSuppression * 0.15f +
                                       Mathf.Clamp(elevation / 800f, -0.05f, 0.08f);
                    pixels[y * MacroResolution + x] = (Color32)(color * modulation);
                }
            }

            foreach (ContextFeature feature in context.openstreetmap.features)
            {
                if (feature.points_q == null || feature.points_q.Length < 4) continue;
                List<Vector2> points = DecodePoints(feature.points_q, context.coordinate_quantization_meters);
                if (feature.geometry_type == "polygon" && (feature.category == "landcover" || feature.category == "aeroway"))
                {
                    PaintPolygon(pixels, points, MacroColor(feature), 0.72f);
                }
                else if (feature.geometry_type == "polyline" && (feature.category == "road" || feature.category == "aeroway"))
                {
                    PaintPolyline(pixels, points, Mathf.Max(7f, feature.render_width_meters), MacroColor(feature));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            repeatedSimilarity = QuadrantSimilarity(pixels, MacroResolution);
            byte[] png = texture.EncodeToPNG();
            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, MacroTexturePath);
            File.WriteAllBytes(absolutePath, png);
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(MacroTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(MacroTexturePath);
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = MacroResolution;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = MacroResolution;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 80;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(MacroTexturePath);
        }

        private static void PaintPolygon(Color32[] pixels, IReadOnlyList<Vector2> polygon, Color target, float blend)
        {
            if (polygon.Count < 3) return;
            const float minWorld = -6000f;
            const float scale = MacroResolution / 12000f;
            int minX = Mathf.Clamp(Mathf.FloorToInt((polygon.Min(p => p.x) - minWorld) * scale), 0, MacroResolution - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((polygon.Max(p => p.x) - minWorld) * scale), 0, MacroResolution - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt((polygon.Min(p => p.y) - minWorld) * scale), 0, MacroResolution - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt((polygon.Max(p => p.y) - minWorld) * scale), 0, MacroResolution - 1);
            for (int y = minY; y <= maxY; y++)
            {
                float worldZ = minWorld + (y + 0.5f) / scale;
                for (int x = minX; x <= maxX; x++)
                {
                    float worldX = minWorld + (x + 0.5f) / scale;
                    if (!PointInPolygon(new Vector2(worldX, worldZ), polygon)) continue;
                    int index = y * MacroResolution + x;
                    pixels[index] = (Color32)Color.Lerp(pixels[index], target, blend);
                }
            }
        }

        private static void PaintPolyline(Color32[] pixels, IReadOnlyList<Vector2> points, float widthMeters, Color color)
        {
            const float minWorld = -6000f;
            const float scale = MacroResolution / 12000f;
            int radius = Mathf.Clamp(Mathf.CeilToInt(widthMeters * 0.5f * scale), 1, 5);
            for (int segment = 1; segment < points.Count; segment++)
            {
                Vector2 first = (points[segment - 1] - Vector2.one * minWorld) * scale;
                Vector2 second = (points[segment] - Vector2.one * minWorld) * scale;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(first, second)));
                for (int step = 0; step <= steps; step++)
                {
                    Vector2 point = Vector2.Lerp(first, second, step / (float)steps);
                    int centerX = Mathf.RoundToInt(point.x);
                    int centerY = Mathf.RoundToInt(point.y);
                    for (int y = centerY - radius; y <= centerY + radius; y++)
                    for (int x = centerX - radius; x <= centerX + radius; x++)
                    {
                        if (x < 0 || y < 0 || x >= MacroResolution || y >= MacroResolution) continue;
                        if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) > radius * radius) continue;
                        pixels[y * MacroResolution + x] = (Color32)color;
                    }
                }
            }
        }

        private static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y + 0.000001f) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static float QuadrantSimilarity(Color32[] pixels, int size)
        {
            long difference = 0;
            long samples = 0;
            int half = size / 2;
            for (int y = 0; y < half; y += 4)
            for (int x = 0; x < half; x += 4)
            {
                Color32 a = pixels[y * size + x];
                Color32 b = pixels[(y + half) * size + x + half];
                difference += Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
                samples += 3;
            }
            return 1f - difference / (samples * 255f);
        }

        private static MaterialSet CreateMaterials(Texture2D macro)
        {
            Shader groundShader = AssetDatabase.LoadAssetAtPath<Shader>(ProductionGroundShaderPath);
            Shader markingShader = AssetDatabase.LoadAssetAtPath<Shader>(RunwayMarkingShaderPath);
            Shader waterShader = AssetDatabase.LoadAssetAtPath<Shader>(ProductionWaterShaderPath);
            Shader standard = Shader.Find("Standard");
            if (groundShader == null || markingShader == null || waterShader == null || standard == null)
                throw new InvalidOperationException("Production environment shaders are unavailable");
            Texture2D dry = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/QuestFlightLab/Environment/GroundMaterials/withered_grass_diff_1k.jpg");
            Texture2D green = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/QuestFlightLab/Environment/GroundMaterials/sparse_grass_diff_1k.jpg");
            Texture2D soil = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/QuestFlightLab/Environment/GroundMaterials/dry_ground_01_diff_1k.jpg");
            MaterialSet result = new MaterialSet
            {
                macroGround = Ground("Production_MacroGround", groundShader, macro, dry, green, soil, Color.white, 1f),
                macroGreen = Ground("Production_MacroGround_Green", groundShader, macro, dry, green, soil, Hex("#91A77C"), 0.92f),
                macroGold = Ground("Production_MacroGround_Harvest", groundShader, macro, dry, green, soil, Hex("#B3A06D"), 0.92f),
                farTerrain = Ground("Production_FarTerrain_Immutable", groundShader, macro, dry, green, soil, Hex("#7C806E"), 0.18f),
                asphalt = Standard("Production_Asphalt", standard, Hex("#303235"), 0.08f),
                concrete = Standard("Production_Concrete", standard, Hex("#777872"), 0.14f),
                building = Standard("Production_Building", standard, Hex("#777873"), 0.22f),
                gravel = Standard("Production_Gravel", standard, Hex("#746B59"), 0.05f),
                vegetation = Standard("Production_Vegetation", standard, Hex("#36552F"), 0.02f),
                runway = Standard("Production_Runway_Asphalt", standard, Hex("#282A2C"), 0.12f),
                runwayMarking = new Material(markingShader) { name = "Production_Runway_ControlledDepthMarking" },
                water = new Material(waterShader) { name = "Production_BoulderReservoir_Opaque", enableInstancing = true },
                waterBank = Ground("Production_WaterBank", groundShader, macro, dry, green, soil, Hex("#796D56"), 0.65f)
            };
            result.runwayMarking.SetColor("_Color", Hex("#D6D2C1"));
            result.water.SetColor("_Color", Hex("#285B6D"));
            result.water.SetColor("_HorizonColor", Hex("#88A7AE"));
            result.water.SetFloat("_Roughness", 0.46f);
            result.water.SetFloat("_RippleStrength", 0.72f);
            result.water.SetFloat("_ZWrite", 1f);
            result.water.SetOverrideTag("RenderType", "Opaque");
            result.water.renderQueue = (int)RenderQueue.Geometry + 5;
            result.SaveAll();
            return result;
        }

        private static void ValidateAndRenderProductionShaders(MaterialSet materials)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Production shader render validation requires a real graphics device");
            ShaderUtil.ClearShaderMessages(materials.macroGround.shader);
            ShaderUtil.ClearShaderMessages(materials.runwayMarking.shader);
            ShaderUtil.ClearShaderMessages(materials.water.shader);
            ShaderUtil.CompilePass(materials.macroGround, 0, true);
            ShaderUtil.CompilePass(materials.runwayMarking, 0, true);
            ShaderUtil.CompilePass(materials.water, 0, true);
            RenderMaterialProbe(materials.macroGround, "macro-ground");
            RenderMaterialProbe(materials.runwayMarking, "runway-marking");
            RenderMaterialProbe(materials.water, "stable-water");
            ValidateShaderMessages(materials.macroGround.shader);
            ValidateShaderMessages(materials.runwayMarking.shader);
            ValidateShaderMessages(materials.water.shader);
        }

        private static void ValidateShaderMessages(Shader shader)
        {
            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
            string[] errors = messages
                .Where(message => string.Equals(message.severity.ToString(), "Error", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.platform + ": " + message.message)
                .ToArray();
            if (errors.Length > 0)
                throw new InvalidOperationException("Shader compiler rejected " + shader.name + ": " + string.Join(" | ", errors));
        }

        private static void RenderMaterialProbe(Material material, string label)
        {
            GameObject sphere = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            RenderTexture target = null;
            Texture2D readback = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "__ProductionShaderProbe_" + label;
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                cameraObject = new GameObject("__ProductionShaderProbeCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.005f, 0.007f, 0.01f, 1f);
                camera.transform.position = new Vector3(0f, 0.4f, -3.2f);
                camera.transform.LookAt(Vector3.zero);
                lightObject = new GameObject("__ProductionShaderProbeLight");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
                target = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                readback = new Texture2D(96, 96, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
                readback.Apply(false, false);
                Color32[] pixels = readback.GetPixels32();
                int visible = 0;
                int magenta = 0;
                foreach (Color32 pixel in pixels)
                {
                    if (pixel.r + pixel.g + pixel.b > 28) visible++;
                    if (pixel.r > 210 && pixel.b > 210 && pixel.g < 85) magenta++;
                }
                if (visible < 300)
                    throw new InvalidOperationException($"Production {label} shader probe rendered empty ({visible} visible pixels)");
                if (magenta > 8)
                    throw new InvalidOperationException($"Production {label} shader probe rendered fallback magenta ({magenta} pixels)");
                Debug.Log($"[QuestFlightLab][ProductionEnvironmentBake] shaderProbe={label} visiblePixels={visible} magentaPixels={magenta}");
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) target.Release();
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                if (sphere != null) UnityEngine.Object.DestroyImmediate(sphere);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }

        private static Material Ground(string name, Shader shader, Texture macro, Texture dry, Texture green, Texture soil, Color tint, float macroInfluence)
        {
            Material material = new Material(shader) { name = name, enableInstancing = true };
            material.SetTexture("_MacroAlbedo", macro);
            material.SetTexture("_MainTex", macro);
            material.SetTexture("_DryGrassTex", dry);
            material.SetTexture("_GreenGrassTex", green);
            material.SetTexture("_SoilTex", soil);
            material.SetColor("_Tint", tint);
            material.SetVector("_MacroWorldMinInvSize", new Vector4(-6000f, -6000f, 1f / 12000f, 1f / 12000f));
            material.SetFloat("_MacroInfluence", macroInfluence);
            material.SetFloat("_MicroScale", 1f / 15f);
            material.SetFloat("_DetailFadeStart", 280f);
            material.SetFloat("_DetailFadeEnd", 1800f);
            material.SetFloat("_Roughness", 0.9f);
            return material;
        }

        private static Material Standard(string name, Shader shader, Color color, float smoothness)
        {
            Material material = new Material(shader) { name = name, color = color, enableInstancing = true };
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0f);
            return material;
        }

        private sealed class MaterialSet
        {
            public Material macroGround, macroGreen, macroGold, farTerrain, asphalt, concrete, building, gravel,
                vegetation, runway, runwayMarking, water, waterBank;

            public Material ForContext(string name)
            {
                if (name.IndexOf("building", StringComparison.OrdinalIgnoreCase) >= 0) return building;
                if (name.IndexOf("asphalt", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("_road_", StringComparison.OrdinalIgnoreCase) >= 0) return asphalt;
                if (name.IndexOf("concrete", StringComparison.OrdinalIgnoreCase) >= 0) return concrete;
                if (name.IndexOf("gravel", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("quarry", StringComparison.OrdinalIgnoreCase) >= 0) return gravel;
                if (name.IndexOf("irrigated", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("forest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("meadow", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("orchard", StringComparison.OrdinalIgnoreCase) >= 0)
                    return macroGreen;
                if (name.IndexOf("harvested", StringComparison.OrdinalIgnoreCase) >= 0) return macroGold;
                return macroGround;
            }

            public void SaveAll()
            {
                foreach (Material material in new[] { macroGround, macroGreen, macroGold, farTerrain, asphalt, concrete, building,
                             gravel, vegetation, runway, runwayMarking, water, waterBank })
                {
                    AssetDatabase.CreateAsset(material, MaterialFolder + "/" + SafeName(material.name) + ".mat");
                }
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            SetStatic(child);
            return child.transform;
        }

        private static void SetStatic(GameObject target)
        {
            target.isStatic = true;
            GameObjectUtility.SetStaticEditorFlags(target, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic |
                                                          StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.NavigationStatic);
        }

        private static string SafeName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Replace('|', '_').Replace(' ', '_');
        }

        private static List<Vector2> DecodePoints(int[] points, float quantization)
        {
            List<Vector2> decoded = new List<Vector2>(points.Length / 2);
            for (int index = 0; index < points.Length; index += 2)
            {
                Vector2 point = new Vector2(points[index] * quantization, points[index + 1] * quantization);
                if (decoded.Count == 0 || Vector2.SqrMagnitude(decoded[decoded.Count - 1] - point) > 0.001f) decoded.Add(point);
            }
            if (decoded.Count > 2 && Vector2.SqrMagnitude(decoded[0] - decoded[decoded.Count - 1]) < 0.001f) decoded.RemoveAt(decoded.Count - 1);
            return decoded;
        }

        private static Color MacroColor(ContextFeature feature)
        {
            switch (feature.macro_material_id)
            {
                case "asphalt": return Hex("#343536");
                case "concrete": return Hex("#777777");
                case "irrigated_field": return Hex("#51763D");
                case "harvested_field": return Hex("#8B7B42");
                case "forest": return Hex("#345334");
                case "meadow": return Hex("#6B824F");
                case "orchard": return Hex("#45683B");
                case "quarry": return Hex("#82796C");
                case "industrial_ground": return Hex("#77736C");
                case "airfield_turf": return Hex("#657848");
                default: return Hex("#72784F");
            }
        }

        private static Color Hex(string value) => ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;

        private static float Hash01(int value)
        {
            unchecked
            {
                uint hash = (uint)value;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static uint StableHash(int x, int y)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)x) * 16777619u;
                hash = (hash ^ (uint)y) * 16777619u;
                return hash;
            }
        }

        private readonly struct IrregularParcel
        {
            public readonly uint hash;
            public readonly float edgeSeparation;

            public IrregularParcel(uint hash, float edgeSeparation)
            {
                this.hash = hash;
                this.edgeSeparation = edgeSeparation;
            }
        }

        private static IrregularParcel FindIrregularParcel(float x, float z, float nominalSize, int seed)
        {
            int cellX = Mathf.FloorToInt(x / nominalSize);
            int cellZ = Mathf.FloorToInt(z / nominalSize);
            float nearest = float.MaxValue;
            float second = float.MaxValue;
            uint nearestHash = 0;
            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                int candidateX = cellX + offsetX;
                int candidateZ = cellZ + offsetZ;
                uint hash = StableHash(candidateX ^ seed, candidateZ + seed * 17);
                float jitterX = ((hash & 0xFFFFu) / 65535f - 0.5f) * nominalSize * 0.88f;
                float jitterZ = (((hash >> 16) & 0xFFFFu) / 65535f - 0.5f) * nominalSize * 0.88f;
                float seedX = (candidateX + 0.5f) * nominalSize + jitterX;
                float seedZ = (candidateZ + 0.5f) * nominalSize + jitterZ;
                float dx = (x - seedX) * 0.86f;
                float dz = (z - seedZ) * 1.14f;
                float distance = dx * dx + dz * dz;
                if (distance < nearest)
                {
                    second = nearest;
                    nearest = distance;
                    nearestHash = hash;
                }
                else if (distance < second)
                {
                    second = distance;
                }
            }
            float separation = Mathf.Clamp01((Mathf.Sqrt(second) - Mathf.Sqrt(nearest)) / (nominalSize * 0.32f));
            return new IrregularParcel(nearestHash, separation);
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash01(ix * 73856093 ^ iy * 19349663 ^ seed);
            float b = Hash01((ix + 1) * 73856093 ^ iy * 19349663 ^ seed);
            float c = Hash01(ix * 73856093 ^ (iy + 1) * 19349663 ^ seed);
            float d = Hash01((ix + 1) * 73856093 ^ (iy + 1) * 19349663 ^ seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy) - 0.5f;
        }

        [Serializable] private sealed class TerrainDocument
        {
            public int schema_version;
            public Origin origin;
            public float height_quantization_meters;
            public TerrainLayer[] layers;
            public SourceSnapshot source_snapshot;
        }
        [Serializable] private sealed class SourceSnapshot { public string id; }
        [Serializable] private sealed class Origin { public double elevation_msl_meters; }
        [Serializable] private sealed class TerrainLayer
        {
            public string id, kind, cutout_layer, height_dm_little_endian_base64;
            public int priority, width, height;
            public float min_x_meters, max_x_meters, min_z_meters, max_z_meters, spacing_meters, inner_radius_meters;
        }
        [Serializable] private sealed class ContextDocument
        {
            public float coordinate_quantization_meters;
            public FaaContext faa;
            public OsmContext openstreetmap;
        }
        [Serializable] private sealed class FaaContext { public FaaRunway[] runways; }
        [Serializable] private sealed class FaaRunway { public string runway_id; public int width_feet; public FaaEndpoint[] endpoints; }
        [Serializable] private sealed class FaaEndpoint { public float x_east_meters, z_north_meters, usgs_elevation_msl_meters; }
        [Serializable] private sealed class OsmContext { public string attribution; public ContextFeature[] features; }
        [Serializable] private sealed class ContextFeature
        {
            public string category, geometry_type, macro_material_id;
            public float render_width_meters;
            public int[] points_q;
            public FeatureTags tags;
        }
        [Serializable] private sealed class FeatureTags { public string name; }

        private sealed class TerrainSampler
        {
            private readonly Layer[] layers;

            public TerrainSampler(TerrainDocument document)
            {
                layers = document.layers.OrderBy(source => source.priority).Select(source => new Layer(source, document.height_quantization_meters)).ToArray();
            }

            public float Sample(float x, float z)
            {
                foreach (Layer layer in layers)
                    if (layer.Contains(x, z)) return layer.Sample(x, z);
                return layers[layers.Length - 1].Sample(x, z);
            }

            public float Slope(float x, float z, float delta)
            {
                float dx = (Sample(x + delta, z) - Sample(x - delta, z)) / (delta * 2f);
                float dz = (Sample(x, z + delta) - Sample(x, z - delta)) / (delta * 2f);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }

            private sealed class Layer
            {
                private readonly TerrainLayer source;
                private readonly float[] heights;

                public Layer(TerrainLayer source, float quantization)
                {
                    this.source = source;
                    byte[] bytes = Convert.FromBase64String(source.height_dm_little_endian_base64);
                    heights = new float[source.width * source.height];
                    for (int index = 0; index < heights.Length; index++)
                    {
                        short value = unchecked((short)(bytes[index * 2] | bytes[index * 2 + 1] << 8));
                        heights[index] = value * quantization;
                    }
                }

                public bool Contains(float x, float z)
                {
                    if (x < source.min_x_meters || x > source.max_x_meters || z < source.min_z_meters || z > source.max_z_meters) return false;
                    return source.kind != "ring" || Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) >= source.inner_radius_meters - 0.01f;
                }

                public float Sample(float x, float z)
                {
                    float column = Mathf.Clamp((x - source.min_x_meters) / source.spacing_meters, 0f, source.width - 1f);
                    float row = Mathf.Clamp((z - source.min_z_meters) / source.spacing_meters, 0f, source.height - 1f);
                    int x0 = Mathf.FloorToInt(column);
                    int y0 = Mathf.FloorToInt(row);
                    int x1 = Mathf.Min(x0 + 1, source.width - 1);
                    int y1 = Mathf.Min(y0 + 1, source.height - 1);
                    float tx = column - x0;
                    float ty = row - y0;
                    float south = Mathf.Lerp(heights[y0 * source.width + x0], heights[y0 * source.width + x1], tx);
                    float north = Mathf.Lerp(heights[y1 * source.width + x0], heights[y1 * source.width + x1], tx);
                    return Mathf.Lerp(south, north, ty);
                }
            }
        }
    }
}
