using System;

namespace PermisVR.Editor.Map
{
    /// One tree: mapped in OSM, on a mapped tree row, or scattered inside a mapped wood/park.
    [Serializable]
    public sealed class MapTree
    {
        public MapPoint position;
        public float heightM;
        public float crownRadiusM;
    }
}
