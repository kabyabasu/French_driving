using System.Collections.Generic;

namespace PermisVR.Exam
{
    /// A graded exam: per-line scores, the total, the verdict and the faults that explain it.
    public sealed class ExamResult
    {
        public IReadOnlyList<CriterionScore> Scores { get; }
        public IReadOnlyList<Fault> Faults { get; }
        public int TotalPoints { get; }
        public int MaxTotalPoints { get; }
        public int PassThresholdPoints { get; }
        public bool HasEliminatoryFault { get; }

        /// Pass = at least the pass mark AND no eliminatory fault.
        public bool Passed => !HasEliminatoryFault && TotalPoints >= PassThresholdPoints;

        public ExamResult(IReadOnlyList<CriterionScore> scores, IReadOnlyList<Fault> faults, int totalPoints,
            int maxTotalPoints, int passThresholdPoints, bool hasEliminatoryFault)
        {
            Scores = scores;
            Faults = faults;
            TotalPoints = totalPoints;
            MaxTotalPoints = maxTotalPoints;
            PassThresholdPoints = passThresholdPoints;
            HasEliminatoryFault = hasEliminatoryFault;
        }
    }
}
