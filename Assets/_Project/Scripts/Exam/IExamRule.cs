using System;

namespace PermisVR.Exam
{
    /// One scored exam rule. Rules never read scene objects: they only receive ExamEvents.
    public interface IExamRule
    {
        string RuleId { get; }

        event Action<Fault> FaultRaised;

        void HandleEvent(in ExamEvent evt);
    }
}
