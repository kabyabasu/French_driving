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
        public float highSpeedSteerAngleDeg = 12f;
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

        [Header("Powertrain (small automatic hatchback, roughly a Yaris)")]
        [Tooltip("Peak pushing force at the tyres at low speed. 3300 N on 1200 kg ≈ 2.75 m/s², about 0-50 km/h in 5 s.")]
        public float maxTractiveForceN = 3300f;
        [Tooltip("Power at the wheels; above ~65 km/h the push falls off as power / speed (0-100 km/h ≈ 11 s).")]
        public float maxPowerKw = 60f;
        [Tooltip("Seconds for the engine to go from idle to full push: no instant torque, like a real engine.")]
        public float throttleRiseS = 0.35f;
        [Tooltip("Per-wheel brake torque at full pedal. 1350 Nm × 4 / 0.5 m wheel ≈ 9 m/s², a real car's maximum.")]
        public float brakeTorque = 1350f;
        [Tooltip("Share of braking on the front axle. Real cars bias forward so the lightly loaded rear wheels don't lock.")]
        [Range(0.5f, 0.8f)] public float brakeFrontBias = 0.65f;
        [Tooltip("Per-wheel brake torque applied when the throttle is released (engine braking + rolling resistance).")]
        public float engineBrakeTorque = 120f;
        [Tooltip("Aerodynamic drag: force = coefficient * speed² (N per (m/s)²).")]
        public float airDragCoefficient = 0.4f;

        [Header("Comfort")]
        [Tooltip("Rigidbody interpolation avoids judder; keep this on.")]
        public bool interpolate = true;
    }
}
