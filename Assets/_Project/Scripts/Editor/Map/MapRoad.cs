using System;

namespace PermisVR.Editor.Map
{
    /// One OSM street segment between two graph nodes. maxspeedKmh 0 = not tagged in OSM.
    [Serializable]
    public sealed class MapRoad
    {
        public long osmId;
        public string name;
        public string highway;
        public MapPoint[] points;
        public int lanes;
        public float widthM;
        public bool oneWay;
        public int maxspeedKmh;
    }
}
