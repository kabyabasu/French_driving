using PermisVR.Exam.Rules;
using UnityEngine;

namespace PermisVR.ExamUnity.Config
{
    /// Thresholds for the mirror-check rules. Unverified starting values: check against official guidance.
    [CreateAssetMenu(fileName = "MirrorRulesConfig", menuName = "PermisVR/Exam/Mirror Rules Config")]
    public sealed class MirrorRulesConfigAsset : ScriptableObject
    {
        [Tooltip("A check counts if it happened up to this long before the maneuver started.")]
        public float lookbackS = 5f;
        [Tooltip("...or up to this long after it started.")]
        public float graceS = 0.5f;

        [Header("Turning at a junction")]
        public float turnAngleDeg = 45f;
        public float turnWindowS = 4f;
        public float turnStartDeg = 10f;
        public float minTurnSpeedKmh = 2f;

        [Header("Moving off")]
        public float stoppedSpeedKmh = 1f;
        public float pullOutMinStopS = 10f;

        [Header("Braking")]
        public float brakingDecelMs2 = 2.5f;
        public float brakingMinDurationS = 0.4f;
        public float brakingMinSpeedKmh = 25f;
        public float decelSmoothingS = 0.15f;

        public MirrorRulesConfig ToConfig()
        {
            return new MirrorRulesConfig
            {
                LookbackS = lookbackS,
                GraceS = graceS,
                TurnAngleDeg = turnAngleDeg,
                TurnWindowS = turnWindowS,
                TurnStartDeg = turnStartDeg,
                MinTurnSpeedKmh = minTurnSpeedKmh,
                StoppedSpeedKmh = stoppedSpeedKmh,
                PullOutMinStopS = pullOutMinStopS,
                BrakingDecelMs2 = brakingDecelMs2,
                BrakingMinDurationS = brakingMinDurationS,
                BrakingMinSpeedKmh = brakingMinSpeedKmh,
                DecelSmoothingS = decelSmoothingS,
            };
        }
    }
}
