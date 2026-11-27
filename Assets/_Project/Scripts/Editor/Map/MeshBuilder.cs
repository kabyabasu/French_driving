using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PermisVR.Editor.Map
{
    /// Accumulates flat-shaded triangles for one merged tile mesh. Winding is derived from
    /// the requested facing direction, so callers never have to get vertex order right.
    /// UVs are world-space metres (top-down for floors, along-the-face + height for walls), so a
    /// material's texture scale is simply 1 / (metres per texture repeat) and tiles join seamlessly.
    internal sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<int> _triangles = new();
        private readonly List<Color32> _colors = new();
        private bool _hasColors;

        public bool IsEmpty => _triangles.Count == 0;

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                (b, c) = (c, b);
            }

            var start = _vertices.Count;
            var normal = facing.normalized;
            AddVertex(a, normal);
            AddVertex(b, normal);
            AddVertex(c, normal);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
        }

        /// a-b-c-d must go around the quad's edge (either direction).
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                (b, d) = (d, b);
            }

            var start = _vertices.Count;
            var normal = facing.normalized;
            AddVertex(a, normal);
            AddVertex(b, normal);
            AddVertex(c, normal);
            AddVertex(d, normal);

            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
            _triangles.Add(start);
            _triangles.Add(start + 2);
            _triangles.Add(start + 3);
        }

        private void AddVertex(Vector3 position, Vector3 normal)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            _uvs.Add(WorldUv(position, normal));
            _colors.Add(new Color32(255, 255, 255, 255));
        }

        /// Quad with explicit UVs and a vertex colour (per-face data for custom shaders).
        /// a-b-c-d go around the quad's edge; uvs follow the same order.
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD,
            Vector3 facing, Color32 color)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                (b, d) = (d, b);
                (uvB, uvD) = (uvD, uvB);
            }

            var start = _vertices.Count;
            var normal = facing.normalized;
            AddVertex(a, normal, uvA, color);
            AddVertex(b, normal, uvB, color);
            AddVertex(c, normal, uvC, color);
            AddVertex(d, normal, uvD, color);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
            _triangles.Add(start);
            _triangles.Add(start + 2);
            _triangles.Add(start + 3);
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector3 facing, Color32 color)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                (b, c) = (c, b);
                (uvB, uvC) = (uvC, uvB);
            }

            var start = _vertices.Count;
            var normal = facing.normalized;
            AddVertex(a, normal, uvA, color);
            AddVertex(b, normal, uvB, color);
            AddVertex(c, normal, uvC, color);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
        }

        private void AddVertex(Vector3 position, Vector3 normal, Vector2 uv, Color32 color)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            _uvs.Add(uv);
            _colors.Add(color);
            _hasColors = true;
        }

        private static Vector2 WorldUv(Vector3 position, Vector3 normal)
        {
            if (Mathf.Abs(normal.y) > 0.7f)
            {
                return new Vector2(position.x, position.z);
            }

            // Horizontal direction along the face; v = height above ground.
            var along = new Vector3(-normal.z, 0f, normal.x).normalized;
            return new Vector2(Vector3.Dot(position, along), position.y);
        }

        /// Flat rectangle in the XZ plane at height y.
        public void AddRect(Vector3 center, Vector3 along, Vector3 across, float halfAlongM, float halfAcrossM, float y)
        {
            center.y = y;
            var a = along * halfAlongM;
            var c = across * halfAcrossM;
            AddQuad(center - a - c, center + a - c, center + a + c, center - a + c, Vector3.up);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = _vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            if (_hasColors)
            {
                mesh.SetColors(_colors);
            }
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
