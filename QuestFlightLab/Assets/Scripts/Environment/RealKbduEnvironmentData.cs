using System;
using UnityEngine;

namespace QuestFlightLab.Environment
{
    [Serializable]
    internal sealed class RealKbduTerrainDocument
    {
        public int schema_version;
        public string label;
        public RealKbduOrigin origin;
        public float height_quantization_meters;
        public float skirt_depth_meters;
        public RealKbduTerrainLayer[] layers;
        public RealKbduTerrainSourceSnapshot source_snapshot;
        public RealKbduTerrainBudgets budgets;
    }

    [Serializable]
    internal sealed class RealKbduOrigin
    {
        public double latitude_degrees;
        public double longitude_degrees;
        public double elevation_msl_meters;
        public int source_elevation_feet;
    }

    [Serializable]
    internal sealed class RealKbduTerrainLayer
    {
        public string id;
        public string kind;
        public int priority;
        public float min_x_meters;
        public float max_x_meters;
        public float min_z_meters;
        public float max_z_meters;
        public float spacing_meters;
        public int width;
        public int height;
        public int sample_count;
        public int expected_triangle_count;
        public float inner_radius_meters;
        public float outer_radius_meters;
        public string cutout_layer;
        public string height_dm_little_endian_base64;
    }

    [Serializable]
    internal sealed class RealKbduTerrainSourceSnapshot
    {
        public string id;
        public string retrieved_utc;
        public RealKbduUsgsEvidence usgs_evidence;
    }

    [Serializable]
    internal sealed class RealKbduUsgsEvidence
    {
        public int sample_count;
        public bool location_mapping_verified;
        public string arcgis_axis_order;
    }

    [Serializable]
    internal sealed class RealKbduTerrainBudgets
    {
        public int height_samples;
        public int expected_terrain_triangles;
        public int maximum_height_samples;
        public int maximum_terrain_triangles;
    }

    [Serializable]
    internal sealed class RealKbduContextDocument
    {
        public int schema_version;
        public string label;
        public RealKbduOrigin origin;
        public float coordinate_quantization_meters;
        public RealKbduFaaContext faa;
        public RealKbduOsmContext openstreetmap;
        public RealKbduMacroMaterialFallback macro_material_fallback;
    }

    [Serializable]
    internal sealed class RealKbduFaaContext
    {
        public string license;
        public string attribution;
        public RealKbduFaaFacility facility;
        public RealKbduFaaRunway[] runways;
    }

    [Serializable]
    internal sealed class RealKbduFaaFacility
    {
        public string EFF_DATE;
        public string ARPT_ID;
        public string ICAO_ID;
        public string ARPT_NAME;
        public double LAT_DECIMAL;
        public double LONG_DECIMAL;
        public float ELEV;
    }

    [Serializable]
    internal sealed class RealKbduFaaRunway
    {
        public string runway_id;
        public int length_feet;
        public int width_feet;
        public string surface;
        public RealKbduFaaRunwayEndpoint[] endpoints;
    }

    [Serializable]
    internal sealed class RealKbduFaaRunwayEndpoint
    {
        public float x_east_meters;
        public float z_north_meters;
        public float usgs_elevation_msl_meters;
    }

    [Serializable]
    internal sealed class RealKbduOsmContext
    {
        public string license;
        public string attribution;
        public RealKbduContextFeature[] features;
        public RealKbduOsmEvidence evidence;
    }

    [Serializable]
    internal sealed class RealKbduOsmEvidence
    {
        public int output_feature_count;
        public int output_point_count;
    }

    [Serializable]
    internal sealed class RealKbduMacroMaterialFallback
    {
        public string default_material_id;
        public RealKbduImageryGate imagery_gate;
    }

    [Serializable]
    internal sealed class RealKbduImageryGate
    {
        public string status;
        public int availability_feature_count;
        public bool raw_imagery_downloaded;
        public string decision;
        public string exact_blocker;
    }

    [Serializable]
    internal sealed class RealKbduContextFeature
    {
        public string category;
        public string macro_material_id;
        public string geometry_type;
        public RealKbduFeatureSource source;
        public RealKbduFeatureTags tags;
        public float render_width_meters;
        public float render_height_meters;
        public int[] points_q;
    }

    [Serializable]
    internal sealed class RealKbduFeatureSource
    {
        public string osm_type;
        public long osm_id;
        public string part;
    }

    [Serializable]
    internal sealed class RealKbduFeatureTags
    {
        public string name;
        public string aeroway;
        public string building;
        public string highway;
        public string surface;
        public string waterway;
        public string natural;
        public string water;
        public string landuse;
        public string barrier;
    }

    public sealed class RealKbduEnvironmentStatus : MonoBehaviour
    {
        public bool dataValidated;
        public string sourceSnapshotId;
        public string profileName;
        public string coordinateFrame;
        public double originLatitudeDegrees;
        public double originLongitudeDegrees;
        public double originElevationMslMeters;
        public int terrainLayerCount;
        public int terrainHeightSamples;
        public int sourceVectorFeatures;
        public int renderedVectorFeatures;
        public int skippedVectorFeatures;
        public int runtimeBatchCount;
        public int rendererCount;
        public int meshCount;
        public int triangleCount;
        public int materialCount;
        public int textureCount;
        public int distanceCullerCount;
        public int lodGroupCount;
        public string faaRunwaySummary;
        public string osmAttribution;
        public string imageryStatus;
        public string notes;

        public string Summary =>
            $"profile={profileName} snapshot={sourceSnapshotId} validated={dataValidated} " +
            $"terrainLayers={terrainLayerCount} sourceFeatures={sourceVectorFeatures} " +
            $"renderedFeatures={renderedVectorFeatures} batches={runtimeBatchCount} " +
            $"renderers={rendererCount} meshes={meshCount} tris={triangleCount} materials={materialCount}";
    }

    /// <summary>Low-frequency distance culling for a combined spatial batch, never one component per source feature.</summary>
    public sealed class RealKbduBatchDistanceCuller : MonoBehaviour
    {
        public Renderer targetRenderer;
        public float maximumDistanceMeters = 12000f;
        public int frameInterval = 24;

        private int _phase;

        private void Awake()
        {
            _phase = Mathf.Abs(GetInstanceID()) % Mathf.Max(1, frameInterval);
        }

        private void Update()
        {
            if (targetRenderer == null || Time.frameCount % Mathf.Max(1, frameInterval) != _phase) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 closest = targetRenderer.bounds.ClosestPoint(camera.transform.position);
            bool visible = (closest - camera.transform.position).sqrMagnitude <= maximumDistanceMeters * maximumDistanceMeters;
            if (targetRenderer.enabled != visible) targetRenderer.enabled = visible;
        }
    }
}
