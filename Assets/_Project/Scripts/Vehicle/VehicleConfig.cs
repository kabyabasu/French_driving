using UnityEngine;

namespace PermisVR.Vehicle
{
    [CreateAssetMenu(fileName = "VehicleConfig", menuName = "PermisVR/Vehicle/Vehicle Config")]
    public sealed class VehicleConfig : ScriptableObject
    {
        [Header("Steering")]
        public float maxSteerAngleDeg = 35f;

        [Header("Powertrain")]
        public float motorTorque = 1500f;
        public float brakeTorque = 3000f;

        [Header("Comfort")]
        [Tooltip("Rigidbody interpolation avoids judder; keep this on.")]
        public bool interpolate = true;
    }
}
