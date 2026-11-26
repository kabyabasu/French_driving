using System.Collections.Generic;

namespace PermisVR.Exam
{
    /// Turns recorded faults into CEPC grid scores. Scored lines start at their maximum and lose
    /// MinorFaultPenaltyPoints per minor fault (floor 0); an eliminatory fault marks its line E
    /// and fails the exam. Bonus lines start at 0: they are earned, not deducted.
    public static class CepcGrader
    {
        public static ExamResult Grade(CepcGrid grid, IReadOnlyList<Fault> faults)
        {
            var minorFaultsById = new Dictionary<string, int>();
            var eliminatedIds = new HashSet<string>();
            var hasEliminatoryFault = false;

            foreach (var fault in faults)
            {
                if (fault.Severity == FaultSeverity.Eliminatory)
                {
                    hasEliminatoryFault = true;
                    eliminatedIds.Add(fault.Criterion);
                    continue;
                }

                minorFaultsById.TryGetValue(fault.Criterion, out var count);
                minorFaultsById[fault.Criterion] = count + 1;
            }

            var scores = new List<CriterionScore>(grid.Criteria.Count);
            var total = 0;
            foreach (var criterion in grid.Criteria)
            {
                minorFaultsById.TryGetValue(criterion.Id, out var minorFaults);
                var eliminated = eliminatedIds.Contains(criterion.Id);
                int points;
                if (criterion.IsBonus)
                {
                    points = 0;
                }
                else if (eliminated)
                {
                    points = 0;
                }
                else
                {
                    points = criterion.MaxPoints - minorFaults * grid.MinorFaultPenaltyPoints;
                    if (points < 0)
                    {
                        points = 0;
                    }
                }

                total += points;
                scores.Add(new CriterionScore(criterion, points, eliminated, minorFaults));
            }

            return new ExamResult(scores, new List<Fault>(faults), total, grid.MaxTotalPoints,
                grid.PassThresholdPoints, hasEliminatoryFault);
        }
    }
}
