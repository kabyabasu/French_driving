namespace PermisVR.Exam.Rules
{
    /// Recognises moving off: at the exam start, or after a stop of at least PullOutMinStopS away
    /// from a junction (waits at a STOP or light don't count). Hysteresis: stopped at or below
    /// StoppedSpeedKmh, moved off only once MoveOffSpeedKmh is reached, so creeping or the car
    /// settling at spawn doesn't count. Reports once the grace period after
    /// moving off has passed, so checks made just after starting still count. Shared by the mirror
    /// and blind-spot rules.
    public sealed class MoveOffDetector
    {
        private readonly MirrorRulesConfig _config;
        private bool _stopped = true;

        // Negative infinity = stationary since the exam started, so the first move always counts.
        private float _stoppedSinceS = float.NegativeInfinity;
        private float _pendingMoveStartS = float.NaN;

        public MoveOffDetector(MirrorRulesConfig config)
        {
            _config = config;
        }

        /// Feed every VehicleTick. Returns true once per move-off, GraceS after it started.
        public bool Tick(in VehicleSnapshot vehicle, bool insideJunction, out float moveStartS)
        {
            moveStartS = 0f;
            var due = false;
            if (!float.IsNaN(_pendingMoveStartS) && vehicle.TimeS >= _pendingMoveStartS + _config.GraceS)
            {
                moveStartS = _pendingMoveStartS;
                _pendingMoveStartS = float.NaN;
                due = true;
            }

            if (vehicle.SpeedKmh <= _config.StoppedSpeedKmh)
            {
                if (!_stopped)
                {
                    _stopped = true;
                    _stoppedSinceS = vehicle.TimeS;
                }

                return due;
            }

            if (_stopped && vehicle.SpeedKmh >= _config.MoveOffSpeedKmh)
            {
                _stopped = false;
                if (vehicle.TimeS - _stoppedSinceS >= _config.PullOutMinStopS && !insideJunction)
                {
                    _pendingMoveStartS = vehicle.TimeS;
                }
            }

            return due;
        }
    }
}
