namespace PermisVR.Route
{
    /// Search window for following the car along the route. Values come from data (ExaminerConfigAsset).
    public sealed class RouteTrackerConfig
    {
        public float SearchBackM = 20f;
        public float SearchAheadM = 60f;
        public float StartSearchM = 100f;
        public float FinishToleranceM = 10f;
        public float ReacquireAboveM = 25f;
        public float RejoinWithinM = 8f;
    }
}
