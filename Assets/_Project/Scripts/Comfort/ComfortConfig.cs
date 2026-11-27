using UnityEngine;

namespace PermisVR.Comfort
{
    /// Motion-sickness settings. Starting values: tune on the headset.
    [CreateAssetMenu(fileName = "ComfortConfig", menuName = "PermisVR/Comfort/Comfort Config")]
    public sealed class ComfortConfig : ScriptableObject
    {
        [Header("Stabilised cockpit")]
        [Tooltip("How much of the car body's pitch and roll (suspension lean, braking dive) the driver sees. " +
                 "0 = none: the map is flat, so body pitch/roll is only suspension motion the inner ear doesn't feel.")]
        [Range(0f, 1f)] public float pitchRollFollow = 0f;
        [Tooltip("Vertical bounce (curbs, spawn settling) is smoothed over about this long.")]
        public float heightSmoothingS = 0.3f;

        [Header("Comfort vignette (while the car turns)")]
        public bool vignetteEnabled = true;
        [Tooltip("Car yaw rate where the view starts narrowing.")]
        public float yawRateStartDegPerS = 10f;
        [Tooltip("Car yaw rate where it is narrowest.")]
        public float yawRateFullDegPerS = 40f;
        [Tooltip("Half-angle of the clear view with no vignette (beyond the headset's field of view).")]
        public float openApertureDeg = 70f;
        [Tooltip("Half-angle of the clear view at full strength.")]
        public float narrowApertureDeg = 32f;
        [Tooltip("Width of the soft edge.")]
        public float featherDeg = 14f;
        public float fadeInS = 0.15f;
        public float fadeOutS = 0.5f;
    }
}
