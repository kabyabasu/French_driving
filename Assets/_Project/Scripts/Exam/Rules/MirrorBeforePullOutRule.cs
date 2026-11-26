using System;

namespace PermisVR.Exam.Rules
{
    /// French rule: before moving off (exam start, or after a long stop away from a junction),
    /// check the interior and left mirrors. Missing either is a minor fault ("Prendre l'information").
    /// Waits at a STOP or light are excluded: they happen inside a junction area.
    public sealed class MirrorBeforePullOutRule : IExamRule
    {
        public string RuleId => "mirror_before_pull_out";
        public event Action<Fault> FaultRaised;

        private readonly MirrorRulesConfig _config;
        private readonly MirrorCheckHistory _history = new();
        private bool _stopped = true;

        // Negative infinity = stationary since the exam started, so the first move always counts.
        private float _stoppedSinceS = float.NegativeInfinity;
        private float _pendingMoveStartS = float.NaN;
        private int _intersectionsInside;

        public MirrorBeforePullOutRule(MirrorRulesConfig config)
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
            // Judged once the grace period after moving off has passed.
            if (!float.IsNaN(_pendingMoveStartS) && vehicle.TimeS >= _pendingMoveStartS + _config.GraceS)
            {
                Evaluate(vehicle, _pendingMoveStartS);
                _pendingMoveStartS = float.NaN;
            }

            if (vehicle.SpeedKmh <= _config.StoppedSpeedKmh)
            {
                if (!_stopped)
                {
                    _stopped = true;
                    _stoppedSinceS = vehicle.TimeS;
                }

                return;
            }

            if (!_stopped)
            {
                return;
            }

            _stopped = false;
            if (vehicle.TimeS - _stoppedSinceS >= _config.PullOutMinStopS && _intersectionsInside == 0)
            {
                _pendingMoveStartS = vehicle.TimeS;
            }
        }

        private void Evaluate(in VehicleSnapshot vehicle, float moveStartS)
        {
            var fromS = moveStartS - _config.LookbackS;
            var toS = moveStartS + _config.GraceS;
            var interiorChecked = _history.CheckedWithin(MirrorIds.Interior, fromS, toS);
            var leftChecked = _history.CheckedWithin(MirrorIds.Left, fromS, toS);
            if (interiorChecked && leftChecked)
            {
                return;
            }

            var missing = !interiorChecked && !leftChecked ? "intérieur et gauche" : !interiorChecked ? "intérieur" : "gauche";
            FaultRaised?.Invoke(Fault.At(vehicle, RuleId, ExamCriteria.TakeInformation, FaultSeverity.Minor,
                $"Rétroviseurs non contrôlés avant de démarrer (manque : {missing})"));
        }
    }
}
