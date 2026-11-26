using System;

namespace PermisVR.Editor.Map
{
    /// A graph node where 3+ streets meet (candidate priorité à droite junction).
    [Serializable]
    public sealed class MapIntersection
    {
        public long osmId;
        public MapPoint position;
        public int streetCount;
        public string highwayTag;
    }
}
