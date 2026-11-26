using UnityEngine;

namespace PermisVR.ExamUnity.Config
{
    /// When a head turn counts as looking at a mirror.
    [CreateAssetMenu(fileName = "MirrorGazeConfig", menuName = "PermisVR/Exam/Mirror Gaze Config")]
    public sealed class MirrorGazeConfigAsset : ScriptableObject
    {
        [Tooltip("Max angle between head forward and the direction to the mirror.")]
        public float gazeAngleDeg = 15f;
        [Tooltip("How long the head must stay on the mirror.")]
        public float dwellS = 0.15f;
    }
}
