namespace PermisVR.Route
{
    /// When the examiner speaks. Values come from data (ExaminerConfigAsset); unverified starting values.
    public sealed class ExaminerConfig
    {
        /// Wait this long after the exam starts before the opening instruction.
        public float DepartDelayS = 3f;
        /// Announce a maneuver this many seconds before reaching it at the current speed...
        public float AnnounceLeadS = 7f;
        /// ...but never closer than this...
        public float MinAnnounceM = 40f;
        /// ...nor further than this.
        public float MaxAnnounceM = 130f;
        /// A maneuver counts as passed this far beyond its junction.
        public float PassedBeyondM = 10f;
        /// Junctions closer than this ahead are already being crossed and aren't counted as "prochain".
        public float JunctionBehindM = 5f;
        /// Minimum silence between two phrases.
        public float MinGapS = 4f;
        /// Off the route by more than this...
        public float OffRouteLateralM = 20f;
        /// ...for this long: tell the candidate.
        public float OffRouteDelayS = 3f;
        /// Ask to pull over this far before the end of the route.
        public float PullOverBeforeEndM = 120f;
        /// The exam is over once the car has stopped for this long after the pull-over request.
        public float StoppedForS = 2f;
        public float StoppedSpeedKmh = 1f;
    }
}
