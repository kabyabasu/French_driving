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

        /// The driver looked at a mirror; ZoneId is a MirrorIds value.
        MirrorChecked,

        /// The vehicle entered/left a junction area (several can overlap).
        IntersectionEnter,
        IntersectionExit,

        /// The driver turned their head over a shoulder (angle mort); ZoneId is a BlindSpotIds value.
        /// Appended last: sensors store this enum by index in scenes.
        BlindSpotChecked,
    }
}
