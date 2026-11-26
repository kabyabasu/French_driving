namespace PermisVR.Exam
{
    /// One line of the CEPC grid: scored 0..MaxPoints (or E), or a bonus earned 0..MaxPoints.
    /// IsEvaluated is false while the game has no detector for it yet.
    public sealed class CriterionDefinition
    {
        public string Id { get; }
        public string LabelFr { get; }
        public string FamilyFr { get; }
        public int MaxPoints { get; }
        public bool IsBonus { get; }
        public bool IsEvaluated { get; }

        public CriterionDefinition(string id, string labelFr, string familyFr, int maxPoints, bool isBonus, bool isEvaluated)
        {
            Id = id;
            LabelFr = labelFr;
            FamilyFr = familyFr;
            MaxPoints = maxPoints;
            IsBonus = isBonus;
            IsEvaluated = isEvaluated;
        }
    }
}
