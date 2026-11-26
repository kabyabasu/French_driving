namespace PermisVR.Exam
{
    /// Traffic light state carried in an ExamEvent, so rules never have to read the light itself.
    public enum SignalState
    {
        None,
        Green,
        Amber,
        Red,
    }
}
