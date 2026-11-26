using System.Collections.Generic;

namespace PermisVR.Exam
{
    /// The grading grid: its lines, the pass mark and how much a minor fault costs.
    public sealed class CepcGrid
    {
        public IReadOnlyList<CriterionDefinition> Criteria { get; }
        public int PassThresholdPoints { get; }
        public int MinorFaultPenaltyPoints { get; }
        public int MaxTotalPoints { get; }

        public CepcGrid(IReadOnlyList<CriterionDefinition> criteria, int passThresholdPoints, int minorFaultPenaltyPoints)
        {
            Criteria = criteria;
            PassThresholdPoints = passThresholdPoints;
            MinorFaultPenaltyPoints = minorFaultPenaltyPoints;

            var total = 0;
            foreach (var criterion in criteria)
            {
                total += criterion.MaxPoints;
            }

            MaxTotalPoints = total;
        }
    }
}
