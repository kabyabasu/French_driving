using UnityEngine;

namespace PermisVR.Input
{
    [CreateAssetMenu(fileName = "SteeringWheelConfig", menuName = "PermisVR/Input/Steering Wheel Config")]
    public sealed class SteeringWheelConfig : ScriptableObject
    {
        [Tooltip("450° lock-to-lock (±225°): quicker than a real car's 900°, which feels sluggish in VR\n" +
                 "without the resistance of a real wheel (driving simulators use similar values).")]
        public float maxWheelAngleDeg = 225f;

        [Header("Self-centering (released wheel)")]
        public float centerRateDegPerSec = 180f;
        [Tooltip("Centering rate doubles by the time the car reaches this speed.")]
        public float centerRateReferenceSpeedKmh = 50f;

        [Header("Grabbing (press grip once to hold, again to let go)")]
        [Tooltip("Radius of the wheel rim; held hands are drawn on it.")]
        public float rimRadiusM = 0.18f;
        [Tooltip("A grip press grabs the wheel only if the hand is this close to the rim.")]
        public float grabDistanceM = 0.15f;
        [Tooltip("A held hand lets go by itself if it moves this far from the rim (e.g. reaching for something).")]
        public float releaseDistanceM = 0.35f;
        [Tooltip("Hands closer than this to the wheel centre are ignored: tiny moves there would be huge angles.")]
        public float minHandRadiusM = 0.06f;
    }
}
