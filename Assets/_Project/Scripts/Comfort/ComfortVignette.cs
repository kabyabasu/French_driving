using UnityEngine;

namespace PermisVR.Comfort
{
    /// Darkens the edges of the view while the car turns, more for a faster turn, to cut the
    /// peripheral rotation that triggers motion sickness. Driven by the car's yaw rate only, so
    /// looking around (head turns) never triggers it. A ring mesh in front of the camera covers
    /// only the periphery (cheap); its renderer is off whenever the vignette is fully open.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ComfortVignette : MonoBehaviour
    {
        private const float DistanceM = 0.3f;
        private const float InnerAngleDeg = 20f;
        private const float OuterAngleDeg = 85f;
        private const int Segments = 48;
        private const int Rings = 7;

        private static readonly int ApertureId = Shader.PropertyToID("_ApertureDeg");
        private static readonly int FeatherId = Shader.PropertyToID("_FeatherDeg");

        [SerializeField] private ComfortConfig _config;
        [SerializeField] private Rigidbody _car;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private float _strength;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();
            GetComponent<MeshFilter>().sharedMesh = BuildRing();
            _renderer.enabled = false;
        }

        private void LateUpdate()
        {
            var target = 0f;
            if (_config.vignetteEnabled && _car != null)
            {
                var yawRateDegPerS = Mathf.Abs(Vector3.Dot(_car.angularVelocity, Vector3.up)) * Mathf.Rad2Deg;
                target = Mathf.InverseLerp(_config.yawRateStartDegPerS, _config.yawRateFullDegPerS, yawRateDegPerS);
            }

            var rate = target > _strength ? 1f / Mathf.Max(_config.fadeInS, 0.01f) : 1f / Mathf.Max(_config.fadeOutS, 0.01f);
            _strength = Mathf.MoveTowards(_strength, target, rate * Time.deltaTime);

            var visible = _strength > 0.001f;
            if (_renderer.enabled != visible)
            {
                _renderer.enabled = visible;
            }

            if (!visible)
            {
                return;
            }

            var eased = _strength * _strength * (3f - 2f * _strength);
            _block.SetFloat(ApertureId, Mathf.Lerp(_config.openApertureDeg, _config.narrowApertureDeg, eased));
            _block.SetFloat(FeatherId, _config.featherDeg);
            _renderer.SetPropertyBlock(_block);
        }

        /// Rings of vertices on a sphere around the camera's forward axis; uv.x = angle from forward (degrees).
        private static Mesh BuildRing()
        {
            var vertices = new Vector3[Segments * Rings];
            var uvs = new Vector2[vertices.Length];
            for (var r = 0; r < Rings; r++)
            {
                var angle = Mathf.Lerp(InnerAngleDeg, OuterAngleDeg, r / (float)(Rings - 1));
                var theta = angle * Mathf.Deg2Rad;
                for (var s = 0; s < Segments; s++)
                {
                    var phi = s * Mathf.PI * 2f / Segments;
                    vertices[r * Segments + s] = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Sin(theta) * Mathf.Sin(phi),
                        Mathf.Cos(theta)) * DistanceM;
                    uvs[r * Segments + s] = new Vector2(angle, 0f);
                }
            }

            var triangles = new int[(Rings - 1) * Segments * 6];
            var t = 0;
            for (var r = 0; r < Rings - 1; r++)
            {
                for (var s = 0; s < Segments; s++)
                {
                    var a = r * Segments + s;
                    var b = r * Segments + (s + 1) % Segments;
                    var c = a + Segments;
                    var d = b + Segments;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            }

            var mesh = new Mesh { name = "ComfortVignetteRing", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f); // never culled
            return mesh;
        }
    }
}
