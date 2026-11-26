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

        public Gear CurrentGear { get; private set; } = Gear.Drive;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.centerOfMass = _config.centerOfMass;
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

            var speedFactor = Mathf.Clamp01(SpeedKmh / _config.highSpeedReferenceKmh);
            var lockDeg = Mathf.Lerp(_config.maxSteerAngleDeg, _config.highSpeedSteerAngleDeg, speedFactor);
            var steerAngle = _input.Steer * lockDeg;
            _frontLeft.steerAngle = steerAngle;
            _frontRight.steerAngle = steerAngle;

            // Requests while moving are dropped, not queued.
            if (_input.TryConsumeGearRequest(out var requestedGear) && SpeedKmh < _config.gearChangeMaxSpeedKmh)
            {
                CurrentGear = requestedGear;
            }

            var motorTorque = _input.Throttle * _config.motorTorque;
            if (CurrentGear == Gear.Reverse)
            {
                motorTorque = SpeedKmh < _config.maxReverseSpeedKmh
                    ? -motorTorque * _config.reverseTorqueFactor
                    : 0f;
            }

            _frontLeft.motorTorque = motorTorque;
            _frontRight.motorTorque = motorTorque;
            _rearLeft.motorTorque = motorTorque;
            _rearRight.motorTorque = motorTorque;

            var brakeTorque = _input.Brake * _config.brakeTorque;
            if (_input.Throttle <= 0f)
            {
                // Without this the car coasts forever: WheelColliders have almost no rolling resistance.
                brakeTorque = Mathf.Max(brakeTorque, _config.engineBrakeTorque);
            }

            var velocity = _rigidbody.linearVelocity;
            _rigidbody.AddForce(-_config.airDragCoefficient * velocity.magnitude * velocity);

            _frontLeft.brakeTorque = brakeTorque;
            _frontRight.brakeTorque = brakeTorque;
            _rearLeft.brakeTorque = brakeTorque;
            _rearRight.brakeTorque = brakeTorque;
        }

        public float SpeedKmh => _rigidbody.linearVelocity.magnitude * 3.6f;
    }
}
