using UnityEngine;

namespace PermisVR.Vehicle
{
    [CreateAssetMenu(fileName = "VehicleConfig", menuName = "PermisVR/Vehicle/Vehicle Config")]
    public sealed class VehicleConfig : ScriptableObject
    {
        [Header("Steering")]
        [Tooltip("Front wheel angle at full lock when stopped.")]
        public float maxSteerAngleDeg = 35f;
        [Tooltip("Front wheel angle at full lock at or above highSpeedReferenceKmh. Lower = less rollover risk.")]
        public float highSpeedSteerAngleDeg = 8f;
        public float highSpeedReferenceKmh = 80f;

        [Header("Gearbox (automatic)")]
        [Tooltip("Drive/Reverse is only accepted below this speed (CLAUDE.md: < 1 km/h).")]
        public float gearChangeMaxSpeedKmh = 1f;
        [Tooltip("Fraction of motorTorque available in reverse.")]
        public float reverseTorqueFactor = 0.5f;
        public float maxReverseSpeedKmh = 15f;

        [Header("Stability")]
        [Tooltip("Rigidbody center of mass, car-local. Kept low (near axle height) to resist rollover.")]
        public Vector3 centerOfMass = new(0f, -0.6f, 0f);

        [Header("Powertrain")]
        public float motorTorque = 1500f;
        public float brakeTorque = 3000f;
        [Tooltip("Per-wheel brake torque applied when the throttle is released (engine braking + rolling resistance).")]
        public float engineBrakeTorque = 120f;
        [Tooltip("Aerodynamic drag: force = coefficient * speed² (N per (m/s)²).")]
        public float airDragCoefficient = 0.4f;

        [Header("Comfort")]
        [Tooltip("Rigidbody interpolation avoids judder; keep this on.")]
        public bool interpolate = true;
    }
}
