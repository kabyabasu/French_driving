using UnityEngine;

namespace PermisVR.Vehicle
{
    /// Syncs a placeholder wheel mesh's pose to its WheelCollider each frame (steer + spin).
    [RequireComponent(typeof(WheelCollider))]
    public sealed class WheelVisual : MonoBehaviour
    {
        [SerializeField] private Transform _mesh;

        private WheelCollider _wheelCollider;
        private Quaternion _meshRotationOffset;

        private void Awake()
        {
            _wheelCollider = GetComponent<WheelCollider>();
            // Keeps the mesh's authored orientation (e.g. a cylinder tipped onto its side);
            // applying the collider pose alone would lay the wheel flat like a coin.
            _meshRotationOffset = _mesh.localRotation;
        }

        private void Update()
        {
            _wheelCollider.GetWorldPose(out var position, out var rotation);
            _mesh.SetPositionAndRotation(position, rotation * _meshRotationOffset);
        }
    }
}
