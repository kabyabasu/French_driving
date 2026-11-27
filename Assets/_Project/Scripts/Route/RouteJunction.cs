namespace PermisVR.Route
{
    /// A junction the route passes, with whether a drivable street leaves on each side
    /// (to count "la deuxième à droite").
    public readonly struct RouteJunction
    {
        public readonly float S;
        public readonly bool HasLeft;
        public readonly bool HasRight;

        public RouteJunction(float s, bool hasLeft, bool hasRight)
        {
            S = s;
            HasLeft = hasLeft;
            HasRight = hasRight;
        }
    }
}
