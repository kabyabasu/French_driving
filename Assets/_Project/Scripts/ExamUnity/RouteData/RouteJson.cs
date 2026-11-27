using System;

namespace PermisVR.ExamUnity.RouteData
{
    /// JSON shape of Data/Routes/<id>_route.json (written by tools/osm/build_route.py), for JsonUtility.
    [Serializable]
    public sealed class RouteJson
    {
        public string id;
        public string name;
        public string source;
        public float lengthM;
        public RoutePointJson[] points;
        public RouteJunctionJson[] junctions;
        public RouteManeuverJson[] maneuvers;
    }
}
