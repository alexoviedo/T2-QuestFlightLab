using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestFlightLab.Environment
{
    public sealed class RealKbduWaterStatus : MonoBehaviour
    {
        public int sourceLinearFeatures;
        public int sourcePolygonFeatures;
        public int renderedLinearFeatures;
        public int renderedPolygonFeatures;
        public int rejectedFeatures;
        public int bankedNearFeatures;
        public int outputCenterlinePoints;
        public int waterTriangleCount;
        public int bankTriangleCount;
        public float maximumTurnDegrees;
        public float minimumTerrainSeparationMeters;
        public bool opaqueZWriteMaterial;
        public bool animatedUv;
        public bool waterUsesLodOrDistanceCulling;

        public string Summary =>
            $"water lines={renderedLinearFeatures}/{sourceLinearFeatures} polygons={renderedPolygonFeatures}/{sourcePolygonFeatures} " +
            $"banks={bankedNearFeatures} rejected={rejectedFeatures} points={outputCenterlinePoints} " +
            $"tris={waterTriangleCount}+{bankTriangleCount} separation={minimumTerrainSeparationMeters:0.000}m " +
            $"opaqueZWrite={opaqueZWriteMaterial} animatedUv={animatedUv} lodOrCulling={waterUsesLodOrDistanceCulling}";
    }

    /// <summary>
    /// Deterministic, camera-independent water geometry for OSM waterways and water polygons.
    /// Linear features are smoothed offline-at-build-time and resampled by arc length; reservoirs
    /// are ear-clipped at one fixed level. Both paths stay well clear of the terrain depth plane.
    /// </summary>
    public static class WaterwayMeshBuilder
    {
        public const float CenterlineSampleSpacingMeters = 12f;
        public const float WaterTerrainClearanceMeters = 0.18f;
        public const float MinimumAcceptedTerrainSeparationMeters = 0.12f;
        public const float BankTerrainClearanceMeters = 0.22f;
        public const float MaximumSegmentTurnDegrees = 55f;
        public const float MinimumWaterwayWidthMeters = 0.8f;
        public const float MaximumWaterwayWidthMeters = 28f;
        public const int SmoothingPasses = 3;

        public delegate float TerrainHeightSampler(float x, float z);

        public sealed class MeshBuffers
        {
            public readonly List<Vector3> Vertices;
            public readonly List<Vector2> Uvs;
            public readonly List<int> Triangles;

            public MeshBuffers(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
            {
                Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
                Uvs = uvs ?? throw new ArgumentNullException(nameof(uvs));
                Triangles = triangles ?? throw new ArgumentNullException(nameof(triangles));
            }
        }

        public struct BuildStatistics
        {
            public int sourcePointCount;
            public int outputPointCount;
            public int waterTriangleCount;
            public int bankTriangleCount;
            public float sourceLengthMeters;
            public float maximumTurnDegrees;
            public float minimumTerrainSeparationMeters;
            public bool horizontalSurface;
            public bool rejectedSelfIntersection;

            public void Accumulate(BuildStatistics other)
            {
                sourcePointCount += other.sourcePointCount;
                outputPointCount += other.outputPointCount;
                waterTriangleCount += other.waterTriangleCount;
                bankTriangleCount += other.bankTriangleCount;
                sourceLengthMeters += other.sourceLengthMeters;
                maximumTurnDegrees = Mathf.Max(maximumTurnDegrees, other.maximumTurnDegrees);
                if (minimumTerrainSeparationMeters <= 0f)
                    minimumTerrainSeparationMeters = other.minimumTerrainSeparationMeters;
                else if (other.minimumTerrainSeparationMeters > 0f)
                    minimumTerrainSeparationMeters = Mathf.Min(minimumTerrainSeparationMeters, other.minimumTerrainSeparationMeters);
                horizontalSurface |= other.horizontalSurface;
                rejectedSelfIntersection |= other.rejectedSelfIntersection;
            }
        }

        public static bool TryAppendLinearWaterway(
            IReadOnlyList<Vector2> sourcePoints,
            float requestedWidthMeters,
            TerrainHeightSampler terrainHeight,
            MeshBuffers water,
            MeshBuffers banks,
            out BuildStatistics statistics)
        {
            statistics = new BuildStatistics
            {
                sourcePointCount = sourcePoints?.Count ?? 0,
                minimumTerrainSeparationMeters = float.MaxValue
            };
            if (terrainHeight == null || water == null || sourcePoints == null || sourcePoints.Count < 2)
                return false;

            List<Vector2> centerline = SmoothAndResampleCenterline(sourcePoints, CenterlineSampleSpacingMeters);
            if (centerline.Count < 2 || HasSelfIntersection(centerline, false))
            {
                statistics.rejectedSelfIntersection = true;
                return false;
            }

            float maximumTurn = MaximumTurnDegrees(centerline);
            if (maximumTurn > MaximumSegmentTurnDegrees + 0.1f)
                return false;

            float width = Mathf.Clamp(requestedWidthMeters, MinimumWaterwayWidthMeters, MaximumWaterwayWidthMeters);
            float halfWidth = width * 0.5f;
            float bankWidth = Mathf.Clamp(width * 0.28f, 0.9f, 2.6f);
            float cumulativeLength = 0f;
            int waterStart = water.Vertices.Count;
            int bankStart = banks != null ? banks.Vertices.Count : 0;

            for (int index = 0; index < centerline.Count; index++)
            {
                if (index > 0) cumulativeLength += Vector2.Distance(centerline[index - 1], centerline[index]);
                Vector2 tangent = StableTangent(centerline, index);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                Vector2 center = centerline[index];
                Vector2 left = center + normal * halfWidth;
                Vector2 right = center - normal * halfWidth;
                float ground = Mathf.Max(
                    terrainHeight(center.x, center.y),
                    Mathf.Max(terrainHeight(left.x, left.y), terrainHeight(right.x, right.y)));
                float waterY = ground + WaterTerrainClearanceMeters;

                water.Vertices.Add(new Vector3(left.x, waterY, left.y));
                water.Vertices.Add(new Vector3(right.x, waterY, right.y));
                water.Uvs.Add(new Vector2(0f, cumulativeLength / 24f));
                water.Uvs.Add(new Vector2(1f, cumulativeLength / 24f));
                statistics.minimumTerrainSeparationMeters = Mathf.Min(
                    statistics.minimumTerrainSeparationMeters,
                    Mathf.Min(
                        waterY - terrainHeight(left.x, left.y),
                        waterY - terrainHeight(right.x, right.y)));

                if (banks == null) continue;
                Vector2 leftInner = center + normal * (halfWidth + 0.12f);
                Vector2 leftOuter = center + normal * (halfWidth + bankWidth);
                Vector2 rightInner = center - normal * (halfWidth + 0.12f);
                Vector2 rightOuter = center - normal * (halfWidth + bankWidth);
                float leftInnerY = Mathf.Max(terrainHeight(leftInner.x, leftInner.y) + BankTerrainClearanceMeters, waterY + 0.06f);
                float leftOuterY = Mathf.Max(terrainHeight(leftOuter.x, leftOuter.y) + BankTerrainClearanceMeters, waterY + 0.06f);
                float rightInnerY = Mathf.Max(terrainHeight(rightInner.x, rightInner.y) + BankTerrainClearanceMeters, waterY + 0.06f);
                float rightOuterY = Mathf.Max(terrainHeight(rightOuter.x, rightOuter.y) + BankTerrainClearanceMeters, waterY + 0.06f);
                banks.Vertices.Add(new Vector3(leftOuter.x, leftOuterY, leftOuter.y));
                banks.Vertices.Add(new Vector3(leftInner.x, leftInnerY, leftInner.y));
                banks.Vertices.Add(new Vector3(rightInner.x, rightInnerY, rightInner.y));
                banks.Vertices.Add(new Vector3(rightOuter.x, rightOuterY, rightOuter.y));
                float bankV = cumulativeLength / 8f;
                banks.Uvs.Add(new Vector2(0f, bankV));
                banks.Uvs.Add(new Vector2(1f, bankV));
                banks.Uvs.Add(new Vector2(0f, bankV));
                banks.Uvs.Add(new Vector2(1f, bankV));
            }

            for (int index = 0; index < centerline.Count - 1; index++)
            {
                int left = waterStart + index * 2;
                water.Triangles.AddRange(new[] { left, left + 2, left + 1, left + 1, left + 2, left + 3 });
                if (banks == null) continue;
                int bank = bankStart + index * 4;
                banks.Triangles.AddRange(new[]
                {
                    bank, bank + 4, bank + 1,
                    bank + 1, bank + 4, bank + 5,
                    bank + 2, bank + 6, bank + 3,
                    bank + 3, bank + 6, bank + 7
                });
            }

            statistics.outputPointCount = centerline.Count;
            statistics.sourceLengthMeters = PolylineLength(sourcePoints);
            statistics.maximumTurnDegrees = maximumTurn;
            statistics.waterTriangleCount = (centerline.Count - 1) * 2;
            statistics.bankTriangleCount = banks != null ? (centerline.Count - 1) * 4 : 0;
            statistics.horizontalSurface = false;
            return statistics.minimumTerrainSeparationMeters >= MinimumAcceptedTerrainSeparationMeters;
        }

        public static bool TryAppendReservoir(
            IReadOnlyList<Vector2> sourcePoints,
            TerrainHeightSampler terrainHeight,
            MeshBuffers water,
            out BuildStatistics statistics)
        {
            statistics = new BuildStatistics
            {
                sourcePointCount = sourcePoints?.Count ?? 0,
                minimumTerrainSeparationMeters = float.MaxValue,
                horizontalSurface = true
            };
            if (terrainHeight == null || water == null || sourcePoints == null) return false;

            List<Vector2> polygon = SanitizePolygon(sourcePoints);
            if (polygon.Count < 3 || HasSelfIntersection(polygon, true))
            {
                statistics.rejectedSelfIntersection = true;
                return false;
            }
            if (!TryTriangulatePolygon(polygon, out List<int> triangles)) return false;

            float waterLevel = float.MinValue;
            foreach (Vector2 point in polygon)
                waterLevel = Mathf.Max(waterLevel, terrainHeight(point.x, point.y));
            for (int index = 0; index < triangles.Count; index += 3)
            {
                Vector2 centroid = (polygon[triangles[index]] + polygon[triangles[index + 1]] + polygon[triangles[index + 2]]) / 3f;
                waterLevel = Mathf.Max(waterLevel, terrainHeight(centroid.x, centroid.y));
            }
            waterLevel += WaterTerrainClearanceMeters;

            int start = water.Vertices.Count;
            foreach (Vector2 point in polygon)
            {
                water.Vertices.Add(new Vector3(point.x, waterLevel, point.y));
                water.Uvs.Add(point / 32f);
                statistics.minimumTerrainSeparationMeters = Mathf.Min(
                    statistics.minimumTerrainSeparationMeters,
                    waterLevel - terrainHeight(point.x, point.y));
            }
            foreach (int triangle in triangles) water.Triangles.Add(start + triangle);

            statistics.outputPointCount = polygon.Count;
            statistics.waterTriangleCount = triangles.Count / 3;
            // The smoothness gate applies to resampled linear waterways. Polygon corners preserve
            // the actual OSM shoreline and must not contaminate the centerline-turn statistic.
            statistics.maximumTurnDegrees = 0f;
            return statistics.minimumTerrainSeparationMeters >= MinimumAcceptedTerrainSeparationMeters;
        }

        public static List<Vector2> SmoothAndResampleCenterline(IReadOnlyList<Vector2> sourcePoints, float spacingMeters)
        {
            List<Vector2> cleaned = SanitizePolyline(sourcePoints);
            if (cleaned.Count < 2) return cleaned;
            List<Vector2> smoothed = cleaned;
            for (int pass = 0; pass < SmoothingPasses; pass++) smoothed = ChaikinPass(smoothed);
            List<Vector2> resampled = ResampleByArcLength(smoothed, Mathf.Clamp(spacingMeters, 8f, 15f));

            // Very short source segments can retain one abrupt corner after uniform resampling.
            // One bounded extra smoothing pass is deterministic and keeps all points within the
            // source polyline's local convex hull.
            if (MaximumTurnDegrees(resampled) > MaximumSegmentTurnDegrees)
                resampled = ResampleByArcLength(ChaikinPass(smoothed), Mathf.Clamp(spacingMeters, 8f, 15f));
            return resampled;
        }

        public static float PolylineLength(IReadOnlyList<Vector2> points)
        {
            if (points == null) return 0f;
            float length = 0f;
            for (int index = 1; index < points.Count; index++) length += Vector2.Distance(points[index - 1], points[index]);
            return length;
        }

        public static float MaximumTurnDegrees(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3) return 0f;
            float maximum = 0f;
            for (int index = 1; index < points.Count - 1; index++)
            {
                Vector2 incoming = points[index] - points[index - 1];
                Vector2 outgoing = points[index + 1] - points[index];
                if (incoming.sqrMagnitude < 0.01f || outgoing.sqrMagnitude < 0.01f) continue;
                maximum = Mathf.Max(maximum, Vector2.Angle(incoming, outgoing));
            }
            return maximum;
        }

        public static bool HasSelfIntersection(IReadOnlyList<Vector2> points, bool closed)
        {
            if (points == null) return false;
            int segmentCount = closed ? points.Count : points.Count - 1;
            if (segmentCount < 3) return false;
            for (int first = 0; first < segmentCount; first++)
            {
                int firstNext = (first + 1) % points.Count;
                for (int second = first + 1; second < segmentCount; second++)
                {
                    int secondNext = (second + 1) % points.Count;
                    if (first == second || firstNext == second || secondNext == first) continue;
                    if (closed && first == 0 && secondNext == 0) continue;
                    if (SegmentsIntersect(points[first], points[firstNext], points[second], points[secondNext])) return true;
                }
            }
            return false;
        }

        private static List<Vector2> SanitizePolyline(IReadOnlyList<Vector2> source)
        {
            List<Vector2> points = new List<Vector2>();
            if (source == null) return points;
            foreach (Vector2 point in source)
            {
                if (points.Count == 0 || Vector2.SqrMagnitude(points[points.Count - 1] - point) > 0.01f)
                    points.Add(point);
            }
            return points;
        }

        private static List<Vector2> SanitizePolygon(IReadOnlyList<Vector2> source)
        {
            List<Vector2> points = SanitizePolyline(source);
            if (points.Count > 2 && Vector2.SqrMagnitude(points[0] - points[points.Count - 1]) < 0.01f)
                points.RemoveAt(points.Count - 1);
            for (int index = points.Count - 1; index >= 0 && points.Count >= 3; index--)
            {
                Vector2 previous = points[(index - 1 + points.Count) % points.Count];
                Vector2 current = points[index];
                Vector2 next = points[(index + 1) % points.Count];
                if (Mathf.Abs(Cross(current - previous, next - current)) <= 0.0001f) points.RemoveAt(index);
            }
            return points;
        }

        private static List<Vector2> ChaikinPass(IReadOnlyList<Vector2> points)
        {
            if (points.Count < 3) return new List<Vector2>(points);
            List<Vector2> result = new List<Vector2>(points.Count * 2) { points[0] };
            for (int index = 0; index < points.Count - 1; index++)
            {
                Vector2 first = points[index];
                Vector2 second = points[index + 1];
                result.Add(Vector2.Lerp(first, second, 0.25f));
                result.Add(Vector2.Lerp(first, second, 0.75f));
            }
            result.Add(points[points.Count - 1]);
            return SanitizePolyline(result);
        }

        private static List<Vector2> ResampleByArcLength(IReadOnlyList<Vector2> points, float spacing)
        {
            List<Vector2> result = new List<Vector2>();
            if (points.Count == 0) return result;
            result.Add(points[0]);
            float distanceUntilSample = spacing;
            Vector2 segmentStart = points[0];
            for (int index = 1; index < points.Count; index++)
            {
                Vector2 segmentEnd = points[index];
                float segmentLength = Vector2.Distance(segmentStart, segmentEnd);
                while (segmentLength >= distanceUntilSample && segmentLength > 0.0001f)
                {
                    float t = distanceUntilSample / segmentLength;
                    Vector2 sample = Vector2.Lerp(segmentStart, segmentEnd, t);
                    result.Add(sample);
                    segmentStart = sample;
                    segmentLength = Vector2.Distance(segmentStart, segmentEnd);
                    distanceUntilSample = spacing;
                }
                distanceUntilSample -= segmentLength;
                segmentStart = segmentEnd;
            }
            Vector2 last = points[points.Count - 1];
            if (Vector2.SqrMagnitude(result[result.Count - 1] - last) > 0.01f) result.Add(last);
            else result[result.Count - 1] = last;
            return result;
        }

        private static Vector2 StableTangent(IReadOnlyList<Vector2> points, int index)
        {
            Vector2 previous = points[Mathf.Max(0, index - 1)];
            Vector2 next = points[Mathf.Min(points.Count - 1, index + 1)];
            Vector2 tangent = next - previous;
            if (tangent.sqrMagnitude < 0.0001f) tangent = Vector2.right;
            return tangent.normalized;
        }

        private static float MaximumTurnDegreesClosed(IReadOnlyList<Vector2> points)
        {
            float maximum = 0f;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 incoming = points[index] - points[(index - 1 + points.Count) % points.Count];
                Vector2 outgoing = points[(index + 1) % points.Count] - points[index];
                if (incoming.sqrMagnitude < 0.01f || outgoing.sqrMagnitude < 0.01f) continue;
                maximum = Mathf.Max(maximum, Vector2.Angle(incoming, outgoing));
            }
            return maximum;
        }

        private static bool TryTriangulatePolygon(List<Vector2> source, out List<int> triangles)
        {
            triangles = new List<int>();
            List<Vector2> points = new List<Vector2>(source);
            if (SignedArea(points) < 0f) points.Reverse();
            List<int> remaining = new List<int>(points.Count);
            for (int index = 0; index < points.Count; index++) remaining.Add(index);

            int guard = points.Count * points.Count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int cursor = 0; cursor < remaining.Count; cursor++)
                {
                    int previous = remaining[(cursor - 1 + remaining.Count) % remaining.Count];
                    int current = remaining[cursor];
                    int next = remaining[(cursor + 1) % remaining.Count];
                    if (Cross(points[current] - points[previous], points[next] - points[current]) <= 0.0001f) continue;
                    bool containsPoint = false;
                    foreach (int candidate in remaining)
                    {
                        if (candidate == previous || candidate == current || candidate == next) continue;
                        if (PointInTriangle(points[candidate], points[previous], points[current], points[next]))
                        {
                            containsPoint = true;
                            break;
                        }
                    }
                    if (containsPoint) continue;

                    // Clockwise in X/Z gives an upward Unity normal.
                    triangles.Add(previous);
                    triangles.Add(next);
                    triangles.Add(current);
                    remaining.RemoveAt(cursor);
                    clipped = true;
                    break;
                }
                if (!clipped) return false;
            }
            if (remaining.Count != 3) return false;
            triangles.Add(remaining[0]);
            triangles.Add(remaining[2]);
            triangles.Add(remaining[1]);

            // The working list may have been reversed. Remap by exact point value to source order.
            if (SignedArea(source) < 0f)
            {
                for (int index = 0; index < triangles.Count; index++)
                    triangles[index] = source.Count - 1 - triangles[index];
            }
            return true;
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float first = Cross(b - a, point - a);
            float second = Cross(c - b, point - b);
            float third = Cross(a - c, point - c);
            return first >= -0.0001f && second >= -0.0001f && third >= -0.0001f;
        }

        private static float SignedArea(IReadOnlyList<Vector2> points)
        {
            float area = 0f;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 a = points[index];
                Vector2 b = points[(index + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float abC = Cross(b - a, c - a);
            float abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c);
            float cdB = Cross(d - c, b - c);
            return abC * abD < -0.0001f && cdA * cdB < -0.0001f;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
