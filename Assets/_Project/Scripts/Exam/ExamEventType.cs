namespace PermisVR.Exam
{
    public enum ExamEventType
    {
        /// Periodic vehicle-state sample; carries no payload beyond the VehicleSnapshot.
        VehicleTick,

        StopZoneEnter,

        /// The vehicle's front crossed the STOP line moving forward.
        StopZoneExit,

        /// The vehicle's front axle crossed a traffic-light stop line moving forward; carries the light state.
        StopLineCrossed,
    }
}
