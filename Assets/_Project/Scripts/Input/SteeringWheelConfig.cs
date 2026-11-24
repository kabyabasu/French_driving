using UnityEngine;

namespace PermisVR.Input
{
    [CreateAssetMenu(fileName = "SteeringWheelConfig", menuName = "PermisVR/Input/Steering Wheel Config")]
    public sealed class SteeringWheelConfig : ScriptableObject
    {
        [Tooltip("900 deg lock-to-lock (±450°), like a real car.")]
        public float maxWheelAngleDeg = 450f;

        [Header("Self-centering (released wheel)")]
        public float centerRateDegPerSec = 180f;
        [Tooltip("Centering rate doubles by the time the car reaches this speed.")]
        public float centerRateReferenceSpeedKmh = 50f;
    }
}
