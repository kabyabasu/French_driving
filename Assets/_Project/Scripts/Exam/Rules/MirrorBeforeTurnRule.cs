using System;

namespace PermisVR.Exam.Rules
{
    /// French rule: before turning at a junction, check the interior mirror and the mirror on the
    /// side you turn towards. Missing either is a minor fault ("Prendre l'information").
    public sealed class MirrorBeforeTurnRule : IExamRule
    {
        public string RuleId => "mirror_before_turn";
        public event Action<Fault> FaultRaised;

        private readonly MirrorRulesConfig _config;
        private readonly MirrorCheckHistory _history = new();
        private readonly TurnDetector _turns;
        private int _intersectionsInside;

        public MirrorBeforeTurnRule(MirrorRulesConfig config)
        {
            _config = config;
            _turns = new TurnDetector(config);
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
            if (!_turns.Tick(vehicle, _intersectionsInside > 0, out var turnStartS, out var turningRight))
            {
                return;
            }

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
    }
}
