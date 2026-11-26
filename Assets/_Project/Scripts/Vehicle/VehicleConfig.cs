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

        [Header("Stability")]
        [Tooltip("Rigidbody center of mass, car-local. Kept low (near axle height) to resist rollover.")]
        public Vector3 centerOfMass = new(0f, -0.6f, 0f);

        [Header("Powertrain")]
        public float motorTorque = 1500f;
        public float brakeTorque = 3000f;

        [Header("Comfort")]
        [Tooltip("Rigidbody interpolation avoids judder; keep this on.")]
        public bool interpolate = true;
    }
}
