using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PermisVR.Editor.Map
{
    /// Accumulates flat-shaded triangles for one merged tile mesh. Winding is derived from
    /// the requested facing direction, so callers never have to get vertex order right.
    internal sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<int> _triangles = new();

        public bool IsEmpty => _triangles.Count == 0;

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                (b, c) = (c, b);
            }

            var start = _vertices.Count;
            var normal = facing.normalized;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _normals.Add(normal);
            _normals.Add(normal);
            _normals.Add(normal);
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
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _vertices.Add(d);
            for (var i = 0; i < 4; i++)
            {
                _normals.Add(normal);
            }

            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
            _triangles.Add(start);
            _triangles.Add(start + 2);
            _triangles.Add(start + 3);
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
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
