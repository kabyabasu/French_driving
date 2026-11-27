namespace PermisVR.Exam
{
    public enum FaultSeverity
    {
        Minor,
        Eliminatory,
    }

    /// One recorded exam fault: which rule, where and when it happened, and why in French.
    /// Position is map metres from the start point; RoutePositionM is the distance along the exam route (0 without one).
    public readonly struct Fault
    {
        public readonly string RuleId;
        public readonly string Criterion;
        public readonly FaultSeverity Severity;
        public readonly float TimeS;
        public readonly float RoutePositionM;
        public readonly string ExplanationFr;
        public readonly float PositionX;
        public readonly float PositionZ;

        public Fault(string ruleId, string criterion, FaultSeverity severity, float timeS, float routePositionM,
            string explanationFr, float positionX = 0f, float positionZ = 0f)
        {
            RuleId = ruleId;
            Criterion = criterion;
            Severity = severity;
            TimeS = timeS;
            RoutePositionM = routePositionM;
            ExplanationFr = explanationFr;
            PositionX = positionX;
            PositionZ = positionZ;
        }

        public static Fault At(in VehicleSnapshot vehicle, string ruleId, string criterion, FaultSeverity severity,
            string explanationFr)
        {
            return new Fault(ruleId, criterion, severity, vehicle.TimeS, vehicle.RoutePositionM, explanationFr,
                vehicle.PositionX, vehicle.PositionZ);
        }
    }
}
