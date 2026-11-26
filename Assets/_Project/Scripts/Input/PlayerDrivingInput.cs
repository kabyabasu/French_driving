using PermisVR.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PermisVR.Input
{
    /// Bridges the Driving action map + VirtualSteeringWheel into the IDrivingInput
    /// contract Vehicle/CarController reads. This is the only place that knows about
    /// both the Input System and the car.
    public sealed class PlayerDrivingInput : MonoBehaviour, IDrivingInput
    {
        [SerializeField] private CarController _car;
        [SerializeField] private VirtualSteeringWheel _wheel;
        [SerializeField] private InputActionReference _throttle;
        [SerializeField] private InputActionReference _brake;
        [SerializeField] private TriggerResponseConfig _triggerResponse;

        private const string GearDriveAction = "GearDrive";
        private const string GearReverseAction = "GearReverse";

        private InputAction _gearDrive;
        private InputAction _gearReverse;
        private Gear? _pendingGear;

        public float Steer => _wheel != null ? _wheel.Steer : 0f;

        public bool TryConsumeGearRequest(out Gear gear)
        {
            gear = _pendingGear.GetValueOrDefault();
            if (!_pendingGear.HasValue)
            {
                return false;
            }

            _pendingGear = null;
            return true;
        }

        public float Throttle => ApplyTriggerResponse(_throttle);
        public float Brake => ApplyTriggerResponse(_brake);

        private float ApplyTriggerResponse(InputActionReference action)
        {
            if (action == null)
            {
                return 0f;
            }

            var raw = action.action.ReadValue<float>();
            return _triggerResponse != null ? _triggerResponse.Apply(raw) : raw;
        }

        private void Awake()
        {
            _car.SetInputSource(this);

            // Same Driving map as the throttle; looked up by name so no extra scene wiring.
            var map = _throttle != null ? _throttle.action.actionMap : null;
            _gearDrive = map?.FindAction(GearDriveAction);
            _gearReverse = map?.FindAction(GearReverseAction);
        }

        // Actions from an .inputactions asset start disabled and read 0 until enabled. This
        // enables the whole Driving map, which the wheel grips and the hand TrackedPoseDrivers
        // also read from.
        private void OnEnable()
        {
            if (_throttle != null)
            {
                _throttle.action.actionMap.Enable();
            }
        }

        private void OnDisable()
        {
            if (_throttle != null)
            {
                _throttle.action.actionMap.Disable();
            }
        }

        private void Update()
        {
            // Latched here because presses are per-frame but the car reads in FixedUpdate.
            if (_gearDrive != null && _gearDrive.WasPressedThisFrame())
            {
                _pendingGear = Gear.Drive;
            }
            else if (_gearReverse != null && _gearReverse.WasPressedThisFrame())
            {
                _pendingGear = Gear.Reverse;
            }

            if (_wheel != null)
            {
                _wheel.SetCarSpeedKmh(_car.SpeedKmh);
            }
        }
    }
}
