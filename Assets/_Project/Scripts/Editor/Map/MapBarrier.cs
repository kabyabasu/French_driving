using System;

namespace PermisVR.Editor.Map
{
    /// A wall, fence or hedge line, already clipped back from streets and sidewalks.
    [Serializable]
    public sealed class MapBarrier
    {
        public string type;
        public float heightM;
        public MapPoint[] points;
    }
}
