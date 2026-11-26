using System;

namespace PermisVR.Exam.Rules
{
    /// French rule: check the interior mirror before slowing down firmly, so you know what is
    /// behind you. Missing it is a minor fault ("Prendre l'information").
    public sealed class MirrorBeforeBrakingRule : IExamRule
    {
        public string RuleId => "mirror_before_braking";
        public event Action<Fault> FaultRaised;

        private readonly MirrorRulesConfig _config;
        private readonly MirrorCheckHistory _history = new();
        private float _previousTimeS = float.NaN;
        private float _previousSpeedKmh;
        private float _smoothedDecelMs2;
        private float _brakingSinceS = float.NaN;
        private float _brakingStartSpeedKmh;
        private bool _judged;

        public MirrorBeforeBrakingRule(MirrorRulesConfig config)
        {
            _config = config;
        }

        public void HandleEvent(in ExamEvent evt)
        {
            if (evt.Type == ExamEventType.MirrorChecked)
            {
                _history.Record(evt.ZoneId, evt.Vehicle.TimeS);
            }
            else if (evt.Type == ExamEventType.VehicleTick)
            {
                OnTick(evt.Vehicle);
            }
        }

        private void OnTick(in VehicleSnapshot vehicle)
        {
            if (float.IsNaN(_previousTimeS))
            {
                _previousTimeS = vehicle.TimeS;
                _previousSpeedKmh = vehicle.SpeedKmh;
                return;
            }

            var dt = vehicle.TimeS - _previousTimeS;
            if (dt <= 0f)
            {
                return;
            }

            // Smoothed because per-tick physics speed is noisy.
            var rawDecelMs2 = (_previousSpeedKmh - vehicle.SpeedKmh) / 3.6f / dt;
            _smoothedDecelMs2 += (rawDecelMs2 - _smoothedDecelMs2) * (dt / (_config.DecelSmoothingS + dt));
            var speedBeforeKmh = _previousSpeedKmh;
            _previousTimeS = vehicle.TimeS;
            _previousSpeedKmh = vehicle.SpeedKmh;

            if (_smoothedDecelMs2 < _config.BrakingDecelMs2)
            {
                _brakingSinceS = float.NaN;
                _judged = false;
                return;
            }

            if (float.IsNaN(_brakingSinceS))
            {
                _brakingSinceS = vehicle.TimeS - dt;
                _brakingStartSpeedKmh = speedBeforeKmh;
            }

            var settleS = Math.Max(_config.BrakingMinDurationS, _config.GraceS);
            if (_judged || vehicle.TimeS - _brakingSinceS < settleS)
            {
                return;
            }

            _judged = true;
            if (_brakingStartSpeedKmh < _config.BrakingMinSpeedKmh)
            {
                return;
            }

            if (!_history.CheckedWithin(MirrorIds.Interior, _brakingSinceS - _config.LookbackS, _brakingSinceS + _config.GraceS))
            {
                FaultRaised?.Invoke(Fault.At(vehicle, RuleId, ExamCriteria.TakeInformation, FaultSeverity.Minor,
                    "Rétroviseur intérieur non contrôlé avant de freiner"));
            }
        }
    }
}
