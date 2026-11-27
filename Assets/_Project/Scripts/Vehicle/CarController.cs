using UnityEngine;

namespace PermisVR.Vehicle
{
    /// Drives the car from WheelColliders (front-wheel drive, like a small hatchback). Reads only
    /// IDrivingInput, never a device directly. Push is force- then power-limited, so it accelerates
    /// like a real small car instead of a fixed torque that keeps pulling at any speed.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarController : MonoBehaviour
    {
        [SerializeField] private VehicleConfig _config;

        [Header("Front (steered, driven)")]
        [SerializeField] private WheelCollider _frontLeft;
        [SerializeField] private WheelCollider _frontRight;

        [Header("Rear (braked only)")]
        [SerializeField] private WheelCollider _rearLeft;
        [SerializeField] private WheelCollider _rearRight;

        private Rigidbody _rigidbody;
        private IDrivingInput _input;
        private float _engineThrottle;

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

            // Engine response lags the pedal going up; lifting off is immediate.
            var pedal = Mathf.Clamp01(_input.Throttle);
            _engineThrottle = pedal > _engineThrottle
                ? Mathf.MoveTowards(_engineThrottle, pedal, Time.fixedDeltaTime / Mathf.Max(_config.throttleRiseS, 0.01f))
                : pedal;

            var speedMs = _rigidbody.linearVelocity.magnitude;
            var pushN = Mathf.Min(_config.maxTractiveForceN, _config.maxPowerKw * 1000f / Mathf.Max(speedMs, 0.5f));
            // Split across the two driven (front) wheels.
            var motorTorque = _engineThrottle * pushN * _frontLeft.radius * 0.5f;
            if (CurrentGear == Gear.Reverse)
            {
                motorTorque = SpeedKmh < _config.maxReverseSpeedKmh
                    ? -motorTorque * _config.reverseTorqueFactor
                    : 0f;
            }

            _frontLeft.motorTorque = motorTorque;
            _frontRight.motorTorque = motorTorque;
            _rearLeft.motorTorque = 0f;
            _rearRight.motorTorque = 0f;

            var brakeTorque = _input.Brake * _config.brakeTorque;
            if (_input.Throttle <= 0f)
            {
                // Without this the car coasts forever: WheelColliders have almost no rolling resistance.
                brakeTorque = Mathf.Max(brakeTorque, _config.engineBrakeTorque);
            }

            var velocity = _rigidbody.linearVelocity;
            _rigidbody.AddForce(-_config.airDragCoefficient * velocity.magnitude * velocity);

            // brakeTorque is the per-wheel average; the bias shifts it towards the front axle.
            var front = brakeTorque * 2f * _config.brakeFrontBias;
            var rear = brakeTorque * 2f * (1f - _config.brakeFrontBias);
            _frontLeft.brakeTorque = front;
            _frontRight.brakeTorque = front;
            _rearLeft.brakeTorque = rear;
            _rearRight.brakeTorque = rear;
        }

        public float SpeedKmh => _rigidbody.linearVelocity.magnitude * 3.6f;
    }
}
