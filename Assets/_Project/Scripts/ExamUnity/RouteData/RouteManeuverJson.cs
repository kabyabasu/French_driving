using System;

namespace PermisVR.ExamUnity.RouteData
{
    [Serializable]
    public sealed class RouteManeuverJson
    {
        public float s;
        public float x;
        public float z;
        public string type;
        public int exit;
        public string street;
    }
}
