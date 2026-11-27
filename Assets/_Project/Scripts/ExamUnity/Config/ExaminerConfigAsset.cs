using PermisVR.Route;
using UnityEngine;

namespace PermisVR.ExamUnity.Config
{
    /// When the examiner speaks and how the car is followed along the route. Unverified starting values.
    [CreateAssetMenu(fileName = "ExaminerConfig", menuName = "PermisVR/Route/Examiner Config")]
    public sealed class ExaminerConfigAsset : ScriptableObject
    {
        [Header("Instructions")]
        public float departDelayS = 3f;
        [Tooltip("Announce a turn this many seconds ahead at the current speed, within the min/max distances below.")]
        public float announceLeadS = 7f;
        public float minAnnounceM = 40f;
        public float maxAnnounceM = 130f;
        public float passedBeyondM = 10f;
        public float junctionBehindM = 5f;
        public float minGapS = 4f;

        [Header("Off route")]
        public float offRouteLateralM = 20f;
        public float offRouteDelayS = 3f;

        [Header("End of the route")]
        public float pullOverBeforeEndM = 120f;
        public float stoppedForS = 2f;
        public float stoppedSpeedKmh = 1f;

        [Header("Following the car along the route")]
        public float searchBackM = 20f;
        public float searchAheadM = 60f;
        public float startSearchM = 100f;
        public float finishToleranceM = 10f;
        public float reacquireAboveM = 25f;
        public float rejoinWithinM = 8f;

        [Header("Subtitles")]
        [Tooltip("Subtitle stays this long after the clip ends.")]
        public float subtitleExtraS = 2f;

        public ExaminerConfig ToConfig()
        {
            return new ExaminerConfig
            {
                DepartDelayS = departDelayS,
                AnnounceLeadS = announceLeadS,
                MinAnnounceM = minAnnounceM,
                MaxAnnounceM = maxAnnounceM,
                PassedBeyondM = passedBeyondM,
                JunctionBehindM = junctionBehindM,
                MinGapS = minGapS,
                OffRouteLateralM = offRouteLateralM,
                OffRouteDelayS = offRouteDelayS,
                PullOverBeforeEndM = pullOverBeforeEndM,
                StoppedForS = stoppedForS,
                StoppedSpeedKmh = stoppedSpeedKmh,
            };
        }

        public RouteTrackerConfig ToTrackerConfig()
        {
            return new RouteTrackerConfig
            {
                SearchBackM = searchBackM,
                SearchAheadM = searchAheadM,
                StartSearchM = startSearchM,
                FinishToleranceM = finishToleranceM,
                ReacquireAboveM = reacquireAboveM,
                RejoinWithinM = rejoinWithinM,
            };
        }
    }
}
