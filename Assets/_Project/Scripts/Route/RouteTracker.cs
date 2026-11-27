namespace PermisVR.Route
{
    /// Follows the car along a route: distance travelled along it and how far off it the car is.
    /// Searches only a window around the last position, so a route that passes near itself (a
    /// loop back to the start) doesn't make the position jump ahead.
    public sealed class RouteTracker
    {
        private readonly RouteDefinition _route;
        private readonly RouteTrackerConfig _config;
        private bool _started;

        public RouteTracker(RouteDefinition route, RouteTrackerConfig config)
        {
            _route = route;
            _config = config;
        }

        public float PositionM { get; private set; }
        public float LateralM { get; private set; }
        public bool Finished => PositionM >= _route.LengthM - _config.FinishToleranceM;

        public void Update(float x, float z)
        {
            var from = _started ? PositionM - _config.SearchBackM : 0f;
            var to = _started ? PositionM + _config.SearchAheadM : _config.StartSearchM;
            if (_route.Project(x, z, from, to, out var s, out var lateral))
            {
                _started = true;
                // Never slide backwards along the route: reversing a little or drifting across
                // the road keeps the furthest point reached.
                if (s > PositionM)
                {
                    PositionM = s;
                }

                LateralM = lateral;
            }

            // Far off the route (a wrong turn): pick it up again wherever the car rejoins it ahead.
            if (_started && LateralM > _config.ReacquireAboveM
                && _route.Project(x, z, PositionM, _route.LengthM, out var aheadS, out var aheadLateral)
                && aheadLateral < _config.RejoinWithinM)
            {
                PositionM = aheadS;
                LateralM = aheadLateral;
            }
        }
    }
}
