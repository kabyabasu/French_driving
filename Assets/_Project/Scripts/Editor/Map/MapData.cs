using System;

namespace PermisVR.Editor.Map
{
    /// Root of argenteuil_map.json, written by tools/osm/build_map.py.
    [Serializable]
    public sealed class MapData
    {
        public MapOrigin origin;
        public MapRoad[] roads;
        public MapIntersection[] intersections;
        public MapBuilding[] buildings;
        public MapSign[] signs;
    }
}
