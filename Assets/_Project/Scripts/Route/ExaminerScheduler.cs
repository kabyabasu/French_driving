namespace PermisVR.Route
{
    /// Decides what the examiner says and when, from the car's progress along the route. Each
    /// maneuver gets one instruction, early enough to prepare at the current speed; side streets
    /// in between are counted ("prenez la deuxième à droite"), and it waits if the turn is
    /// further than the second street. A missed turn is never a fault (the real examiner just
    /// redirects); the candidate is told to turn around.
    public sealed class ExaminerScheduler
    {
        private const float SameJunctionM = 1f; // a junction this close to the maneuver is the maneuver's own

        private readonly RouteDefinition _route;
        private readonly ExaminerConfig _config;

        private float _startS = float.NaN;
        private float _lastSpokenS = float.NegativeInfinity;
        private bool _departSaid;
        private int _next;
        private bool _nextAnnounced;
        private float _offSinceS = float.NaN;
        private bool _offRouteSaid;
        private bool _pullOverSaid;
        private float _stoppedSinceS = float.NaN;

        public ExaminerScheduler(RouteDefinition route, ExaminerConfig config)
        {
            _route = route;
            _config = config;
        }

        public bool Finished { get; private set; }

        /// Index of the next maneuver not yet passed (Maneuvers.Length once all are done).
        public int NextManeuverIndex => _next;

        public bool Tick(float timeS, float routeS, float lateralM, float speedKmh, out ExaminerPhrase phrase)
        {
            phrase = default;
            if (Finished)
            {
                return false;
            }

            if (float.IsNaN(_startS))
            {
                _startS = timeS;
            }

            var maneuvers = _route.Maneuvers;
            while (_next < maneuvers.Length && routeS > maneuvers[_next].S + _config.PassedBeyondM)
            {
                _next++;
                _nextAnnounced = false;
            }

            var offRoute = UpdateOffRoute(timeS, lateralM);
            if (timeS - _lastSpokenS < _config.MinGapS)
            {
                return false;
            }

            if (!_departSaid)
            {
                if (timeS - _startS < _config.DepartDelayS)
                {
                    return false;
                }

                _departSaid = true;
                return Say(ExaminerPhrase.Depart, timeS, out phrase);
            }

            if (offRoute)
            {
                if (_offRouteSaid)
                {
                    return false;
                }

                _offRouteSaid = true;
                return Say(ExaminerPhrase.OffRoute, timeS, out phrase);
            }

            if (_next < maneuvers.Length)
            {
                if (!_nextAnnounced && TryAnnounce(maneuvers[_next], routeS, speedKmh, out phrase))
                {
                    _nextAnnounced = true;
                    _lastSpokenS = timeS;
                    return true;
                }

                return false;
            }

            if (!_pullOverSaid)
            {
                if (routeS < _route.LengthM - _config.PullOverBeforeEndM)
                {
                    return false;
                }

                _pullOverSaid = true;
                return Say(ExaminerPhrase.PullOver, timeS, out phrase);
            }

            if (speedKmh > _config.StoppedSpeedKmh)
            {
                _stoppedSinceS = float.NaN;
                return false;
            }

            if (float.IsNaN(_stoppedSinceS))
            {
                _stoppedSinceS = timeS;
            }

            if (timeS - _stoppedSinceS < _config.StoppedForS)
            {
                return false;
            }

            Finished = true;
            return Say(ExaminerPhrase.ExamFinished, timeS, out phrase);
        }

        private bool UpdateOffRoute(float timeS, float lateralM)
        {
            if (lateralM <= _config.OffRouteLateralM)
            {
                _offSinceS = float.NaN;
                if (lateralM <= _config.OffRouteLateralM * 0.5f)
                {
                    _offRouteSaid = false; // back on the route: a later excursion is reported again
                }

                return false;
            }

            if (float.IsNaN(_offSinceS))
            {
                _offSinceS = timeS;
            }

            return timeS - _offSinceS >= _config.OffRouteDelayS;
        }

        private bool TryAnnounce(in RouteManeuver maneuver, float routeS, float speedKmh, out ExaminerPhrase phrase)
        {
            phrase = default;
            var distanceM = maneuver.S - routeS;
            var leadM = speedKmh / 3.6f * _config.AnnounceLeadS;
            leadM = leadM < _config.MinAnnounceM ? _config.MinAnnounceM : leadM > _config.MaxAnnounceM ? _config.MaxAnnounceM : leadM;
            if (distanceM > leadM)
            {
                return false;
            }

            switch (maneuver.Type)
            {
                case ManeuverType.Straight:
                    if (CountJunctions(routeS, maneuver.S, anySide: true, right: false) > 1)
                    {
                        return false; // "au prochain carrefour" must be the next junction
                    }

                    phrase = ExaminerPhrase.StraightOn;
                    return true;
                case ManeuverType.Roundabout:
                    if (maneuver.RoundaboutExit < 1 || maneuver.RoundaboutExit > 4)
                    {
                        return false; // no clip for it: the route data must be fixed
                    }

                    phrase = ExaminerPhrase.RoundaboutExit1 + (maneuver.RoundaboutExit - 1);
                    return true;
                default:
                    var right = maneuver.Type == ManeuverType.Right;
                    var ordinal = CountJunctions(routeS, maneuver.S, anySide: false, right);
                    if (ordinal > 2)
                    {
                        return false; // too many streets before it: wait until closer
                    }

                    phrase = ordinal == 2
                        ? (right ? ExaminerPhrase.SecondRight : ExaminerPhrase.SecondLeft)
                        : (right ? ExaminerPhrase.NextRight : ExaminerPhrase.NextLeft);
                    return true;
            }
        }

        /// Which junction (anySide) or which street on that side the maneuver's is: 1 + those between
        /// just ahead of the car and the maneuver's own junction.
        private int CountJunctions(float routeS, float maneuverS, bool anySide, bool right)
        {
            var count = 1;
            var junctions = _route.Junctions;
            for (var i = 0; i < junctions.Length; i++)
            {
                var s = junctions[i].S;
                if (s <= routeS + _config.JunctionBehindM || s >= maneuverS - SameJunctionM)
                {
                    continue;
                }

                if (anySide || (right ? junctions[i].HasRight : junctions[i].HasLeft))
                {
                    count++;
                }
            }

            return count;
        }

        private bool Say(ExaminerPhrase value, float timeS, out ExaminerPhrase phrase)
        {
            phrase = value;
            _lastSpokenS = timeS;
            return true;
        }
    }
}
