using System;

namespace PermisVR.Editor.Map
{
    /// A stretch of drivable street that passes under something (OSM tunnel segment, or a
    /// railway/road/foot bridge crossing it). The importer roofs it with a deck on side walls.
    [Serializable]
    public sealed class MapUnderpass
    {
        public MapPoint[] points;
        public float roadWidthM;
        /// Railway or motorway overhead: steel girder faces (orange) instead of concrete.
        public bool steel;
        /// "tunnel" or "bridge": which OSM tagging produced it (for debugging).
        public string source;
    }
}
