using System;

namespace PermisVR.Editor.Map
{
    /// Manual correction for one road; 0 leaves that field as OSM has it.
    [Serializable]
    public sealed class RoadOverride
    {
        public long osmId;
        public int maxspeedKmh;
        public int lanes;
    }
}
