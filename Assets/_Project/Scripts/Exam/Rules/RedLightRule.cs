using System;

namespace PermisVR.Exam.Rules
{
    /// French red-light rule: the front axle crossing the stop line while the light is red is a
    /// faute eliminatoire. Amber is not judged yet: that needs stopping-distance logic.
    public sealed class RedLightRule : IExamRule
    {
        public string RuleId => "red_light";
        public event Action<Fault> FaultRaised;

        public void HandleEvent(in ExamEvent evt)
        {
            if (evt.Type != ExamEventType.StopLineCrossed || evt.Signal != SignalState.Red)
            {
                return;
            }

            FaultRaised?.Invoke(Fault.At(evt.Vehicle, RuleId, ExamCriteria.ApplyRegulations,
                FaultSeverity.Eliminatory, "Franchissement d'un feu rouge"));
        }
    }
}
