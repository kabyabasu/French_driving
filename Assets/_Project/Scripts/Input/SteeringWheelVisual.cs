using UnityEngine;

namespace PermisVR.Input
{
    /// Rotates a placeholder wheel mesh to match VirtualSteeringWheel's current angle,
    /// around the parent's local Z (the axis pointing from the seat through the wheel).
    public sealed class SteeringWheelVisual : MonoBehaviour
    {
        [SerializeField] private VirtualSteeringWheel _wheel;
        [SerializeField] private Transform _mesh;

        private Quaternion _restRotation;

        private void Awake()
        {
            _restRotation = _mesh.localRotation;
        }

        private void LateUpdate()
        {
            // Rotate in the parent's space, not the mesh's: the disc is a cylinder tipped 90°
            // on X, so its own Z axis points down and spinning about it flips it like a coin.
            _mesh.localRotation = Quaternion.AngleAxis(-_wheel.WheelAngleDeg, Vector3.forward) * _restRotation;
        }
    }
}
