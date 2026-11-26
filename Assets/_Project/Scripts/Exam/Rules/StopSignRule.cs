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

        // Reused collections: HandleEvent runs on every VehicleTick and must not allocate.
        private readonly List<string> _activeZoneIds = new();
        private readonly HashSet<string> _stoppedZoneIds = new();

        public StopSignRule(StopSignRuleConfig config)
        {
            _config = config;
        }

        public void HandleEvent(in ExamEvent evt)
        {
            switch (evt.Type)
            {
                case ExamEventType.StopZoneEnter:
                    if (!_activeZoneIds.Contains(evt.ZoneId))
                    {
                        _activeZoneIds.Add(evt.ZoneId);
                    }

                    _stoppedZoneIds.Remove(evt.ZoneId);
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

            // A VehicleTick is global, not per zone: every zone currently entered counts as stopped.
            for (var i = 0; i < _activeZoneIds.Count; i++)
            {
                _stoppedZoneIds.Add(_activeZoneIds[i]);
            }
        }

        private void HandleZoneExit(in ExamEvent evt)
        {
            if (!_activeZoneIds.Remove(evt.ZoneId))
            {
                return;
            }

            if (!_stoppedZoneIds.Remove(evt.ZoneId))
            {
                FaultRaised?.Invoke(Fault.At(evt.Vehicle, RuleId, ExamCriteria.ReadTheRoad,
                    FaultSeverity.Eliminatory, "Arrêt non marqué au STOP"));
            }
        }
    }
}
