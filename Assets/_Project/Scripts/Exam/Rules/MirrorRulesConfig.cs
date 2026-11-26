namespace PermisVR.Exam.Rules
{
    /// Thresholds for the mirror and blind-spot check rules. Unverified starting values: check against
    /// official guidance before release.
    public sealed class MirrorRulesConfig
    {
        /// A check counts if it happened this long before the maneuver started...
        public float LookbackS = 5f;

        /// ...or up to this long after (reaction tolerance).
        public float GraceS = 0.5f;

        // Turning at a junction.
        public float TurnAngleDeg = 45f;
        public float TurnWindowS = 4f;
        public float TurnStartDeg = 10f;
        public float MinTurnSpeedKmh = 2f;

        // Moving off.
        public float StoppedSpeedKmh = 1f;
        public float PullOutMinStopS = 10f;
        /// Speed that counts as having moved off; small creeps and bounces below it are ignored.
        public float MoveOffSpeedKmh = 3f;

        // Braking.
        public float BrakingDecelMs2 = 2.5f;
        public float BrakingMinDurationS = 0.4f;
        public float BrakingMinSpeedKmh = 25f;
        public float DecelSmoothingS = 0.15f;

        // Blind spot (angle mort). Moving off always needs the left one.
        public bool BlindSpotOnTurns = true;
    }
}
