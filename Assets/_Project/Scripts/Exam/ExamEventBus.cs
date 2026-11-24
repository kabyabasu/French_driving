using System;

namespace PermisVR.Exam
{
    /// Fan-out point between sensors (ExamUnity) and rule evaluators (Exam).
    public sealed class ExamEventBus
    {
        public event Action<ExamEvent> EventRaised;

        public void Publish(in ExamEvent evt)
        {
            EventRaised?.Invoke(evt);
        }
    }
}
