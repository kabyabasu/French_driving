namespace PermisVR.Route
{
    /// One instruction point on the route: where the junction is (distance along the route) and what to do there.
    public readonly struct RouteManeuver
    {
        public readonly float S;
        public readonly ManeuverType Type;
        public readonly int RoundaboutExit;
        public readonly string Street;

        public RouteManeuver(float s, ManeuverType type, int roundaboutExit = 0, string street = "")
        {
            S = s;
            Type = type;
            RoundaboutExit = roundaboutExit;
            Street = street ?? "";
        }
    }
}
