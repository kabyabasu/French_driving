using System;
using System.Collections.Generic;

namespace PermisVR.Exam.Rules
{
    /// French STOP rule: the vehicle must come to a stop (speed near zero) inside the stop
    /// zone before the stop line. Failing to do so is a faute eliminatoire.
    public sealed class StopSignRule : IExamRule
    {
        public string RuleId => "stop_sign";
        public event Action<Fault> FaultRaised;

        private readonly StopSignRuleConfig _config;
        private readonly Dictionary<string, bool> _stoppedByZoneId = new();

        public StopSignRule(StopSignRuleConfig config)
        {
            _config = config;
        }

        public void HandleEvent(in ExamEvent evt)
        {
            switch (evt.Type)
            {
                case ExamEventType.StopZoneEnter:
                    _stoppedByZoneId[evt.ZoneId] = false;
                    break;

                case ExamEventType.VehicleTick:
                    MarkStoppedZones(evt.Vehicle);
                    break;

                case ExamEventType.StopZoneExit:
                    HandleZoneExit(evt);
                    break;
            }
        }

        private void MarkStoppedZones(in VehicleSnapshot vehicle)
        {
            if (vehicle.SpeedKmh > _config.SpeedThresholdKmh)
            {
                return;
            }

            // A VehicleTick is global, not per zone: only zones currently entered are tracked here.
            foreach (var zoneId in new List<string>(_stoppedByZoneId.Keys))
            {
                _stoppedByZoneId[zoneId] = true;
            }
        }

        private void HandleZoneExit(in ExamEvent evt)
        {
            if (!_stoppedByZoneId.TryGetValue(evt.ZoneId, out var stopped))
            {
                return;
            }

            if (!stopped)
            {
                FaultRaised?.Invoke(new Fault(
                    RuleId,
                    criterion: "controle_vehicule",
                    FaultSeverity.Eliminatory,
                    evt.Vehicle.TimeS,
                    evt.Vehicle.RoutePositionM,
                    explanationFr: "Arrêt non marqué au STOP"));
            }

            _stoppedByZoneId.Remove(evt.ZoneId);
        }
    }
}
