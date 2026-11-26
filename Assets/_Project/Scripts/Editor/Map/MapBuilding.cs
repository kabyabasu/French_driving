using System;

namespace PermisVR.Editor.Map
{
    /// A building footprint (open ring, no repeated closing point) extruded to heightM.
    [Serializable]
    public sealed class MapBuilding
    {
        public long osmId;
        public MapPoint[] footprint;
        public float heightM;
    }
}
