using System;

namespace PermisVR.Exam.Rules
{
    /// French rule: before turning at a junction, check the interior mirror and the mirror on the
    /// side you turn towards. Missing either is a minor fault ("Prendre l'information").
    public sealed class MirrorBeforeTurnRule : IExamRule
    {
        public string RuleId => "mirror_before_turn";
        public event Action<Fault> FaultRaised;

        // 50 Hz ticks over the turn window fit comfortably; fixed so ticks never allocate.
        private const int Capacity = 512;

        private readonly MirrorRulesConfig _config;
        private readonly MirrorCheckHistory _history = new();
        private readonly float[] _times = new float[Capacity];
        private readonly float[] _headings = new float[Capacity];
        private int _start;
        private int _count;
        private int _intersectionsInside;
        private bool _awaitingStraight;

        public MirrorBeforeTurnRule(MirrorRulesConfig config)
        {
            _config = config;
        }

        public void HandleEvent(in ExamEvent evt)
        {
            switch (evt.Type)
            {
                case ExamEventType.MirrorChecked:
                    _history.Record(evt.ZoneId, evt.Vehicle.TimeS);
                    break;
                case ExamEventType.IntersectionEnter:
                    _intersectionsInside++;
                    break;
                case ExamEventType.IntersectionExit:
                    _intersectionsInside = Math.Max(0, _intersectionsInside - 1);
                    break;
                case ExamEventType.VehicleTick:
                    OnTick(evt.Vehicle);
                    break;
            }
        }

        private void OnTick(in VehicleSnapshot vehicle)
        {
            Push(vehicle.TimeS, vehicle.HeadingDeg);
            var oldest = OldestOffsetWithin(vehicle.TimeS - _config.TurnWindowS);
            var oldestHeading = _headings[IndexAt(oldest)];
            var turned = Angles.Delta(oldestHeading, vehicle.HeadingDeg);
            var magnitude = Math.Abs(turned);

            if (_awaitingStraight)
            {
                // One fault per turn: wait until the window no longer contains the turn.
                if (magnitude < _config.TurnStartDeg)
                {
                    _awaitingStraight = false;
                }

                return;
            }

            if (magnitude < _config.TurnAngleDeg || vehicle.SpeedKmh < _config.MinTurnSpeedKmh || _intersectionsInside == 0)
            {
                return;
            }

            _awaitingStraight = true;
            var turnStartS = TurnStartTime(oldest, oldestHeading);
            var turningRight = turned > 0f;
            var sideMirror = turningRight ? MirrorIds.Right : MirrorIds.Left;
            var fromS = turnStartS - _config.LookbackS;
            var toS = turnStartS + _config.GraceS;
            var interiorChecked = _history.CheckedWithin(MirrorIds.Interior, fromS, toS);
            var sideChecked = _history.CheckedWithin(sideMirror, fromS, toS);
            if (interiorChecked && sideChecked)
            {
                return;
            }

            FaultRaised?.Invoke(Fault.At(vehicle, RuleId, ExamCriteria.TakeInformation, FaultSeverity.Minor,
                Explain(turningRight, interiorChecked, sideChecked)));
        }

        private static string Explain(bool turningRight, bool interiorChecked, bool sideChecked)
        {
            var side = turningRight ? "droit" : "gauche";
            var missing = !interiorChecked && !sideChecked ? $"intérieur et {side}"
                : !interiorChecked ? "intérieur" : side;
            return $"Rétroviseurs non contrôlés avant de tourner à {(turningRight ? "droite" : "gauche")} (manque : {missing})";
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
