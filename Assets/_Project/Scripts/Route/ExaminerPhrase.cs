namespace PermisVR.Route
{
    /// The examiner's fixed set of spoken instructions (one pre-recorded clip each).
    /// Append new values at the end: scenes store clip arrays by index.
    public enum ExaminerPhrase
    {
        Depart,
        NextLeft,
        NextRight,
        SecondLeft,
        SecondRight,
        StraightOn,
        RoundaboutExit1,
        RoundaboutExit2,
        RoundaboutExit3,
        RoundaboutExit4,
        PullOver,
        ExamFinished,
        OffRoute,
    }
}
