namespace PermisVR.Exam
{
    public enum ExamEventType
    {
        /// Periodic vehicle-state sample; carries no payload beyond the VehicleSnapshot.
        VehicleTick,

        StopZoneEnter,
        StopZoneExit,
    }
}
