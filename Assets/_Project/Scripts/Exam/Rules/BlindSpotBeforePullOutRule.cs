using System;

namespace PermisVR.Exam.Rules
{
    /// French rule: before moving off, look over the left shoulder (angle mort gauche) for traffic
    /// the mirror can't show. Missing it is a minor fault ("Prendre l'information").
    public sealed class BlindSpotBeforePullOutRule : IExamRule
    {
        public string RuleId => "blind_spot_before_pull_out";
        public event Action<Fault> FaultRaised;

        private readonly MirrorRulesConfig _config;
        private readonly MirrorCheckHistory _history = new();
        private readonly MoveOffDetector _moveOff;
        private int _intersectionsInside;

        public BlindSpotBeforePullOutRule(MirrorRulesConfig config)
        {
            _config = config;
            _moveOff = new MoveOffDetector(config);
        }

        public void HandleEvent(in ExamEvent evt)
        {
            switch (evt.Type)
            {
                case ExamEventType.BlindSpotChecked:
                    _history.Record(evt.ZoneId, evt.Vehicle.TimeS);
                    break;
                case ExamEventType.IntersectionEnter:
                    _intersectionsInside++;
                    break;
                case ExamEventType.IntersectionExit:
                    _intersectionsInside = Math.Max(0, _intersectionsInside - 1);
                    break;
                case ExamEventType.VehicleTick:
                    if (_moveOff.Tick(evt.Vehicle, _intersectionsInside > 0, out var moveStartS)
                        && !_history.CheckedWithin(BlindSpotIds.Left, moveStartS - _config.LookbackS, moveStartS + _config.GraceS))
                    {
                        FaultRaised?.Invoke(Fault.At(evt.Vehicle, RuleId, ExamCriteria.TakeInformation, FaultSeverity.Minor,
                            "Angle mort gauche non contrôlé avant de démarrer"));
                    }

                    break;
            }
        }
    }
}
