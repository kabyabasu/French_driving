using UnityEngine;

namespace PermisVR.Traffic
{
    /// Two-phase signal cycle: phase A green+amber, all-red, phase B green+amber, all-red.
    [CreateAssetMenu(fileName = "TrafficLightTiming", menuName = "PermisVR/Traffic/Traffic Light Timing")]
    public sealed class TrafficLightTiming : ScriptableObject
    {
        public float greenS = 20f;
        [Tooltip("French urban amber is 3 s.")]
        public float amberS = 3f;
        [Tooltip("Both phases red between switches.")]
        public float allRedS = 2f;

        public float CycleS => 2f * (greenS + amberS + allRedS);
        public float HalfCycleS => greenS + amberS + allRedS;
    }
}
