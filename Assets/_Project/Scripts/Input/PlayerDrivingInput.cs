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

        public float Steer => _wheel != null ? _wheel.Steer : 0f;

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
        }

        private void Update()
        {
            if (_wheel != null)
            {
                _wheel.SetCarSpeedKmh(_car.SpeedKmh);
            }
        }
    }
}
