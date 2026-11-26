using System.Collections.Generic;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// 2D helpers on the XZ plane for turning OSM polylines/polygons into meshes.
    internal static class MapGeometry
    {
        private const float MaxMiterScale = 2f;
        private const float Epsilon = 1e-6f;

        /// Clockwise perpendicular seen from above: forward +Z gives right +X.
        public static Vector3 Right(Vector3 forward) => new(forward.z, 0f, -forward.x);

        /// Each point pushed sideways by offset (negative = left), mitred at corners.
        public static Vector3[] Offset(IReadOnlyList<Vector3> points, float offsetM)
        {
            var result = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var rightIn = i > 0 ? SegmentRight(points[i - 1], points[i]) : Vector3.zero;
                var rightOut = i < points.Count - 1 ? SegmentRight(points[i], points[i + 1]) : Vector3.zero;
                var reference = rightOut != Vector3.zero ? rightOut : rightIn;

                var right = rightIn + rightOut;
                if (right.sqrMagnitude < Epsilon)
                {
                    right = reference;
                }

                right.Normalize();
                var dot = Vector3.Dot(right, reference);
                var scale = dot > 0.1f ? Mathf.Min(1f / dot, MaxMiterScale) : 1f;
                result[i] = points[i] + right * (offsetM * scale);
            }

            return result;
        }

        private static Vector3 SegmentRight(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude < Epsilon ? Vector3.zero : Right(direction.normalized);
        }

        public static float Length(IReadOnlyList<Vector3> points)
        {
            var total = 0f;
            for (var i = 0; i < points.Count - 1; i++)
            {
                total += Vector3.Distance(points[i], points[i + 1]);
            }

            return total;
        }

        /// Point at a distance along the polyline (clamped to its ends).
        public static Vector3 PointAt(IReadOnlyList<Vector3> points, float distanceM)
        {
            var walked = 0f;
            for (var i = 0; i < points.Count - 1; i++)
            {
                var segment = Vector3.Distance(points[i], points[i + 1]);
                if (walked + segment >= distanceM && segment > Epsilon)
                {
                    return Vector3.Lerp(points[i], points[i + 1], (distanceM - walked) / segment);
                }

                walked += segment;
            }

            return points[points.Count - 1];
        }

        /// Polyline with startM/endM cut off its ends, or null if nothing useful is left.
        public static List<Vector3> Trim(IReadOnlyList<Vector3> points, float startM, float endM, float minLengthM)
        {
            var total = Length(points);
            var from = startM;
            var to = total - endM;
            if (to - from < minLengthM)
            {
                return null;
            }

            var result = new List<Vector3>();
            var walked = 0f;
            for (var i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var segment = Vector3.Distance(a, b);
                var segmentEnd = walked + segment;
                if (segment > Epsilon && segmentEnd >= from && walked <= to)
                {
                    if (result.Count == 0)
                    {
                        result.Add(Vector3.Lerp(a, b, Mathf.Clamp01((from - walked) / segment)));
                    }

                    result.Add(Vector3.Lerp(a, b, Mathf.Clamp01((to - walked) / segment)));
                }

                walked = segmentEnd;
            }

            return result.Count >= 2 ? result : null;
        }

        /// Positive when the ring runs counterclockwise seen from above (+Z north up).
        public static float SignedArea(IReadOnlyList<Vector2> ring)
        {
            var area = 0f;
            for (var i = 0; i < ring.Count; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Count];
                area += a.x * b.y - b.x * a.y;
            }

            return area * 0.5f;
        }

        /// Ear-clipping triangulation; returns index triples into ring. Falls back to a fan
        /// for self-intersecting footprints, which OSM occasionally contains.
        public static List<int> Triangulate(IReadOnlyList<Vector2> ring)
        {
            var triangles = new List<int>();
            var count = ring.Count;
            if (count < 3)
            {
                return triangles;
            }

            var counterclockwise = SignedArea(ring) > 0f;
            var remaining = new List<int>(count);
            for (var i = 0; i < count; i++)
            {
                remaining.Add(counterclockwise ? i : count - 1 - i);
            }

            while (remaining.Count > 3)
            {
                var clipped = false;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var ia = remaining[(i + remaining.Count - 1) % remaining.Count];
                    var ib = remaining[i];
                    var ic = remaining[(i + 1) % remaining.Count];
                    if (!IsEar(ring, remaining, ia, ib, ic))
                    {
                        continue;
                    }

                    triangles.Add(ia);
                    triangles.Add(ib);
                    triangles.Add(ic);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                {
                    break;
                }
            }

            for (var i = 1; i < remaining.Count - 1; i++)
            {
                triangles.Add(remaining[0]);
                triangles.Add(remaining[i]);
                triangles.Add(remaining[i + 1]);
            }

            return triangles;
        }

        private static bool IsEar(IReadOnlyList<Vector2> ring, List<int> remaining, int ia, int ib, int ic)
        {
            var a = ring[ia];
            var b = ring[ib];
            var c = ring[ic];
            if (Cross(b - a, c - b) <= 0f)
            {
                return false;
            }

            foreach (var ip in remaining)
            {
                if (ip != ia && ip != ib && ip != ic && InTriangle(ring[ip], a, b, c))
                {
                    return false;
                }
            }

            return true;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Cross(b - a, p - a);
            var d2 = Cross(c - b, p - b);
            var d3 = Cross(a - c, p - c);
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }
    }
}
