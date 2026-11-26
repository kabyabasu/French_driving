using System;

namespace PermisVR.Editor.Map
{
    /// The geocoded exam start point that the map is centred on (world origin).
    [Serializable]
    public sealed class MapOrigin
    {
        public string address;
        public double lat;
        public double lon;
        public float radiusM;
    }
}
