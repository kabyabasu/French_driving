namespace PermisVR.Exam
{
    /// A discrete thing that happened during the exam, carrying the vehicle state at that moment.
    public readonly struct ExamEvent
    {
        public readonly ExamEventType Type;
        public readonly string ZoneId;
        public readonly VehicleSnapshot Vehicle;

        public ExamEvent(ExamEventType type, VehicleSnapshot vehicle, string zoneId = null)
        {
            Type = type;
            Vehicle = vehicle;
            ZoneId = zoneId;
        }
    }
}
