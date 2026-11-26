using System;

namespace PermisVR.Editor.Map
{
    /// A sign/marking node: type is the OSM highway tag (stop, give_way, traffic_signals, crossing).
    [Serializable]
    public sealed class MapSign
    {
        public long osmId;
        public string type;
        public string examRule;
        public MapPoint position;
        public string direction;
    }
}
