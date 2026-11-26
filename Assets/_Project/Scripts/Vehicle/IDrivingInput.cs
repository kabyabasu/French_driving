namespace PermisVR.Vehicle
{
    /// The only contract Vehicle code reads. Adding a wheel/pedals later means a new
    /// implementation of this interface, never a change inside Vehicle/.
    public interface IDrivingInput
    {
        /// -1 (left) to +1 (right).
        float Steer { get; }

        /// 0 to 1.
        float Throttle { get; }

        /// 0 to 1.
        float Brake { get; }

        /// True once per gear button press; the car decides whether the change is allowed.
        bool TryConsumeGearRequest(out Gear gear);
    }
}
