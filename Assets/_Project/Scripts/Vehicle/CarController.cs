using UnityEngine;

namespace PermisVR.Vehicle
{
    /// Drives the car from WheelColliders. Reads only IDrivingInput, never a device directly.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarController : MonoBehaviour
    {
        [SerializeField] private VehicleConfig _config;

        [Header("Front (steered, driven)")]
        [SerializeField] private WheelCollider _frontLeft;
        [SerializeField] private WheelCollider _frontRight;

        [Header("Rear (driven)")]
        [SerializeField] private WheelCollider _rearLeft;
        [SerializeField] private WheelCollider _rearRight;

        private Rigidbody _rigidbody;
        private IDrivingInput _input;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.interpolation = _config.interpolate
                ? RigidbodyInterpolation.Interpolate
                : RigidbodyInterpolation.None;
        }

        /// Set once at spawn time by whatever owns the input source (Input/PlayerDrivingInput).
        public void SetInputSource(IDrivingInput input)
        {
            _input = input;
        }

        private void FixedUpdate()
        {
            if (_input == null)
            {
                return;
            }

            var steerAngle = _input.Steer * _config.maxSteerAngleDeg;
            _frontLeft.steerAngle = steerAngle;
            _frontRight.steerAngle = steerAngle;

            var motorTorque = _input.Throttle * _config.motorTorque;
            _frontLeft.motorTorque = motorTorque;
            _frontRight.motorTorque = motorTorque;
            _rearLeft.motorTorque = motorTorque;
            _rearRight.motorTorque = motorTorque;

            var brakeTorque = _input.Brake * _config.brakeTorque;
            _frontLeft.brakeTorque = brakeTorque;
            _frontRight.brakeTorque = brakeTorque;
            _rearLeft.brakeTorque = brakeTorque;
            _rearRight.brakeTorque = brakeTorque;
        }

        // Unity 6+ renamed Rigidbody.velocity to linearVelocity; swap this once the
        // Unity version in CLAUDE.md is pinned, if it's 6.0 or later.
        public float SpeedKmh => _rigidbody.velocity.magnitude * 3.6f;
    }
}
