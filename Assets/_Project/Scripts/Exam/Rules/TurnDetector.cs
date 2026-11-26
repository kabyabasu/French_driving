using System;

namespace PermisVR.Exam.Rules
{
    /// Recognises a turn at a junction from the heading history: at least TurnAngleDeg within
    /// TurnWindowS, while moving and inside a junction area. Reports each turn once, with the time
    /// the turn started (heading first TurnStartDeg away). Shared by the mirror and blind-spot rules.
    public sealed class TurnDetector
    {
        // 50 Hz ticks over the turn window fit comfortably; fixed so ticks never allocate.
        private const int Capacity = 512;

        private readonly MirrorRulesConfig _config;
        private readonly float[] _times = new float[Capacity];
        private readonly float[] _headings = new float[Capacity];
        private int _start;
        private int _count;
        private bool _awaitingStraight;

        public TurnDetector(MirrorRulesConfig config)
        {
            _config = config;
        }

        /// Feed every VehicleTick. Returns true once per turn.
        public bool Tick(in VehicleSnapshot vehicle, bool insideJunction, out float turnStartS, out bool turningRight)
        {
            turnStartS = 0f;
            turningRight = false;

            Push(vehicle.TimeS, vehicle.HeadingDeg);
            var oldest = OldestOffsetWithin(vehicle.TimeS - _config.TurnWindowS);
            var oldestHeading = _headings[IndexAt(oldest)];
            var turned = Angles.Delta(oldestHeading, vehicle.HeadingDeg);
            var magnitude = Math.Abs(turned);

            if (_awaitingStraight)
            {
                // One report per turn: wait until the window no longer contains the turn.
                if (magnitude < _config.TurnStartDeg)
                {
                    _awaitingStraight = false;
                }

                return false;
            }

            if (magnitude < _config.TurnAngleDeg || vehicle.SpeedKmh < _config.MinTurnSpeedKmh || !insideJunction)
            {
                return false;
            }

            _awaitingStraight = true;
            turnStartS = TurnStartTime(oldest, oldestHeading);
            turningRight = turned > 0f;
            return true;
        }

        private void Push(float timeS, float headingDeg)
        {
            var index = IndexAt(_count == Capacity ? 0 : _count);
            if (_count == Capacity)
            {
                _start = (_start + 1) % Capacity;
            }
            else
            {
                _count++;
            }

            _times[index] = timeS;
            _headings[index] = headingDeg;
        }

        private int IndexAt(int offset) => (_start + offset) % Capacity;

        private int OldestOffsetWithin(float cutoffS)
        {
            for (var offset = 0; offset < _count; offset++)
            {
                if (_times[IndexAt(offset)] >= cutoffS)
                {
                    return offset;
                }
            }

            return _count - 1;
        }

        private float TurnStartTime(int fromOffset, float referenceHeading)
        {
            for (var offset = fromOffset; offset < _count; offset++)
            {
                var index = IndexAt(offset);
                if (Math.Abs(Angles.Delta(referenceHeading, _headings[index])) >= _config.TurnStartDeg)
                {
                    return _times[index];
                }
            }

            return _times[IndexAt(_count - 1)];
        }
    }
}
