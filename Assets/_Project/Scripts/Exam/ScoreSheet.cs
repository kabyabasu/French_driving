using System;
using System.Collections.Generic;

namespace PermisVR.Exam
{
    /// Collects faults raised by rule evaluators over the course of one exam run.
    public sealed class ScoreSheet
    {
        private readonly List<Fault> _faults = new();

        public IReadOnlyList<Fault> Faults => _faults;
        public bool HasEliminatoryFault { get; private set; }

        public event Action<Fault> FaultAdded;

        public void Watch(IExamRule rule)
        {
            rule.FaultRaised += OnFaultRaised;
        }

        private void OnFaultRaised(Fault fault)
        {
            _faults.Add(fault);
            if (fault.Severity == FaultSeverity.Eliminatory)
            {
                HasEliminatoryFault = true;
            }

            FaultAdded?.Invoke(fault);
        }
    }
}
