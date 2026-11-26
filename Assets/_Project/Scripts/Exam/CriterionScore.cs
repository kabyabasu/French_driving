namespace PermisVR.Exam
{
    /// The result for one grid line: points (0 when eliminated) and the faults behind it.
    public readonly struct CriterionScore
    {
        public readonly CriterionDefinition Definition;
        public readonly int Points;
        public readonly bool IsEliminated;
        public readonly int MinorFaultCount;

        public CriterionScore(CriterionDefinition definition, int points, bool isEliminated, int minorFaultCount)
        {
            Definition = definition;
            Points = points;
            IsEliminated = isEliminated;
            MinorFaultCount = minorFaultCount;
        }
    }
}
