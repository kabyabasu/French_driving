namespace PermisVR.Exam
{
    /// Recent mirror checks in a fixed ring buffer (no allocation after construction), so a rule
    /// can ask "was this mirror checked between two times?" after the maneuver is recognised.
    public sealed class MirrorCheckHistory
    {
        private const int Capacity = 64;

        private readonly string[] _ids = new string[Capacity];
        private readonly float[] _times = new float[Capacity];
        private int _next;
        private int _count;

        public void Record(string mirrorId, float timeS)
        {
            _ids[_next] = mirrorId;
            _times[_next] = timeS;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity)
            {
                _count++;
            }
        }

        public bool CheckedWithin(string mirrorId, float fromS, float toS)
        {
            for (var i = 0; i < _count; i++)
            {
                if (_ids[i] == mirrorId && _times[i] >= fromS && _times[i] <= toS)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
