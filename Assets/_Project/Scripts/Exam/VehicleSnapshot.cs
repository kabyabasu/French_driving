namespace PermisVR.Exam
{
    /// A snapshot of the player vehicle's state at one instant, as seen by exam rules.
    public readonly struct VehicleSnapshot
    {
        public readonly float TimeS;
        public readonly float SpeedKmh;
        public readonly float RoutePositionM;
        public readonly float HeadingDeg;

        public VehicleSnapshot(float timeS, float speedKmh, float routePositionM, float headingDeg)
        {
            TimeS = timeS;
            SpeedKmh = speedKmh;
            RoutePositionM = routePositionM;
            HeadingDeg = headingDeg;
        }
    }
}
