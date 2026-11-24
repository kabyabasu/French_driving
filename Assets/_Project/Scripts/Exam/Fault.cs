namespace PermisVR.Exam
{
    public enum FaultSeverity
    {
        Minor,
        Eliminatory,
    }

    /// One recorded exam fault: which rule, where and when it happened, and why in French.
    public readonly struct Fault
    {
        public readonly string RuleId;
        public readonly string Criterion;
        public readonly FaultSeverity Severity;
        public readonly float TimeS;
        public readonly float RoutePositionM;
        public readonly string ExplanationFr;

        public Fault(string ruleId, string criterion, FaultSeverity severity, float timeS, float routePositionM, string explanationFr)
        {
            RuleId = ruleId;
            Criterion = criterion;
            Severity = severity;
            TimeS = timeS;
            RoutePositionM = routePositionM;
            ExplanationFr = explanationFr;
        }
    }
}
