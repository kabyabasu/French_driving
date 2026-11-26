using System;

namespace PermisVR.Exam.Rules
{
    /// French driving practice: before turning at a junction, look over the shoulder on the turn
    /// side (angle mort) for cyclists and two-wheelers alongside. Missing it is a minor fault
    /// ("Prendre l'information"). Can be switched off with MirrorRulesConfig.BlindSpotOnTurns.
    public sealed class BlindSpotBeforeTurnRule : IExamRule
    {
        public string RuleId => "blind_spot_before_turn";
        public event Action<Fault> FaultRaised;

        private readonly MirrorRulesConfig _config;
        private readonly MirrorCheckHistory _history = new();
        private readonly TurnDetector _turns;
        private int _intersectionsInside;

        public BlindSpotBeforeTurnRule(MirrorRulesConfig config)
        {
            _config = config;
            _turns = new TurnDetector(config);
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
                    OnTick(evt.Vehicle);
                    break;
            }
        }

        private void OnTick(in VehicleSnapshot vehicle)
        {
            // Always fed, so the detector's history stays correct if the option is toggled.
            if (!_turns.Tick(vehicle, _intersectionsInside > 0, out var turnStartS, out var turningRight)
                || !_config.BlindSpotOnTurns)
            {
                return;
            }

            var side = turningRight ? BlindSpotIds.Right : BlindSpotIds.Left;
            if (_history.CheckedWithin(side, turnStartS - _config.LookbackS, turnStartS + _config.GraceS))
            {
                return;
            }

            FaultRaised?.Invoke(Fault.At(vehicle, RuleId, ExamCriteria.TakeInformation, FaultSeverity.Minor,
                turningRight ? "Angle mort droit non contrôlé avant de tourner à droite"
                    : "Angle mort gauche non contrôlé avant de tourner à gauche"));
        }
    }
}
