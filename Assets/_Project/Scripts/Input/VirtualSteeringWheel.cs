using UnityEngine;
using UnityEngine.InputSystem;

namespace PermisVR.Input
{
    /// Grabbed with the grip buttons, one or two hands. Each gripping hand's frame-to-frame
    /// angle delta around the wheel is added to the wheel angle, so hand-over-hand turning
    /// past 180 degrees works. Self-centers when released, faster at higher car speed.
    public sealed class VirtualSteeringWheel : MonoBehaviour
    {
        [SerializeField] private SteeringWheelConfig _config;
        [SerializeField] private Transform _wheelCenter;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private InputActionReference _leftGrip;
        [SerializeField] private InputActionReference _rightGrip;

        private float _wheelAngleDeg;
        private float? _leftHandAngleDeg;
        private float? _rightHandAngleDeg;
        private float _carSpeedKmh;

        public float Steer => Mathf.Clamp(_wheelAngleDeg / _config.maxWheelAngleDeg, -1f, 1f);

        /// Fed externally each frame (e.g. by PlayerDrivingInput) so this script never
        /// needs to reference Vehicle/ directly.
        public void SetCarSpeedKmh(float speedKmh)
        {
            _carSpeedKmh = speedKmh;
        }

        private void Update()
        {
            var leftGripping = _leftGrip != null && _leftGrip.action.ReadValue<float>() > 0.5f;
            var rightGripping = _rightGrip != null && _rightGrip.action.ReadValue<float>() > 0.5f;

            UpdateHand(leftGripping, _leftHand, ref _leftHandAngleDeg);
            UpdateHand(rightGripping, _rightHand, ref _rightHandAngleDeg);

            if (!leftGripping && !rightGripping)
            {
                SelfCenter(Time.deltaTime);
            }

            _wheelAngleDeg = Mathf.Clamp(_wheelAngleDeg, -_config.maxWheelAngleDeg, _config.maxWheelAngleDeg);
        }

        private void UpdateHand(bool gripping, Transform hand, ref float? prevAngleDeg)
        {
            if (!gripping || hand == null)
            {
                prevAngleDeg = null;
                return;
            }

            var angleDeg = AngleAroundWheel(hand.position);

            if (prevAngleDeg.HasValue)
            {
                _wheelAngleDeg += Mathf.DeltaAngle(prevAngleDeg.Value, angleDeg);
            }

            prevAngleDeg = angleDeg;
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

            var speedMultiplier = 1f + _carSpeedKmh / Mathf.Max(_config.centerRateReferenceSpeedKmh, 0.01f);
            var step = _config.centerRateDegPerSec * speedMultiplier * deltaTimeS;
            _wheelAngleDeg = Mathf.MoveTowards(_wheelAngleDeg, 0f, step);
        }
    }
}
