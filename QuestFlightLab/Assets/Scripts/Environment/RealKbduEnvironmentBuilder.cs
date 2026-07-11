using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestFlightLab.Environment
{
    /// <summary>
    /// Builds the production-demo KBDU mesh path from small committed FAA/USGS/OSM derivatives.
    /// Source responses stay outside Git. The older procedural world remains the guarded fallback.
    /// </summary>
    public static class RealKbduEnvironmentBuilder
    {
        public const string RootName = "KBDU_RealData_World_NotForNavigation";
        public const string TerrainResourcePath = "QuestFlightLab/Environment/KBDU/kbdu_terrain_rings";
        public const string ContextResourcePath = "QuestFlightLab/Environment/KBDU/kbdu_reference_context";
        public const int MaximumVectorBatches = 180;
        public const int MaximumVerticesPerBatch = 60000;
        public const int MaximumExpectedRenderers = 200;
        public const int MaximumExpectedTriangles = 350000;
        public const float RecommendedRunwayStartInsetMeters = 70f;
        public const float RecommendedAircraftRootHeightAboveRunwayMeters = 1.25f;
        public const string ProceduralFallbackRootName = "KBDU_Inspired_Expanded_World_NotForNavigation";
        public const float FarTerrainStableMeshSpacingMeters = 200f;
        public const float StableNormalSampleDistanceMeters = 25f;

        private const float TerrainSeamEpsilon = 0.01f;

        public static bool LastBuildUsedRealData { get; private set; }
        public static string LastBuildMessage { get; private set; } = "not attempted";
        public static Vector3 LastPavedRunway08EndLocal { get; private set; }
        public static Vector3 LastPavedRunway26EndLocal { get; private set; }
        public static Vector3 LastPavedRunwayCenterLocal =>
            (LastPavedRunway08EndLocal + LastPavedRunway26EndLocal) * 0.5f;

        /// <summary>
        /// Returns an aircraft-root pose on the FAA 08/26 centerline, inset from the 08 end and
        /// following the USGS endpoint grade. The root clearance preserves the prototype's 1.25 m
        /// ground-height convention; callers still own their ground-contact model.
        /// </summary>
        public static bool TryGetRecommendedPavedRunwayStart(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (!LastBuildUsedRealData) return false;

            Vector3 endpointDelta = LastPavedRunway26EndLocal - LastPavedRunway08EndLocal;
            Vector3 horizontal = new Vector3(endpointDelta.x, 0f, endpointDelta.z);
            float horizontalLength = horizontal.magnitude;
            if (horizontalLength < 1f) return false;

            float inset = Mathf.Clamp(RecommendedRunwayStartInsetMeters, 0f, horizontalLength);
            float interpolation = inset / horizontalLength;
            position = Vector3.Lerp(LastPavedRunway08EndLocal, LastPavedRunway26EndLocal, interpolation) +
                       Vector3.up * RecommendedAircraftRootHeightAboveRunwayMeters;
            rotation = Quaternion.LookRotation(horizontal / horizontalLength, Vector3.up);
            return true;
        }

        public static bool TryBuild(Transform airportRoot, out GameObject world, out string error)
        {
            world = null;
            error = string.Empty;
            LastBuildUsedRealData = false;

            if (airportRoot == null)
            {
                error = "airport root is null";
                LastBuildMessage = error;
                return false;
            }

            if (string.Equals(System.Environment.GetEnvironmentVariable("QFL_FORCE_PROCEDURAL_KBDU"), "1", StringComparison.Ordinal))
            {
                error = "QFL_FORCE_PROCEDURAL_KBDU=1";
                LastBuildMessage = error;
                return false;
            }

            Transform existing = airportRoot.Find(RootName);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                int disabledConflicts = HideLegacyFlatPlaceholders(airportRoot, existing);
                EnsureMountainStabilityProbe(existing, airportRoot);
                world = existing.gameObject;
                LastBuildUsedRealData = true;
                LastBuildMessage = $"reused existing real-data world; disabled {disabledConflicts} conflicting renderers";
                return true;
            }

            TextAsset terrainText = Resources.Load<TextAsset>(TerrainResourcePath);
            TextAsset contextText = Resources.Load<TextAsset>(ContextResourcePath);
            if (terrainText == null || contextText == null)
            {
                error = $"real KBDU resources missing terrain={terrainText != null} context={contextText != null}";
                LastBuildMessage = error;
                return false;
            }

            GameObject root = null;
            try
            {
                RealKbduTerrainDocument terrain = JsonUtility.FromJson<RealKbduTerrainDocument>(terrainText.text);
                RealKbduContextDocument context = JsonUtility.FromJson<RealKbduContextDocument>(contextText.text);
                ValidateDocuments(terrain, context);

                root = new GameObject(RootName);
                root.transform.SetParent(airportRoot, false);
                MaterialLibrary materials = new MaterialLibrary();
                TerrainSampler sampler = BuildTerrain(root.transform, terrain, materials, out int terrainTriangles);
                BuildFaaRunway(
                    root.transform,
                    terrain,
                    context,
                    sampler,
                    materials,
                    out int runwayTriangles,
                    out string runwaySummary);
                BuildVectorBatches(
                    root.transform,
                    context,
                    sampler,
                    materials,
                    out int renderedFeatures,
                    out int skippedFeatures,
                    out int vectorTriangles,
                    out int batchCount);

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (!QuestEnvironmentMaterialFactory.IsGroundMaterial(renderer.sharedMaterial)) continue;
                    bool terrainContinuity = renderer.name.StartsWith("RealTerrain_", StringComparison.Ordinal);
                    QuestEnvironmentMaterialFactory.ApplyDeterministicBatchVariation(
                        renderer,
                        renderer.name,
                        preserveGlobalContinuity: terrainContinuity);
                }
                MeshFilter[] meshes = root.GetComponentsInChildren<MeshFilter>(true);
                int actualTriangles = CountTriangles(meshes);
                if (actualTriangles != terrainTriangles + runwayTriangles + vectorTriangles)
                {
                    throw new InvalidOperationException(
                        $"Runtime triangle accounting mismatch actual={actualTriangles} expected={terrainTriangles + runwayTriangles + vectorTriangles}");
                }
                if (renderers.Length > MaximumExpectedRenderers)
                {
                    throw new InvalidOperationException($"Real KBDU renderer budget exceeded: {renderers.Length}/{MaximumExpectedRenderers}");
                }
                if (actualTriangles > MaximumExpectedTriangles)
                {
                    throw new InvalidOperationException($"Real KBDU triangle budget exceeded: {actualTriangles}/{MaximumExpectedTriangles}");
                }
                int hiddenLegacyRenderers = HideLegacyFlatPlaceholders(airportRoot, root.transform);

                RealKbduEnvironmentStatus status = root.AddComponent<RealKbduEnvironmentStatus>();
                status.dataValidated = true;
                status.sourceSnapshotId = terrain.source_snapshot.id;
                status.profileName = "real_kbdu_usgs_faa_osm_v1";
                status.coordinateFrame = "local ENU: Unity +X=east, +Y=up, +Z=north";
                status.originLatitudeDegrees = terrain.origin.latitude_degrees;
                status.originLongitudeDegrees = terrain.origin.longitude_degrees;
                status.originElevationMslMeters = terrain.origin.elevation_msl_meters;
                status.terrainLayerCount = terrain.layers.Length;
                status.terrainHeightSamples = terrain.budgets.height_samples;
                status.sourceVectorFeatures = context.openstreetmap.evidence.output_feature_count;
                status.renderedVectorFeatures = renderedFeatures;
                status.skippedVectorFeatures = skippedFeatures;
                status.runtimeBatchCount = batchCount;
                status.rendererCount = renderers.Length;
                status.meshCount = meshes.Length;
                status.triangleCount = actualTriangles;
                status.materialCount = materials.MaterialCount;
                status.textureCount = materials.TextureCount;
                status.distanceCullerCount = root.GetComponentsInChildren<RealKbduBatchDistanceCuller>(true).Length;
                status.lodGroupCount = root.GetComponentsInChildren<LODGroup>(true).Length;
                status.faaRunwaySummary = runwaySummary;
                status.osmAttribution = context.openstreetmap.attribution;
                status.imageryStatus = context.macro_material_fallback?.imagery_gate != null
                    ? context.macro_material_fallback.imagery_gate.status
                    : "unknown";
                status.notes =
                    $"Four USGS resolution layers plus combined OSM spatial/material/category batches. " +
                    $"Disabled {hiddenLegacyRenderers} legacy flat/placeholder renderers. No NAIP pixels committed. " +
                    root.GetComponent<RealKbduWaterStatus>()?.Summary + ". " +
                    "Three attributed CC0 ground maps use one world-space anti-tile shader. " +
                    "Existing airport colliders remain the prototype ground-physics fallback; not for navigation.";

                WorldPerformanceBudget budget = root.AddComponent<WorldPerformanceBudget>();
                budget.profileName = status.profileName;
                budget.worldSizeMeters = new Vector2(24000f, 24000f);
                budget.terrainChunkCount = terrain.layers.Length;
                budget.lodGroupCount = status.lodGroupCount;
                budget.rendererCount = status.rendererCount;
                budget.meshCount = status.meshCount;
                budget.approxTriangleCount = status.triangleCount;
                budget.materialCount = status.materialCount;
                budget.textureCount = status.textureCount;
                budget.nearDetailRadiusMeters = 2000f;
                budget.midDetailRadiusMeters = 6000f;
                budget.farDrawRadiusMeters = 12000f;
                budget.notes = status.Summary + "; " + status.faaRunwaySummary + "; " + status.osmAttribution;

                EnsureMountainStabilityProbe(root.transform, airportRoot);

                world = root;
                LastBuildUsedRealData = true;
                LastBuildMessage = status.Summary;
                RealKbduWaterStatus waterStatus = root.GetComponent<RealKbduWaterStatus>();
                Debug.Log($"[QuestFlightLab][RealKBDU] {status.Summary} runway={runwaySummary} water={waterStatus?.Summary}");
                return true;
            }
            catch (Exception exception)
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                error = exception.Message;
                LastBuildMessage = "real-data build rejected: " + exception.Message;
                Debug.LogWarning($"[QuestFlightLab][RealKBDU] {LastBuildMessage}; preserving procedural fallback.");
                return false;
            }
        }

        private static void ValidateDocuments(RealKbduTerrainDocument terrain, RealKbduContextDocument context)
        {
            if (terrain == null || context == null) throw new InvalidOperationException("KBDU JSON deserialization failed");
            if (terrain.schema_version != 1 || context.schema_version != 1) throw new InvalidOperationException("KBDU schema must be v1");
            if (terrain.origin == null || context.origin == null) throw new InvalidOperationException("KBDU origin missing");
            if (Math.Abs(terrain.origin.latitude_degrees - 40.03936527d) > 0.00000002d ||
                Math.Abs(terrain.origin.longitude_degrees - -105.22608958d) > 0.00000002d ||
                Math.Abs(terrain.origin.elevation_msl_meters - 1611.7824d) > 0.001d)
            {
                throw new InvalidOperationException("KBDU terrain origin does not match the pinned FAA ARP datum");
            }
            if (Math.Abs(terrain.origin.latitude_degrees - context.origin.latitude_degrees) > 1e-9d ||
                Math.Abs(terrain.origin.longitude_degrees - context.origin.longitude_degrees) > 1e-9d ||
                Math.Abs(terrain.origin.elevation_msl_meters - context.origin.elevation_msl_meters) > 0.001d)
            {
                throw new InvalidOperationException("KBDU terrain/context origins disagree");
            }
            if (terrain.layers == null || terrain.layers.Length != 4) throw new InvalidOperationException("KBDU requires four terrain layers");
            if (terrain.budgets == null || terrain.budgets.height_samples != 30304 ||
                terrain.budgets.expected_terrain_triangles > 65000)
            {
                throw new InvalidOperationException("KBDU terrain static budget metadata is invalid");
            }
            if (terrain.source_snapshot?.usgs_evidence == null ||
                !terrain.source_snapshot.usgs_evidence.location_mapping_verified ||
                terrain.source_snapshot.usgs_evidence.sample_count < 29000)
            {
                throw new InvalidOperationException("USGS ArcGIS locationId/coordinate mapping evidence is missing");
            }
            if (context.faa?.facility == null || context.faa.facility.ARPT_ID != "BDU" || context.faa.facility.ICAO_ID != "KBDU")
            {
                throw new InvalidOperationException("FAA KBDU facility metadata is missing");
            }
            if (context.openstreetmap?.features == null || context.openstreetmap.evidence == null ||
                context.openstreetmap.features.Length != context.openstreetmap.evidence.output_feature_count ||
                context.openstreetmap.features.Length > 6000 || context.openstreetmap.license != "ODbL-1.0" ||
                context.openstreetmap.attribution != "© OpenStreetMap contributors")
            {
                throw new InvalidOperationException("OSM context count/license/attribution validation failed");
            }
            int pointCount = 0;
            foreach (RealKbduContextFeature feature in context.openstreetmap.features)
            {
                if (feature?.points_q == null || feature.points_q.Length % 2 != 0)
                    throw new InvalidOperationException("OSM flat x/z coordinate payload is invalid");
                pointCount += feature.points_q.Length / 2;
            }
            if (pointCount != context.openstreetmap.evidence.output_point_count || pointCount > 70000)
                throw new InvalidOperationException("OSM point budget/count validation failed");
            RealKbduFaaRunway paved = context.faa.runways?.FirstOrDefault(runway => runway.runway_id == "08/26");
            if (paved?.endpoints == null || paved.endpoints.Length != 2 || paved.length_feet != 4100 || paved.width_feet != 75)
                throw new InvalidOperationException("FAA paved runway 08/26 metadata is invalid");
            float localLength = Vector2.Distance(
                new Vector2(paved.endpoints[0].x_east_meters, paved.endpoints[0].z_north_meters),
                new Vector2(paved.endpoints[1].x_east_meters, paved.endpoints[1].z_north_meters));
            float gradeDelta = paved.endpoints[1].usgs_elevation_msl_meters - paved.endpoints[0].usgs_elevation_msl_meters;
            if (Mathf.Abs(localLength - 1249.68f) > 3f || Mathf.Abs(gradeDelta - -2.919f) > 0.05f)
                throw new InvalidOperationException($"FAA/USGS runway geometry mismatch length={localLength:0.00}m delta={gradeDelta:0.000}m");
        }

        private static TerrainSampler BuildTerrain(
            Transform parent,
            RealKbduTerrainDocument document,
            MaterialLibrary materials,
            out int triangleCount)
        {
            Dictionary<string, RuntimeTerrainLayer> layers = new Dictionary<string, RuntimeTerrainLayer>(StringComparer.Ordinal);
            foreach (RealKbduTerrainLayer source in document.layers)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.id)) throw new InvalidOperationException("Terrain layer id missing");
                int expectedSamples = checked(source.width * source.height);
                if (expectedSamples != source.sample_count) throw new InvalidOperationException($"Terrain sample count mismatch in {source.id}");
                byte[] payload = Convert.FromBase64String(source.height_dm_little_endian_base64);
                if (payload.Length != expectedSamples * 2) throw new InvalidOperationException($"Terrain byte count mismatch in {source.id}");
                float[] heights = new float[expectedSamples];
                for (int index = 0; index < expectedSamples; index++)
                {
                    int low = payload[index * 2];
                    int high = payload[index * 2 + 1];
                    short value = unchecked((short)(low | (high << 8)));
                    heights[index] = value * document.height_quantization_meters;
                }
                layers.Add(source.id, new RuntimeTerrainLayer(source, heights));
            }

            TerrainSampler sampler = new TerrainSampler(layers);
            triangleCount = 0;
            foreach (RuntimeTerrainLayer layer in layers.Values.OrderBy(item => item.Source.priority))
            {
                Mesh mesh = BuildTerrainMesh(layer, layers);
                ApplyStableTerrainNormals(mesh, sampler);
                triangleCount += mesh.triangles.Length / 3;
                GameObject tile = new GameObject("RealTerrain_" + layer.Source.id);
                tile.transform.SetParent(parent, false);
                MeshFilter filter = tile.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = tile.AddComponent<MeshRenderer>();
                string materialId = layer.Source.id == "far_24km"
                    ? "terrain_far"
                    : layer.Source.id == "mid_12km" ? "terrain_mid" : "dry_prairie";
                renderer.sharedMaterial = materials.Get(materialId);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = layer.Source.priority <= 1;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.allowOcclusionWhenDynamic = false;
                tile.isStatic = true;
            }
            return sampler;
        }

        private static Mesh BuildTerrainMesh(
            RuntimeTerrainLayer layer,
            Dictionary<string, RuntimeTerrainLayer> layers)
        {
            RealKbduTerrainLayer source = layer.Source;

            // Each USGS ring is sampled independently. Building the coarse ring straight from its
            // source grid either overlaps the finer ring (the 2 km join) or leaves two unrelated
            // height polylines on the same boundary (the 6 km join). Both cases are view-unstable
            // in stereo at grazing angles. Replace the first coarse cell band with a deterministic
            // stitch that contains every vertex from the finer boundary and then fans out to the
            // coarse grid. No terrain LOD swap or runtime deformation is involved.
            if (source.kind == "ring" && source.inner_radius_meters > TerrainSeamEpsilon &&
                TryFindInnerSeamLayer(source, layers, out RuntimeTerrainLayer innerLayer))
            {
                return BuildStitchedSquareRingMesh(layer, innerLayer);
            }

            Vector3[] vertices = new Vector3[source.sample_count];
            Vector2[] uvs = new Vector2[source.sample_count];
            for (int row = 0; row < source.height; row++)
            {
                float z = source.min_z_meters + row * source.spacing_meters;
                for (int column = 0; column < source.width; column++)
                {
                    float x = source.min_x_meters + column * source.spacing_meters;
                    int index = row * source.width + column;
                    vertices[index] = new Vector3(x, layer.Heights[index], z);
                    uvs[index] = new Vector2(x / 192f, z / 192f);
                }
            }

            List<int> triangles = new List<int>(source.expected_triangle_count * 3);
            layers.TryGetValue(source.cutout_layer ?? string.Empty, out RuntimeTerrainLayer cutout);
            for (int row = 0; row < source.height - 1; row++)
            {
                float centerZ = source.min_z_meters + (row + 0.5f) * source.spacing_meters;
                for (int column = 0; column < source.width - 1; column++)
                {
                    float centerX = source.min_x_meters + (column + 0.5f) * source.spacing_meters;
                    if (source.kind == "ring" && Mathf.Max(Mathf.Abs(centerX), Mathf.Abs(centerZ)) < source.inner_radius_meters)
                        continue;
                    if (cutout != null && cutout.Contains(centerX, centerZ)) continue;
                    int index = row * source.width + column;
                    triangles.Add(index);
                    triangles.Add(index + source.width);
                    triangles.Add(index + 1);
                    triangles.Add(index + 1);
                    triangles.Add(index + source.width);
                    triangles.Add(index + source.width + 1);
                }
            }
            if (triangles.Count / 3 != source.expected_triangle_count)
                throw new InvalidOperationException($"Terrain triangle count mismatch in {source.id}: {triangles.Count / 3}/{source.expected_triangle_count}");

            Mesh mesh = new Mesh { name = source.id + "_USGS_Mesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool TryFindInnerSeamLayer(
            RealKbduTerrainLayer source,
            Dictionary<string, RuntimeTerrainLayer> layers,
            out RuntimeTerrainLayer innerLayer)
        {
            float radius = source.inner_radius_meters;
            innerLayer = layers.Values
                .Where(candidate => candidate.Source.priority < source.priority)
                .Where(candidate => Mathf.Abs(candidate.Source.min_x_meters + radius) <= TerrainSeamEpsilon)
                .Where(candidate => Mathf.Abs(candidate.Source.max_x_meters - radius) <= TerrainSeamEpsilon)
                .Where(candidate => Mathf.Abs(candidate.Source.min_z_meters + radius) <= TerrainSeamEpsilon)
                .Where(candidate => Mathf.Abs(candidate.Source.max_z_meters - radius) <= TerrainSeamEpsilon)
                .OrderByDescending(candidate => candidate.Source.priority)
                .FirstOrDefault();
            return innerLayer != null;
        }

        private static Mesh BuildStitchedSquareRingMesh(RuntimeTerrainLayer outer, RuntimeTerrainLayer inner)
        {
            RealKbduTerrainLayer source = outer.Source;
            float radius = source.inner_radius_meters;
            float stableSpacing = StableMeshSpacing(source);
            int refinement = Mathf.Max(1, Mathf.RoundToInt(source.spacing_meters / stableSpacing));
            int maximumTriangles = source.expected_triangle_count * refinement * refinement + 2048;
            float negativeOuterLine = PreviousGridCoordinate(source, -radius, stableSpacing);
            float positiveOuterLine = NextGridCoordinate(source, radius, stableSpacing);
            if (negativeOuterLine >= -radius - TerrainSeamEpsilon ||
                positiveOuterLine <= radius + TerrainSeamEpsilon)
            {
                throw new InvalidOperationException($"Terrain seam grid is invalid for {source.id}");
            }

            TerrainMeshAssembler mesh = new TerrainMeshAssembler(
                source.id + "_Stable" + Mathf.RoundToInt(stableSpacing) + "m",
                maximumTriangles);

            // Keep all complete coarse cells beyond the transition band. The transition band is
            // generated below, so no coplanar surface remains underneath the finer ring.
            int rowCount = Mathf.RoundToInt((source.max_z_meters - source.min_z_meters) / stableSpacing);
            int columnCount = Mathf.RoundToInt((source.max_x_meters - source.min_x_meters) / stableSpacing);
            for (int row = 0; row < rowCount; row++)
            {
                float z0 = source.min_z_meters + row * stableSpacing;
                float z1 = row == rowCount - 1 ? source.max_z_meters : z0 + stableSpacing;
                for (int column = 0; column < columnCount; column++)
                {
                    float x0 = source.min_x_meters + column * stableSpacing;
                    float x1 = column == columnCount - 1 ? source.max_x_meters : x0 + stableSpacing;
                    bool beyondTransition = x1 <= negativeOuterLine + TerrainSeamEpsilon ||
                                            x0 >= positiveOuterLine - TerrainSeamEpsilon ||
                                            z1 <= negativeOuterLine + TerrainSeamEpsilon ||
                                            z0 >= positiveOuterLine - TerrainSeamEpsilon;
                    if (beyondTransition)
                    {
                        mesh.AddQuad(x0, z0, x1, z1, outer.Sample);
                    }
                }
            }

            List<float> innerCoordinates = GridCoordinates(inner.Source, -radius, radius);
            List<float> outerCoordinates = GridCoordinates(source, -radius, radius, stableSpacing);

            // West/east transition strips, ordered south to north.
            mesh.AddVerticalTransition(
                negativeOuterLine,
                outerCoordinates,
                outer.Sample,
                -radius,
                innerCoordinates,
                inner.Sample);
            mesh.AddVerticalTransition(
                radius,
                innerCoordinates,
                inner.Sample,
                positiveOuterLine,
                outerCoordinates,
                outer.Sample);

            // South/north transition strips, ordered west to east.
            mesh.AddHorizontalTransition(
                negativeOuterLine,
                outerCoordinates,
                outer.Sample,
                -radius,
                innerCoordinates,
                inner.Sample);
            mesh.AddHorizontalTransition(
                radius,
                innerCoordinates,
                inner.Sample,
                positiveOuterLine,
                outerCoordinates,
                outer.Sample);

            // Four small corner quads complete the square band. Boundary corner heights come from
            // the finer ring; all other corner vertices remain samples of the coarse source.
            mesh.AddCornerQuad(negativeOuterLine, negativeOuterLine, -radius, -radius, outer.Sample, inner.Sample);
            mesh.AddCornerQuad(radius, negativeOuterLine, positiveOuterLine, -radius, outer.Sample, inner.Sample);
            mesh.AddCornerQuad(negativeOuterLine, radius, -radius, positiveOuterLine, outer.Sample, inner.Sample);
            mesh.AddCornerQuad(radius, radius, positiveOuterLine, positiveOuterLine, outer.Sample, inner.Sample);

            Mesh result = mesh.ToMesh();
            int triangleCount = result.triangles.Length / 3;
            if (triangleCount <= 0 || triangleCount > maximumTriangles)
            {
                throw new InvalidOperationException(
                    $"Terrain seam triangle budget invalid in {source.id}: {triangleCount}/{maximumTriangles}");
            }
            return result;
        }

        private static float StableMeshSpacing(RealKbduTerrainLayer source)
        {
            return string.Equals(source.id, "far_24km", StringComparison.Ordinal)
                ? Mathf.Min(source.spacing_meters, FarTerrainStableMeshSpacingMeters)
                : source.spacing_meters;
        }

        private static float PreviousGridCoordinate(RealKbduTerrainLayer source, float value, float spacing)
        {
            int count = Mathf.RoundToInt((source.max_x_meters - source.min_x_meters) / spacing);
            int index = Mathf.CeilToInt((value - source.min_x_meters) / spacing) - 1;
            index = Mathf.Clamp(index, 0, count);
            return source.min_x_meters + index * spacing;
        }

        private static float NextGridCoordinate(RealKbduTerrainLayer source, float value, float spacing)
        {
            int count = Mathf.RoundToInt((source.max_x_meters - source.min_x_meters) / spacing);
            int index = Mathf.FloorToInt((value - source.min_x_meters) / spacing) + 1;
            index = Mathf.Clamp(index, 0, count);
            return source.min_x_meters + index * spacing;
        }

        private static List<float> GridCoordinates(
            RealKbduTerrainLayer source,
            float minimum,
            float maximum,
            float spacing = -1f)
        {
            if (spacing <= TerrainSeamEpsilon) spacing = source.spacing_meters;
            List<float> coordinates = new List<float> { minimum };
            int count = Mathf.RoundToInt((source.max_x_meters - source.min_x_meters) / spacing);
            for (int index = 0; index <= count; index++)
            {
                float coordinate = source.min_x_meters + index * spacing;
                if (coordinate > minimum + TerrainSeamEpsilon && coordinate < maximum - TerrainSeamEpsilon)
                {
                    coordinates.Add(coordinate);
                }
            }
            coordinates.Add(maximum);
            return coordinates;
        }

        private static void ApplyStableTerrainNormals(Mesh mesh, TerrainSampler sampler)
        {
            if (mesh == null || sampler == null) return;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = new Vector3[vertices.Length];
            float delta = StableNormalSampleDistanceMeters;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 vertex = vertices[index];
                float west = sampler.Sample(vertex.x - delta, vertex.z);
                float east = sampler.Sample(vertex.x + delta, vertex.z);
                float south = sampler.Sample(vertex.x, vertex.z - delta);
                float north = sampler.Sample(vertex.x, vertex.z + delta);
                normals[index] = new Vector3(west - east, delta * 2f, south - north).normalized;
            }
            mesh.normals = normals;
            mesh.RecalculateBounds();
        }

        private static void BuildFaaRunway(
            Transform parent,
            RealKbduTerrainDocument terrain,
            RealKbduContextDocument context,
            TerrainSampler sampler,
            MaterialLibrary materials,
            out int triangleCount,
            out string summary)
        {
            RealKbduFaaRunway runway = context.faa.runways.First(item => item.runway_id == "08/26");
            RealKbduFaaRunwayEndpoint first = runway.endpoints[0];
            RealKbduFaaRunwayEndpoint second = runway.endpoints[1];
            float datum = (float)terrain.origin.elevation_msl_meters;
            Vector3 end08 = new Vector3(first.x_east_meters, first.usgs_elevation_msl_meters - datum + 0.065f, first.z_north_meters);
            Vector3 end26 = new Vector3(second.x_east_meters, second.usgs_elevation_msl_meters - datum + 0.065f, second.z_north_meters);
            LastPavedRunway08EndLocal = end08;
            LastPavedRunway26EndLocal = end26;
            Vector3 horizontal = end26 - end08;
            horizontal.y = 0f;
            float length = horizontal.magnitude;
            Vector3 forward = horizontal.normalized;
            Vector3 left = new Vector3(-forward.z, 0f, forward.x);
            float halfWidth = runway.width_feet * 0.3048f * 0.5f;

            Vector3[] pavementVertices =
            {
                end08 + left * halfWidth,
                end26 + left * halfWidth,
                end08 - left * halfWidth,
                end26 - left * halfWidth
            };
            Mesh pavement = new Mesh { name = "FAA_KBDU_Runway_08_26_Sloped_Mesh" };
            pavement.vertices = pavementVertices;
            pavement.uv = new[] { Vector2.zero, new Vector2(0f, length / 32f), Vector2.right, new Vector2(1f, length / 32f) };
            pavement.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            pavement.RecalculateNormals();
            pavement.RecalculateBounds();
            CreateMeshObject(parent, "FAA_KBDU_Runway_08_26_Sloped", pavement, materials.Get("asphalt"), 9000f, 0.00015f);

            MeshAccumulator markings = new MeshAccumulator("FAA_Runway_Markings", 0, "runway_marking", "runway_marking", 9000f, 0.0002f);
            for (int dash = 0; dash < 12; dash++)
            {
                float t = 0.09f + dash * 0.074f;
                AddRunwayRectangle(markings, end08, end26, forward, left, t, 26f, 0.85f, 0.025f);
            }
            AddRunwayRectangle(markings, end08, end26, forward, left, 0.055f, 9f, halfWidth * 1.55f, 0.026f);
            AddRunwayRectangle(markings, end08, end26, forward, left, 0.945f, 9f, halfWidth * 1.55f, 0.026f);
            Mesh markingMesh = markings.ToMesh();
            CreateMeshObject(parent, "FAA_KBDU_Runway_08_26_Markings", markingMesh, materials.Get("runway_marking"), 9000f, 0.0002f);

            triangleCount = pavement.triangles.Length / 3 + markingMesh.triangles.Length / 3;
            float elevationDelta = second.usgs_elevation_msl_meters - first.usgs_elevation_msl_meters;
            summary = $"FAA 08/26 length={length:0.00}m width={runway.width_feet * 0.3048f:0.00}m " +
                      $"USGS endpoints={first.usgs_elevation_msl_meters:0.000}/{second.usgs_elevation_msl_meters:0.000}m " +
                      $"gradeDelta={elevationDelta:0.000}m center=({LastPavedRunwayCenterLocal.x:0.0},{LastPavedRunwayCenterLocal.z:0.0})";
        }

        private static void AddRunwayRectangle(
            MeshAccumulator target,
            Vector3 first,
            Vector3 second,
            Vector3 forward,
            Vector3 left,
            float t,
            float length,
            float width,
            float yOffset)
        {
            Vector3 center = Vector3.Lerp(first, second, t) + Vector3.up * yOffset;
            float halfLength = length * 0.5f;
            float halfWidth = width * 0.5f;
            int start = target.Vertices.Count;
            target.Vertices.Add(center - forward * halfLength + left * halfWidth);
            target.Vertices.Add(center + forward * halfLength + left * halfWidth);
            target.Vertices.Add(center - forward * halfLength - left * halfWidth);
            target.Vertices.Add(center + forward * halfLength - left * halfWidth);
            target.Uvs.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one });
            target.Triangles.AddRange(new[] { start, start + 1, start + 2, start + 2, start + 1, start + 3 });
        }

        private static void BuildVectorBatches(
            Transform parent,
            RealKbduContextDocument context,
            TerrainSampler sampler,
            MaterialLibrary materials,
            out int renderedFeatures,
            out int skippedFeatures,
            out int triangleCount,
            out int batchCount)
        {
            Dictionary<string, List<MeshAccumulator>> batches = new Dictionary<string, List<MeshAccumulator>>(StringComparer.Ordinal);
            renderedFeatures = 0;
            skippedFeatures = 0;
            float quantization = context.coordinate_quantization_meters;
            WaterwayMeshBuilder.BuildStatistics aggregateWater = new WaterwayMeshBuilder.BuildStatistics();
            int sourceWaterLines = 0;
            int sourceWaterPolygons = 0;
            int renderedWaterLines = 0;
            int renderedWaterPolygons = 0;
            int rejectedWater = 0;
            int bankedWater = 0;

            foreach (RealKbduContextFeature feature in context.openstreetmap.features)
            {
                if (!TryDecodePoints(feature, quantization, out List<Vector2> points))
                {
                    skippedFeatures++;
                    continue;
                }
                Vector2 centroid = Centroid(points);
                float radius = Mathf.Max(Mathf.Abs(centroid.x), Mathf.Abs(centroid.y));
                if (feature.category == "building" && radius > 4200f)
                {
                    skippedFeatures++;
                    continue;
                }
                if (feature.category == "aeroway" && string.Equals(feature.tags?.aeroway, "runway", StringComparison.OrdinalIgnoreCase))
                {
                    // The authoritative FAA runway mesh above owns 08/26 and avoids OSM/legacy z-fighting.
                    skippedFeatures++;
                    continue;
                }

                int ring = radius <= 2200f ? 0 : radius <= 6500f ? 1 : 2;
                float tileSize = ring == 0 ? 1100f : ring == 1 ? 3000f : 6000f;
                int tileX = Mathf.FloorToInt(centroid.x / tileSize);
                int tileZ = Mathf.FloorToInt(centroid.y / tileSize);
                string materialId = string.IsNullOrWhiteSpace(feature.macro_material_id) ? "dry_prairie" : feature.macro_material_id;
                int estimate = EstimateVertices(feature, points.Count);
                string baseKey = $"{ring}|{tileX}|{tileZ}|{feature.category}|{materialId}";
                MeshAccumulator batch = GetAccumulator(batches, baseKey, ring, feature.category, materialId, estimate);
                if (batch == null)
                {
                    skippedFeatures++;
                    continue;
                }

                bool added = false;
                if (feature.category == "water")
                {
                    WaterwayMeshBuilder.MeshBuffers waterBuffers = new WaterwayMeshBuilder.MeshBuffers(
                        batch.Vertices,
                        batch.Uvs,
                        batch.Triangles);
                    if (feature.geometry_type == "polyline")
                    {
                        sourceWaterLines++;
                        MeshAccumulator bankBatch = null;
                        // Banks are most useful in the near airport ring. Keeping distant drainage
                        // to one opaque draw avoids spending the Quest budget on invisible edging.
                        if (ring == 0)
                        {
                            string bankKey = $"{ring}|{tileX}|{tileZ}|water_bank|water_bank";
                            bankBatch = GetAccumulator(batches, bankKey, ring, "water_bank", "water_bank", points.Count * 4);
                        }
                        WaterwayMeshBuilder.MeshBuffers bankBuffers = bankBatch != null
                            ? new WaterwayMeshBuilder.MeshBuffers(bankBatch.Vertices, bankBatch.Uvs, bankBatch.Triangles)
                            : null;
                        added = WaterwayMeshBuilder.TryAppendLinearWaterway(
                            points,
                            Mathf.Max(WaterwayMeshBuilder.MinimumWaterwayWidthMeters, feature.render_width_meters),
                            sampler.Sample,
                            waterBuffers,
                            bankBuffers,
                            out WaterwayMeshBuilder.BuildStatistics waterStatistics);
                        if (added)
                        {
                            renderedWaterLines++;
                            if (bankBuffers != null)
                            {
                                bankBatch.FeatureCount++;
                                bankedWater++;
                            }
                            aggregateWater.Accumulate(waterStatistics);
                        }
                    }
                    else if (feature.geometry_type == "polygon")
                    {
                        sourceWaterPolygons++;
                        added = WaterwayMeshBuilder.TryAppendReservoir(
                            points,
                            sampler.Sample,
                            waterBuffers,
                            out WaterwayMeshBuilder.BuildStatistics waterStatistics);
                        if (added)
                        {
                            renderedWaterPolygons++;
                            aggregateWater.Accumulate(waterStatistics);
                        }
                    }
                    if (!added) rejectedWater++;
                }
                else if (feature.category == "building" && feature.geometry_type == "polygon")
                {
                    added = AddBuilding(batch, points, Mathf.Max(3.5f, feature.render_height_meters), sampler);
                }
                else if (feature.category == "barrier")
                {
                    added = AddWallPolyline(batch, points, Mathf.Max(0.25f, feature.render_width_meters), Mathf.Max(1f, feature.render_height_meters), sampler);
                }
                else if (feature.geometry_type == "polygon")
                {
                    added = AddGroundPolygon(batch, points, GroundOffset(feature.category), sampler);
                }
                else if (feature.geometry_type == "polyline")
                {
                    float width = Mathf.Max(0.6f, feature.render_width_meters);
                    added = AddRibbon(batch, points, width, GroundOffset(feature.category), sampler);
                }

                if (added)
                {
                    batch.FeatureCount++;
                    renderedFeatures++;
                }
                else
                {
                    skippedFeatures++;
                }
            }

            triangleCount = 0;
            batchCount = 0;
            foreach (MeshAccumulator batch in batches.Values.SelectMany(value => value).OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                if (batch.Triangles.Count < 3) continue;
                Mesh mesh = batch.ToMesh();
                triangleCount += mesh.triangles.Length / 3;
                CreateMeshObject(
                    parent,
                    $"RealContext_{batch.Ring}_{batch.Category}_{batch.MaterialId}_{batchCount:000}",
                    mesh,
                    materials.Get(batch.MaterialId),
                    batch.MaximumDistanceMeters,
                    batch.CullScreenHeight);
                batchCount++;
            }

            Material waterMaterial = materials.Get("water");
            RealKbduWaterStatus waterStatus = parent.gameObject.AddComponent<RealKbduWaterStatus>();
            waterStatus.sourceLinearFeatures = sourceWaterLines;
            waterStatus.sourcePolygonFeatures = sourceWaterPolygons;
            waterStatus.renderedLinearFeatures = renderedWaterLines;
            waterStatus.renderedPolygonFeatures = renderedWaterPolygons;
            waterStatus.rejectedFeatures = rejectedWater;
            waterStatus.bankedNearFeatures = bankedWater;
            waterStatus.outputCenterlinePoints = aggregateWater.outputPointCount;
            waterStatus.waterTriangleCount = aggregateWater.waterTriangleCount;
            waterStatus.bankTriangleCount = aggregateWater.bankTriangleCount;
            waterStatus.maximumTurnDegrees = aggregateWater.maximumTurnDegrees;
            waterStatus.minimumTerrainSeparationMeters = aggregateWater.minimumTerrainSeparationMeters;
            waterStatus.opaqueZWriteMaterial = QuestEnvironmentMaterialFactory.IsStableWaterMaterial(waterMaterial) &&
                                               waterMaterial.GetFloat("_ZWrite") > 0.5f &&
                                               waterMaterial.renderQueue < (int)RenderQueue.Transparent;
            waterStatus.animatedUv = false;
            waterStatus.waterUsesLodOrDistanceCulling = parent.GetComponentsInChildren<RealKbduBatchDistanceCuller>(true)
                .Any(culler => culler.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0) ||
                parent.GetComponentsInChildren<LODGroup>(true)
                    .Any(lod => lod.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static MeshAccumulator GetAccumulator(
            Dictionary<string, List<MeshAccumulator>> batches,
            string baseKey,
            int ring,
            string category,
            string materialId,
            int estimatedVertices)
        {
            if (!batches.TryGetValue(baseKey, out List<MeshAccumulator> parts))
            {
                if (batches.Values.Sum(list => list.Count) >= MaximumVectorBatches) return null;
                parts = new List<MeshAccumulator>();
                batches.Add(baseKey, parts);
            }
            MeshAccumulator current = parts.Count > 0 ? parts[parts.Count - 1] : null;
            if (current == null || current.Vertices.Count + estimatedVertices > MaximumVerticesPerBatch)
            {
                if (batches.Values.Sum(list => list.Count) >= MaximumVectorBatches) return null;
                float maxDistance = ring == 0 ? (category == "building" ? 5200f : 7200f) : ring == 1 ? 12000f : 18000f;
                float cullHeight = category == "building" ? 0.0015f : ring == 0 ? 0.00025f : ring == 1 ? 0.00045f : 0.0008f;
                current = new MeshAccumulator(baseKey + "|" + parts.Count, ring, category, materialId, maxDistance, cullHeight);
                parts.Add(current);
            }
            return current;
        }

        private static bool TryDecodePoints(RealKbduContextFeature feature, float quantization, out List<Vector2> points)
        {
            points = new List<Vector2>();
            if (feature?.points_q == null || feature.points_q.Length < 2 || feature.points_q.Length % 2 != 0) return false;
            for (int index = 0; index < feature.points_q.Length; index += 2)
            {
                Vector2 point = new Vector2(feature.points_q[index] * quantization, feature.points_q[index + 1] * quantization);
                if (points.Count == 0 || Vector2.SqrMagnitude(points[points.Count - 1] - point) > 0.0001f) points.Add(point);
            }
            return feature.geometry_type == "point" ? points.Count == 1 : points.Count >= 2;
        }

        private static int EstimateVertices(RealKbduContextFeature feature, int pointCount)
        {
            if (feature.category == "building") return pointCount * 7 + 1;
            if (feature.category == "barrier") return pointCount * 2;
            if (feature.geometry_type == "polygon") return pointCount + 1;
            return pointCount * 2;
        }

        private static Vector2 Centroid(List<Vector2> points)
        {
            int count = points.Count > 2 && Vector2.SqrMagnitude(points[0] - points[points.Count - 1]) < 0.01f
                ? points.Count - 1
                : points.Count;
            Vector2 sum = Vector2.zero;
            for (int index = 0; index < count; index++) sum += points[index];
            return count > 0 ? sum / count : Vector2.zero;
        }

        private static float GroundOffset(string category)
        {
            if (category == "aeroway") return 0.09f;
            if (category == "road") return 0.07f;
            if (category == "water") return 0.045f;
            if (category == "landcover") return 0.025f;
            return 0.04f;
        }

        private static bool AddRibbon(MeshAccumulator batch, List<Vector2> points, float width, float yOffset, TerrainSampler sampler)
        {
            if (points.Count < 2) return false;
            int start = batch.Vertices.Count;
            float halfWidth = width * 0.5f;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 previous = points[Mathf.Max(0, index - 1)];
                Vector2 next = points[Mathf.Min(points.Count - 1, index + 1)];
                Vector2 direction = (next - previous).normalized;
                if (direction.sqrMagnitude < 0.5f) direction = Vector2.right;
                Vector2 left = new Vector2(-direction.y, direction.x) * halfWidth;
                Vector2 point = points[index];
                float y = sampler.Sample(point.x, point.y) + yOffset;
                batch.Vertices.Add(new Vector3(point.x + left.x, y, point.y + left.y));
                batch.Vertices.Add(new Vector3(point.x - left.x, y, point.y - left.y));
                batch.Uvs.Add(new Vector2(0f, index));
                batch.Uvs.Add(new Vector2(1f, index));
            }
            for (int index = 0; index < points.Count - 1; index++)
            {
                int left = start + index * 2;
                int right = left + 1;
                int nextLeft = left + 2;
                int nextRight = left + 3;
                batch.Triangles.AddRange(new[] { left, nextLeft, right, right, nextLeft, nextRight });
            }
            return true;
        }

        private static bool AddGroundPolygon(MeshAccumulator batch, List<Vector2> sourcePoints, float yOffset, TerrainSampler sampler)
        {
            List<Vector2> points = OpenPolygon(sourcePoints);
            if (points.Count < 3) return false;
            Vector2 centroid = Centroid(points);
            int center = batch.Vertices.Count;
            batch.Vertices.Add(new Vector3(centroid.x, sampler.Sample(centroid.x, centroid.y) + yOffset, centroid.y));
            batch.Uvs.Add(centroid / 128f);
            int first = batch.Vertices.Count;
            foreach (Vector2 point in points)
            {
                batch.Vertices.Add(new Vector3(point.x, sampler.Sample(point.x, point.y) + yOffset, point.y));
                batch.Uvs.Add(point / 128f);
            }
            bool counterClockwise = SignedArea(points) > 0f;
            for (int index = 0; index < points.Count; index++)
            {
                int current = first + index;
                int next = first + (index + 1) % points.Count;
                if (counterClockwise) batch.Triangles.AddRange(new[] { center, next, current });
                else batch.Triangles.AddRange(new[] { center, current, next });
            }
            return true;
        }

        private static bool AddBuilding(MeshAccumulator batch, List<Vector2> sourcePoints, float height, TerrainSampler sampler)
        {
            List<Vector2> points = OpenPolygon(sourcePoints);
            if (points.Count < 3) return false;
            float baseAverage = points.Average(point => sampler.Sample(point.x, point.y));
            float topY = baseAverage + height;
            bool counterClockwise = SignedArea(points) > 0f;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 a = points[index];
                Vector2 b = points[(index + 1) % points.Count];
                float ay = sampler.Sample(a.x, a.y) + 0.03f;
                float by = sampler.Sample(b.x, b.y) + 0.03f;
                int start = batch.Vertices.Count;
                batch.Vertices.Add(new Vector3(a.x, ay, a.y));
                batch.Vertices.Add(new Vector3(b.x, by, b.y));
                batch.Vertices.Add(new Vector3(a.x, topY, a.y));
                batch.Vertices.Add(new Vector3(b.x, topY, b.y));
                batch.Uvs.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
                if (counterClockwise) batch.Triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
                else batch.Triangles.AddRange(new[] { start, start + 1, start + 2, start + 1, start + 3, start + 2 });
            }

            Vector2 centroid = Centroid(points);
            int center = batch.Vertices.Count;
            batch.Vertices.Add(new Vector3(centroid.x, topY, centroid.y));
            batch.Uvs.Add(centroid / 16f);
            int roofStart = batch.Vertices.Count;
            foreach (Vector2 point in points)
            {
                batch.Vertices.Add(new Vector3(point.x, topY, point.y));
                batch.Uvs.Add(point / 16f);
            }
            for (int index = 0; index < points.Count; index++)
            {
                int current = roofStart + index;
                int next = roofStart + (index + 1) % points.Count;
                if (counterClockwise) batch.Triangles.AddRange(new[] { center, next, current });
                else batch.Triangles.AddRange(new[] { center, current, next });
            }
            return true;
        }

        private static bool AddWallPolyline(
            MeshAccumulator batch,
            List<Vector2> points,
            float width,
            float height,
            TerrainSampler sampler)
        {
            if (points.Count < 2) return false;
            // A narrow ground ribbon gives the fence base spatial weight; vertical walls carry the silhouette.
            AddRibbon(batch, points, width, 0.035f, sampler);
            for (int index = 0; index < points.Count - 1; index++)
            {
                Vector2 a = points[index];
                Vector2 b = points[index + 1];
                float ay = sampler.Sample(a.x, a.y) + 0.04f;
                float by = sampler.Sample(b.x, b.y) + 0.04f;
                int start = batch.Vertices.Count;
                batch.Vertices.Add(new Vector3(a.x, ay, a.y));
                batch.Vertices.Add(new Vector3(b.x, by, b.y));
                batch.Vertices.Add(new Vector3(a.x, ay + height, a.y));
                batch.Vertices.Add(new Vector3(b.x, by + height, b.y));
                batch.Uvs.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
                batch.Triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
            }
            return true;
        }

        private static List<Vector2> OpenPolygon(List<Vector2> source)
        {
            List<Vector2> points = new List<Vector2>(source);
            if (points.Count > 2 && Vector2.SqrMagnitude(points[0] - points[points.Count - 1]) < 0.01f)
                points.RemoveAt(points.Count - 1);
            return points;
        }

        private static float SignedArea(List<Vector2> points)
        {
            float twiceArea = 0f;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 a = points[index];
                Vector2 b = points[(index + 1) % points.Count];
                twiceArea += a.x * b.y - b.x * a.y;
            }
            return twiceArea * 0.5f;
        }

        private static GameObject CreateMeshObject(
            Transform parent,
            string name,
            Mesh mesh,
            Material material,
            float maximumDistance,
            float cullScreenHeight)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.isStatic = true;
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = name.IndexOf("building", StringComparison.OrdinalIgnoreCase) >= 0
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            bool stableWaterSurface = QuestEnvironmentMaterialFactory.IsStableWaterMaterial(material) ||
                                      name.IndexOf("water_bank", StringComparison.OrdinalIgnoreCase) >= 0;
            renderer.receiveShadows = !QuestEnvironmentMaterialFactory.IsStableWaterMaterial(material);
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            QuestEnvironmentMaterialFactory.ApplyDeterministicBatchVariation(
                renderer,
                name,
                preserveGlobalContinuity: false);
            if (!stableWaterSurface)
            {
                RealKbduBatchDistanceCuller culler = gameObject.AddComponent<RealKbduBatchDistanceCuller>();
                culler.targetRenderer = renderer;
                culler.maximumDistanceMeters = maximumDistance;
                LODGroup lod = gameObject.AddComponent<LODGroup>();
                lod.fadeMode = LODFadeMode.None;
                lod.SetLODs(new[] { new LOD(cullScreenHeight, new[] { renderer }) });
                lod.RecalculateBounds();
            }
            return gameObject;
        }

        private static int HideLegacyFlatPlaceholders(Transform airportRoot, Transform realRoot)
        {
            int hidden = 0;
            Transform searchRoot = airportRoot.root;
            Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
            List<Transform> proceduralFallbacks = new List<Transform>();
            foreach (Transform candidate in transforms)
            {
                if (candidate == null || candidate == realRoot || candidate.IsChildOf(realRoot)) continue;
                if (!string.Equals(candidate.name, ProceduralFallbackRootName, StringComparison.Ordinal)) continue;
                proceduralFallbacks.Add(candidate);
                hidden += candidate.GetComponentsInChildren<Renderer>(true)
                    .Count(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy);
                candidate.gameObject.SetActive(false);
            }
            string[] exact =
            {
                "Terrain", "Runway_08_26_Approx_4100x75ft", "EnhancedRunwaySurface", "MowedRunwaySafetyArea"
            };
            string[] prefixes =
            {
                "Foothill_", "RunwayCenterline_", "Runway08_Number_Block", "Runway26_Number_Block",
                "RunwayShoulderWear", "TouchdownRubber", "RunwayPatch_", "RunwayExpansionJoint_",
                "RunwayHairlineCrack_", "RunwayGrassVariation", "QualityGateRunway", "FadedCenterlineOverpaint_",
                "RunwayEdgeGravel"
            };
            foreach (Renderer renderer in searchRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(realRoot)) continue;
                if (proceduralFallbacks.Any(fallback => renderer.transform.IsChildOf(fallback))) continue;
                string objectName = renderer.gameObject.name;
                bool shouldHide = exact.Any(item => string.Equals(item, objectName, StringComparison.OrdinalIgnoreCase)) ||
                                  prefixes.Any(prefix => objectName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ||
                                  MountainTemporalStabilityProbe.IsLegacyMountainName(objectName);
                if (!shouldHide || !renderer.enabled) continue;
                renderer.enabled = false;
                if (MountainTemporalStabilityProbe.IsLegacyMountainName(objectName))
                {
                    renderer.gameObject.SetActive(false);
                }
                hidden++;
            }
            return hidden;
        }

        private static MountainTemporalStabilityProbe EnsureMountainStabilityProbe(
            Transform realRoot,
            Transform airportRoot)
        {
            MountainTemporalStabilityProbe probe = realRoot.GetComponent<MountainTemporalStabilityProbe>();
            if (probe == null) probe = realRoot.gameObject.AddComponent<MountainTemporalStabilityProbe>();
            probe.Initialize(realRoot, airportRoot);
            return probe;
        }

        private static int CountTriangles(IEnumerable<MeshFilter> filters)
        {
            int triangles = 0;
            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh != null) triangles += filter.sharedMesh.triangles.Length / 3;
            }
            return triangles;
        }

        private sealed class RuntimeTerrainLayer
        {
            public readonly RealKbduTerrainLayer Source;
            public readonly float[] Heights;

            public RuntimeTerrainLayer(RealKbduTerrainLayer source, float[] heights)
            {
                Source = source;
                Heights = heights;
            }

            public bool Contains(float x, float z)
            {
                return x >= Source.min_x_meters && x <= Source.max_x_meters &&
                       z >= Source.min_z_meters && z <= Source.max_z_meters;
            }

            public float Sample(float x, float z)
            {
                float column = Mathf.Clamp((x - Source.min_x_meters) / Source.spacing_meters, 0f, Source.width - 1f);
                float row = Mathf.Clamp((z - Source.min_z_meters) / Source.spacing_meters, 0f, Source.height - 1f);
                int x0 = Mathf.FloorToInt(column);
                int z0 = Mathf.FloorToInt(row);
                int x1 = Mathf.Min(Source.width - 1, x0 + 1);
                int z1 = Mathf.Min(Source.height - 1, z0 + 1);
                float tx = column - x0;
                float tz = row - z0;
                float south = Mathf.Lerp(Heights[z0 * Source.width + x0], Heights[z0 * Source.width + x1], tx);
                float north = Mathf.Lerp(Heights[z1 * Source.width + x0], Heights[z1 * Source.width + x1], tx);
                return Mathf.Lerp(south, north, tz);
            }
        }

        private sealed class TerrainSampler
        {
            private readonly RuntimeTerrainLayer _airport;
            private readonly RuntimeTerrainLayer _inner;
            private readonly RuntimeTerrainLayer _mid;
            private readonly RuntimeTerrainLayer _far;

            public TerrainSampler(Dictionary<string, RuntimeTerrainLayer> layers)
            {
                _airport = layers["airport_patch"];
                _inner = layers["inner_4km"];
                _mid = layers["mid_12km"];
                _far = layers["far_24km"];
            }

            public float Sample(float x, float z)
            {
                if (_airport.Contains(x, z)) return _airport.Sample(x, z);
                float radius = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                if (radius <= 2000f) return _inner.Sample(x, z);
                if (radius <= 6000f) return _mid.Sample(x, z);
                return _far.Sample(x, z);
            }
        }

        private sealed class TerrainMeshAssembler
        {
            private readonly string _name;
            private readonly List<Vector3> _vertices;
            private readonly List<Vector2> _uvs;
            private readonly List<int> _triangles;
            private readonly Dictionary<Vector2, int> _vertexLookup = new Dictionary<Vector2, int>();

            public TerrainMeshAssembler(string name, int triangleCapacity)
            {
                _name = name;
                _vertices = new List<Vector3>(Mathf.Max(256, triangleCapacity / 2));
                _uvs = new List<Vector2>(Mathf.Max(256, triangleCapacity / 2));
                _triangles = new List<int>(Mathf.Max(768, triangleCapacity * 3));
            }

            public void AddQuad(
                float x0,
                float z0,
                float x1,
                float z1,
                Func<float, float, float> height)
            {
                int southWest = Vertex(x0, z0, height);
                int northWest = Vertex(x0, z1, height);
                int southEast = Vertex(x1, z0, height);
                int northEast = Vertex(x1, z1, height);
                AddTriangle(southWest, northWest, southEast);
                AddTriangle(southEast, northWest, northEast);
            }

            public void AddVerticalTransition(
                float leftX,
                IReadOnlyList<float> leftCoordinates,
                Func<float, float, float> leftHeight,
                float rightX,
                IReadOnlyList<float> rightCoordinates,
                Func<float, float, float> rightHeight)
            {
                int left = 0;
                int right = 0;
                while (left < leftCoordinates.Count - 1 || right < rightCoordinates.Count - 1)
                {
                    int leftCurrent = Vertex(leftX, leftCoordinates[left], leftHeight);
                    int rightCurrent = Vertex(rightX, rightCoordinates[right], rightHeight);
                    bool advanceLeft = right >= rightCoordinates.Count - 1 ||
                                       (left < leftCoordinates.Count - 1 &&
                                        leftCoordinates[left + 1] <= rightCoordinates[right + 1] + TerrainSeamEpsilon);
                    if (advanceLeft)
                    {
                        int leftNext = Vertex(leftX, leftCoordinates[left + 1], leftHeight);
                        AddTriangle(leftCurrent, leftNext, rightCurrent);
                        left++;
                    }
                    else
                    {
                        int rightNext = Vertex(rightX, rightCoordinates[right + 1], rightHeight);
                        AddTriangle(leftCurrent, rightNext, rightCurrent);
                        right++;
                    }
                }
            }

            public void AddHorizontalTransition(
                float bottomZ,
                IReadOnlyList<float> bottomCoordinates,
                Func<float, float, float> bottomHeight,
                float topZ,
                IReadOnlyList<float> topCoordinates,
                Func<float, float, float> topHeight)
            {
                int bottom = 0;
                int top = 0;
                while (bottom < bottomCoordinates.Count - 1 || top < topCoordinates.Count - 1)
                {
                    int bottomCurrent = Vertex(bottomCoordinates[bottom], bottomZ, bottomHeight);
                    int topCurrent = Vertex(topCoordinates[top], topZ, topHeight);
                    bool advanceBottom = top >= topCoordinates.Count - 1 ||
                                         (bottom < bottomCoordinates.Count - 1 &&
                                          bottomCoordinates[bottom + 1] <= topCoordinates[top + 1] + TerrainSeamEpsilon);
                    if (advanceBottom)
                    {
                        int bottomNext = Vertex(bottomCoordinates[bottom + 1], bottomZ, bottomHeight);
                        AddTriangle(bottomCurrent, topCurrent, bottomNext);
                        bottom++;
                    }
                    else
                    {
                        int topNext = Vertex(topCoordinates[top + 1], topZ, topHeight);
                        AddTriangle(bottomCurrent, topCurrent, topNext);
                        top++;
                    }
                }
            }

            public void AddCornerQuad(
                float x0,
                float z0,
                float x1,
                float z1,
                Func<float, float, float> outerHeight,
                Func<float, float, float> innerHeight)
            {
                float radius = Mathf.Min(
                    Mathf.Min(Mathf.Abs(x0), Mathf.Abs(x1)),
                    Mathf.Min(Mathf.Abs(z0), Mathf.Abs(z1)));
                int southWest = CornerVertex(x0, z0, radius, outerHeight, innerHeight);
                int northWest = CornerVertex(x0, z1, radius, outerHeight, innerHeight);
                int southEast = CornerVertex(x1, z0, radius, outerHeight, innerHeight);
                int northEast = CornerVertex(x1, z1, radius, outerHeight, innerHeight);
                AddTriangle(southWest, northWest, southEast);
                AddTriangle(southEast, northWest, northEast);
            }

            public Mesh ToMesh()
            {
                Mesh mesh = new Mesh { name = _name + "_USGS_StitchedMesh" };
                if (_vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_triangles, 0, true);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            private int CornerVertex(
                float x,
                float z,
                float radius,
                Func<float, float, float> outerHeight,
                Func<float, float, float> innerHeight)
            {
                bool onInnerCorner = Mathf.Abs(Mathf.Abs(x) - radius) <= TerrainSeamEpsilon &&
                                     Mathf.Abs(Mathf.Abs(z) - radius) <= TerrainSeamEpsilon;
                return Vertex(x, z, onInnerCorner ? innerHeight : outerHeight);
            }

            private int Vertex(float x, float z, Func<float, float, float> height)
            {
                Vector2 key = new Vector2(x, z);
                if (_vertexLookup.TryGetValue(key, out int index)) return index;
                index = _vertices.Count;
                _vertexLookup.Add(key, index);
                _vertices.Add(new Vector3(x, height(x, z), z));
                _uvs.Add(new Vector2(x / 192f, z / 192f));
                return index;
            }

            private void AddTriangle(int first, int second, int third)
            {
                if (first == second || second == third || first == third) return;
                _triangles.Add(first);
                _triangles.Add(second);
                _triangles.Add(third);
            }
        }

        private sealed class MeshAccumulator
        {
            public readonly string Key;
            public readonly int Ring;
            public readonly string Category;
            public readonly string MaterialId;
            public readonly float MaximumDistanceMeters;
            public readonly float CullScreenHeight;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
            public int FeatureCount;

            public MeshAccumulator(
                string key,
                int ring,
                string category,
                string materialId,
                float maximumDistanceMeters,
                float cullScreenHeight)
            {
                Key = key;
                Ring = ring;
                Category = category;
                MaterialId = materialId;
                MaximumDistanceMeters = maximumDistanceMeters;
                CullScreenHeight = cullScreenHeight;
            }

            public Mesh ToMesh()
            {
                Mesh mesh = new Mesh { name = "RealKbduBatch_" + Key.Replace('|', '_') };
                if (Vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(Vertices);
                mesh.SetUVs(0, Uvs);
                mesh.SetTriangles(Triangles, 0, true);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private sealed class MaterialLibrary
        {
            private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            private readonly Texture2D _detailTexture;

            public int MaterialCount => _materials.Count;
            public int TextureCount => (_detailTexture != null ? 1 : 0) + QuestEnvironmentMaterialFactory.LoadedGroundTextureCount;

            public MaterialLibrary()
            {
                _detailTexture = CreateDetailTexture();
            }

            public Material Get(string id)
            {
                if (_materials.TryGetValue(id, out Material existing)) return existing;
                Color color = ColorFor(id);
                if (string.Equals(id, "water", StringComparison.Ordinal))
                {
                    Material water = QuestEnvironmentMaterialFactory.CreateStableWaterMaterial("RealKBDU_water", color);
                    _materials.Add(id, water);
                    return water;
                }
                if (QuestEnvironmentMaterialFactory.IsGroundMaterialId(id))
                {
                    Material ground = QuestEnvironmentMaterialFactory.CreateGroundMaterial("RealKBDU_" + id, id, color);
                    _materials.Add(id, ground);
                    return ground;
                }
                Shader shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Standard shader unavailable for real KBDU materials");
                Material material = new Material(shader)
                {
                    name = "RealKBDU_" + id,
                    color = color,
                    mainTexture = _detailTexture,
                    mainTextureScale = id == "building_footprint" ? new Vector2(0.3f, 0.3f) : new Vector2(4f, 4f),
                    enableInstancing = true
                };
                if (material.HasProperty("_Glossiness"))
                    material.SetFloat("_Glossiness", id == "water" ? 0.55f : id == "asphalt" ? 0.16f : 0.08f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                _materials.Add(id, material);
                return material;
            }

            private static Color ColorFor(string id)
            {
                switch (id)
                {
                    case "irrigated_field": return Hex("#B7D9A2");
                    case "harvested_field": return Hex("#D5C286");
                    case "orchard": return Hex("#9FC18E");
                    case "meadow": return Hex("#B5C991");
                    case "forest": return Hex("#829679");
                    case "quarry": return Hex("#B6AA98");
                    case "industrial_ground": return Hex("#B1AAA0");
                    case "water": return Hex("#28536B");
                    case "water_bank": return Hex("#837156");
                    case "asphalt": return Hex("#343536");
                    case "concrete": return Hex("#777777");
                    case "gravel": return Hex("#756A57");
                    case "airfield_turf": return Hex("#ACB987");
                    case "building_footprint": return Hex("#696866");
                    case "runway_marking": return Hex("#E5E3D5");
                    case "terrain_mid": return Hex("#A3A071");
                    case "terrain_far": return Hex("#929578");
                    default: return Hex("#A2A875");
                }
            }

            private static Color Hex(string value)
            {
                return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;
            }

            private static Texture2D CreateDetailTexture()
            {
                const int size = 96;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = "RealKBDU_SharedDeterministicMicroDetail",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4
                };
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float broad = Mathf.Sin(x * 0.19f) * Mathf.Cos(y * 0.16f) * 0.075f;
                        uint hash = unchecked((uint)(x * 374761393 + y * 668265263));
                        hash = (hash ^ (hash >> 13)) * 1274126177u;
                        float noise = ((hash ^ (hash >> 16)) & 0xFFFF) / 65535f;
                        float value = Mathf.Clamp01(0.84f + noise * 0.22f + broad);
                        texture.SetPixel(x, y, new Color(value, value, value, 1f));
                    }
                }
                texture.Apply(true, true);
                return texture;
            }
        }
    }
}
