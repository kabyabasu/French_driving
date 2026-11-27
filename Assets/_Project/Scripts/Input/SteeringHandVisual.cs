using UnityEngine;

namespace PermisVR.Input
{
    /// Draws one hand: wrapped around the rim where it holds the wheel (turning with it), or
    /// following the controller when free: open when relaxed, a fist while the grip is squeezed.
    /// Swaps between two prebuilt meshes, so it is one draw call and no per-frame allocation.
    public sealed class SteeringHandVisual : MonoBehaviour
    {
        [SerializeField] private VirtualSteeringWheel _wheel;
        [SerializeField] private Transform _controller;
        [SerializeField] private bool _isLeft;
        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private Mesh _gripMesh;
        [SerializeField] private Mesh _openMesh;

        [Header("Free hand on the controller (tune if it looks off on the headset)")]
        [Tooltip("Hand rotation relative to the controller, degrees. Default maps the fist around the grip.")]
        [SerializeField] private Vector3 _controllerRotationOffsetDeg = new(90f, 0f, -90f);
        [Tooltip("Where the fist's centre sits relative to the controller, controller-local metres.")]
        [SerializeField] private Vector3 _controllerPositionOffsetM = Vector3.zero;

        // In the grip mesh the curled fingers wrap a rim tube centred here (hand-local).
        private static readonly Vector3 GripTubeCenter = new(0f, -0.03f, 0.02f);

        private Quaternion _rotationOffset;

        private void Awake()
        {
            // Left hand is the mirror image: flip the roll around the forward axis.
            var offset = _controllerRotationOffsetDeg;
            _rotationOffset = Quaternion.Euler(offset.x, _isLeft ? -offset.y : offset.y, _isLeft ? -offset.z : offset.z);
        }

        private void LateUpdate()
        {
            if (_wheel != null && _wheel.TryGetHeldHandPose(_isLeft, out var position, out var rotation))
            {
                transform.SetPositionAndRotation(position, rotation);
                SetMesh(_gripMesh);
                return;
            }

            if (_controller == null)
            {
                return;
            }

            var handRotation = _controller.rotation * _rotationOffset;
            var fistCenter = _controller.TransformPoint(_controllerPositionOffsetM);
            var gripping = _wheel != null && _wheel.IsGripDown(_isLeft);
            transform.SetPositionAndRotation(fistCenter - handRotation * GripTubeCenter, handRotation);
            SetMesh(gripping ? _gripMesh : _openMesh);
        }

        private void SetMesh(Mesh mesh)
        {
            if (_meshFilter.sharedMesh != mesh)
            {
                _meshFilter.sharedMesh = mesh;
            }
        }
    }
}
