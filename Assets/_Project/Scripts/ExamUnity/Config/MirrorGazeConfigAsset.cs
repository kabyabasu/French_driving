using UnityEngine;

namespace PermisVR.ExamUnity.Config
{
    /// When a head turn counts as looking at a mirror, or over a shoulder into the blind spot.
    [CreateAssetMenu(fileName = "MirrorGazeConfig", menuName = "PermisVR/Exam/Mirror Gaze Config")]
    public sealed class MirrorGazeConfigAsset : ScriptableObject
    {
        [Tooltip("Max angle between head forward and the direction to the mirror.")]
        public float gazeAngleDeg = 15f;
        [Tooltip("How long the head must stay on the mirror.")]
        public float dwellS = 0.15f;

        [Header("Blind spot (angle mort)")]
        [Tooltip("Head yaw relative to the car, left or right, that counts as a shoulder check. From the driver's seat the left mirror is ~45 deg and the right ~70 deg away; looking at a mirror never counts.")]
        public float blindSpotYawDeg = 70f;
        [Tooltip("How long the head must stay turned that far.")]
        public float blindSpotDwellS = 0.15f;
    }
}
