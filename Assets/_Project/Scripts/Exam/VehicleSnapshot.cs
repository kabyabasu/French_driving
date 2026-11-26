namespace PermisVR.Exam
{
    /// A snapshot of the player vehicle's state at one instant, as seen by exam rules.
    /// Position is map metres from the start point (+X east, +Z north).
    public readonly struct VehicleSnapshot
    {
        public readonly float TimeS;
        public readonly float SpeedKmh;
        public readonly float RoutePositionM;
        public readonly float HeadingDeg;
        public readonly float PositionX;
        public readonly float PositionZ;

        public VehicleSnapshot(float timeS, float speedKmh, float routePositionM, float headingDeg,
            float positionX = 0f, float positionZ = 0f)
        {
            TimeS = timeS;
            SpeedKmh = speedKmh;
            RoutePositionM = routePositionM;
            HeadingDeg = headingDeg;
            PositionX = positionX;
            PositionZ = positionZ;
        }
    }
}
