using UnityEngine;
using UnityEngine.InputSystem;

namespace PermisVR.Input
{
    /// Steering wheel turned by one or both hands. Grabbing near the rim works two ways:
    /// a quick tap of the grip latches the hand on until the next tap (no need to hold the button
    /// while cruising); squeezing and holding grips only while squeezed, so letting go of the
    /// button lets go of the wheel (quick hand-over-hand). A hand also lets go if it moves far
    /// away. While held, the hands' average frame-to-frame angle change around the wheel turns it.
    /// Released, it self-centres like a real car: not at all when stopped, gently at manoeuvring
    /// speed, firmly at road speed.
    public sealed class VirtualSteeringWheel : MonoBehaviour
    {
        [SerializeField] private SteeringWheelConfig _config;
        [SerializeField] private Transform _wheelCenter;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private InputActionReference _leftGrip;
        [SerializeField] private InputActionReference _rightGrip;

        private float _wheelAngleDeg;
        private float _carSpeedKmh;
        private HandState _left;
        private HandState _right;

        // Grip is an analogue axis: press above PressOn, re-arm below PressOff (no flicker).
        private const float PressOn = 0.6f;
        private const float PressOff = 0.3f;

        private struct HandState
        {
            public bool Held;
            public bool GripDown;
            /// Held by a quick tap: stays on after the grip is released, until the next press.
            public bool Latched;
            public float PressStartS;
            /// Where on the rim the hand holds, in the wheel's own (rotating) frame, degrees.
            public float GrabAngleOnWheelDeg;
            public float? PrevAngleDeg;
        }

        public float Steer => Mathf.Clamp(_wheelAngleDeg / _config.maxWheelAngleDeg, -1f, 1f);

        /// Raw wheel rotation in degrees, positive = clockwise (turning right) as seen from the seat.
        public float WheelAngleDeg => _wheelAngleDeg;

        public bool IsHeld(bool left) => left ? _left.Held : _right.Held;

        /// Grip button currently squeezed (for showing an open or closed free hand).
        public bool IsGripDown(bool left) => left ? _left.GripDown : _right.GripDown;

        /// Fed externally each frame (e.g. by PlayerDrivingInput) so this script never
        /// needs to reference Vehicle/ directly.
        public void SetCarSpeedKmh(float speedKmh)
        {
            _carSpeedKmh = speedKmh;
        }

        /// Pose for drawing a held hand on the rim at the point it grabbed, turning with the wheel.
        /// Palm faces the wheel centre, fingers point forward (away from the driver), thumb side
        /// along the rim. Returns false if that hand isn't holding the wheel.
        public bool TryGetHeldHandPose(bool left, out Vector3 position, out Quaternion rotation)
        {
            var state = left ? _left : _right;
            if (!state.Held)
            {
                position = default;
                rotation = default;
                return false;
            }

            var angleRad = (state.GrabAngleOnWheelDeg - _wheelAngleDeg) * Mathf.Deg2Rad;
            var radial = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);
            var tangent = new Vector3(-radial.y, radial.x, 0f);
            // Hand sits just outside the rim tube, slightly towards the driver, wrapping around it.
            var local = radial * (_config.rimRadiusM + 0.03f) - Vector3.forward * 0.02f;
            position = _wheelCenter.TransformPoint(local);
            var up = _wheelCenter.TransformDirection(radial);              // back of the hand
            var right = _wheelCenter.TransformDirection(-tangent);
            rotation = Quaternion.LookRotation(Vector3.Cross(right, up), up);
            return true;
        }

        private void Update()
        {
            UpdateGrab(_leftGrip, _leftHand, ref _left);
            UpdateGrab(_rightGrip, _rightHand, ref _right);

            // Average, not sum: two hands turning the wheel 10° together turn it 10°, not 20°.
            var deltaSum = 0f;
            var contributing = 0;
            AccumulateDelta(_leftHand, ref _left, ref deltaSum, ref contributing);
            AccumulateDelta(_rightHand, ref _right, ref deltaSum, ref contributing);
            if (contributing > 0)
            {
                // AngleAroundWheel grows counterclockwise (seen from the seat); the wheel angle
                // grows clockwise so that Steer is +1 for a right turn.
                _wheelAngleDeg -= deltaSum / contributing;
            }

            if (!_left.Held && !_right.Held)
            {
                SelfCenter(Time.deltaTime);
            }

            _wheelAngleDeg = Mathf.Clamp(_wheelAngleDeg, -_config.maxWheelAngleDeg, _config.maxWheelAngleDeg);
        }

        private void UpdateGrab(InputActionReference grip, Transform hand, ref HandState state)
        {
            if (hand == null)
            {
                state.Held = false;
                return;
            }

            var gripValue = grip != null ? grip.action.ReadValue<float>() : 0f;
            var pressed = false;
            var releasedButton = false;
            if (!state.GripDown && gripValue >= PressOn)
            {
                state.GripDown = true;
                pressed = true;
            }
            else if (state.GripDown && gripValue <= PressOff)
            {
                state.GripDown = false;
                releasedButton = true;
            }

            var distance = DistanceFromRim(hand.position);
            if (state.Held && distance > _config.releaseDistanceM)
            {
                LetGo(ref state);
            }
            else if (state.Held && state.Latched && pressed)
            {
                LetGo(ref state); // second tap
            }
            else if (state.Held && !state.Latched && releasedButton)
            {
                // Short press = tap: stay on. Long squeeze = held grip: letting go of the button lets go.
                if (Time.time - state.PressStartS <= _config.tapMaxS)
                {
                    state.Latched = true;
                }
                else
                {
                    LetGo(ref state);
                }
            }
            else if (!state.Held && pressed && distance <= _config.grabDistanceM)
            {
                state.Held = true;
                state.Latched = false;
                state.PressStartS = Time.time;
                state.GrabAngleOnWheelDeg = AngleAroundWheel(hand.position) + _wheelAngleDeg;
                state.PrevAngleDeg = null;
            }
        }

        private static void LetGo(ref HandState state)
        {
            state.Held = false;
            state.Latched = false;
            state.PrevAngleDeg = null;
        }

        private void AccumulateDelta(Transform hand, ref HandState state, ref float deltaSum, ref int contributing)
        {
            if (!state.Held)
            {
                return;
            }

            var local = _wheelCenter.InverseTransformPoint(hand.position);
            if (new Vector2(local.x, local.y).magnitude < _config.minHandRadiusM)
            {
                state.PrevAngleDeg = null;
                return;
            }

            var angleDeg = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            if (state.PrevAngleDeg.HasValue)
            {
                deltaSum += Mathf.DeltaAngle(state.PrevAngleDeg.Value, angleDeg);
                contributing++;
            }

            state.PrevAngleDeg = angleDeg;
        }

        private float DistanceFromRim(Vector3 worldPosition)
        {
            var local = _wheelCenter.InverseTransformPoint(worldPosition);
            var radial = new Vector2(local.x, local.y).magnitude - _config.rimRadiusM;
            return Mathf.Sqrt(radial * radial + local.z * local.z);
        }

        private float AngleAroundWheel(Vector3 worldPosition)
        {
            var local = _wheelCenter.InverseTransformPoint(worldPosition);
            // Wheel plane is the local XY plane; local Z is the wheel's forward (rotation) axis.
            return Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        }

        private void SelfCenter(float deltaTimeS)
        {
            if (Mathf.Approximately(_wheelAngleDeg, 0f))
            {
                return;
            }

            // Real self-aligning torque comes from the tyres rolling: none when stopped, weak while
            // manoeuvring (so a U-turn isn't lost between hand changes), full by road speed.
            var ramp = Mathf.Clamp01((_carSpeedKmh - _config.centerStartSpeedKmh)
                                     / Mathf.Max(_config.centerFullSpeedKmh - _config.centerStartSpeedKmh, 0.01f));
            var speedMultiplier = 1f + _carSpeedKmh / Mathf.Max(_config.centerRateReferenceSpeedKmh, 0.01f);
            var step = _config.centerRateDegPerSec * speedMultiplier * ramp * ramp * deltaTimeS;
            if (step <= 0f)
            {
                return;
            }
            _wheelAngleDeg = Mathf.MoveTowards(_wheelAngleDeg, 0f, step);
        }
    }
}
