using PermisVR.Editor.Map;
using UnityEngine;

namespace PermisVR.Editor.Car
{
    /// Low-poly solids for the procedural car body, interior, steering wheel and hands, added to a
    /// MeshBuilder (flat shaded; winding comes from each face's outward direction).
    internal static class CarMeshKit
    {
        /// Six-faced solid from 8 corners: bottom 0-3 then top 4-7, each ring in the same order
        /// (e.g. -x-z, +x-z, +x+z, -x+z). Corners may be skewed for slopes; it must stay convex.
        public static void Hexa(MeshBuilder builder, Vector3[] c)
        {
            var center = Vector3.zero;
            foreach (var corner in c)
            {
                center += corner;
            }

            center /= 8f;
            Face(builder, center, c[0], c[1], c[2], c[3]);
            Face(builder, center, c[4], c[5], c[6], c[7]);
            Face(builder, center, c[0], c[1], c[5], c[4]);
            Face(builder, center, c[1], c[2], c[6], c[5]);
            Face(builder, center, c[2], c[3], c[7], c[6]);
            Face(builder, center, c[3], c[0], c[4], c[7]);
        }

        private static void Face(MeshBuilder builder, Vector3 center, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var outward = (a + b + c + d) * 0.25f - center;
            builder.AddQuad(a, b, c, d, outward);
        }

        /// Axis-aligned box between two corners.
        public static void Box(MeshBuilder builder, Vector3 min, Vector3 max)
        {
            Hexa(builder, new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
            });
        }

        /// Box of `size` (x width, y height, z length) centred at `center`, rotated; optionally
        /// mirrored across the YZ plane (x -> -x) for left/right copies.
        public static void OrientedBox(MeshBuilder builder, Vector3 center, Quaternion rotation, Vector3 size, bool mirrorX = false)
        {
            var h = size * 0.5f;
            var corners = new Vector3[8];
            var signs = new[]
            {
                new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, -1, 1), new Vector3(-1, -1, 1),
                new Vector3(-1, 1, -1), new Vector3(1, 1, -1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1),
            };
            for (var i = 0; i < 8; i++)
            {
                var p = center + rotation * Vector3.Scale(signs[i], h);
                corners[i] = mirrorX ? new Vector3(-p.x, p.y, p.z) : p;
            }

            Hexa(builder, corners);
        }

        /// Closed n-sided cylinder along `axis` (unit), from `start` for `length` metres.
        public static void Prism(MeshBuilder builder, Vector3 start, Vector3 axis, float radius, float length, int sides)
        {
            axis.Normalize();
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var other = Vector3.Cross(axis, side);
            var end = start + axis * length;
            for (var k = 0; k < sides; k++)
            {
                var a0 = k * Mathf.PI * 2f / sides;
                var a1 = (k + 1) * Mathf.PI * 2f / sides;
                var r0 = (side * Mathf.Cos(a0) + other * Mathf.Sin(a0)) * radius;
                var r1 = (side * Mathf.Cos(a1) + other * Mathf.Sin(a1)) * radius;
                builder.AddQuad(start + r0, start + r1, end + r1, end + r0, r0 + r1);
                builder.AddTriangle(start, start + r0, start + r1, -axis);
                builder.AddTriangle(end, end + r1, end + r0, axis);
            }
        }

        /// Ring (torus) around `axis` through `center`: majorRadius to the tube centre, tube minorRadius.
        public static void Torus(MeshBuilder builder, Vector3 center, Vector3 axis, float majorRadius, float minorRadius,
            int majorSegments, int minorSegments)
        {
            axis.Normalize();
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var v = Vector3.Cross(axis, u);
            for (var i = 0; i < majorSegments; i++)
            {
                var t0 = i * Mathf.PI * 2f / majorSegments;
                var t1 = (i + 1) * Mathf.PI * 2f / majorSegments;
                var d0 = u * Mathf.Cos(t0) + v * Mathf.Sin(t0);
                var d1 = u * Mathf.Cos(t1) + v * Mathf.Sin(t1);
                var c0 = center + d0 * majorRadius;
                var c1 = center + d1 * majorRadius;
                for (var j = 0; j < minorSegments; j++)
                {
                    var p0 = j * Mathf.PI * 2f / minorSegments;
                    var p1 = (j + 1) * Mathf.PI * 2f / minorSegments;
                    var n00 = d0 * Mathf.Cos(p0) + axis * Mathf.Sin(p0);
                    var n01 = d0 * Mathf.Cos(p1) + axis * Mathf.Sin(p1);
                    var n10 = d1 * Mathf.Cos(p0) + axis * Mathf.Sin(p0);
                    var n11 = d1 * Mathf.Cos(p1) + axis * Mathf.Sin(p1);
                    builder.AddQuad(c0 + n00 * minorRadius, c1 + n10 * minorRadius, c1 + n11 * minorRadius,
                        c0 + n01 * minorRadius, n00 + n01 + n10 + n11);
                }
            }
        }

        /// Mirror of a point across the car's centre line (x -> -x).
        public static Vector3 MirrorX(Vector3 p) => new(-p.x, p.y, p.z);
    }
}
