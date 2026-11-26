using UnityEngine;

namespace PermisVR.Vehicle
{
    /// One car mirror: a low-res camera renders what the mirror would reflect onto the glass,
    /// flipped left-right. Mono (not stereo) like most VR car mirrors; redraws every Nth frame.
    public sealed class RearViewMirror : MonoBehaviour
    {
        [SerializeField] private MirrorConfig _config;
        [SerializeField] private Camera _camera;
        [SerializeField] private Renderer _glass;
        [SerializeField] private int _textureWidth = 256;
        [SerializeField] private int _textureHeight = 96;
        [SerializeField] private float _verticalFovDeg = 20f;
        [Tooltip("Staggers mirrors so they don't all redraw on the same frame.")]
        [SerializeField] private int _frameOffset;

        private RenderTexture _texture;
        private Material _material;

        private void Awake()
        {
            _texture = new RenderTexture(_textureWidth, _textureHeight, 16) { name = name + "_Reflection" };
            _texture.Create();

            _camera.targetTexture = _texture;
            _camera.stereoTargetEye = StereoTargetEyeMask.None;
            _camera.fieldOfView = _verticalFovDeg;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = _config.farClipM;

            // Instance of the saved glass material (keeps its shader in the build), mirrored horizontally.
            _material = new Material(_glass.sharedMaterial)
            {
                mainTexture = _texture,
                mainTextureScale = new Vector2(-1f, 1f),
                mainTextureOffset = new Vector2(1f, 0f),
            };
            _glass.sharedMaterial = _material;
        }

        private void LateUpdate()
        {
            _camera.enabled = _config.renderMirrors
                && (Time.frameCount + _frameOffset) % _config.renderEveryNFrames == 0;
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
