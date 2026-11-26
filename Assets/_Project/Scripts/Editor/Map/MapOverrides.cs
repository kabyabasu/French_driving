using System;

namespace PermisVR.Editor.Map
{
    /// Root of argenteuil_overrides.json: manual fixes applied after the OSM data.
    [Serializable]
    public sealed class MapOverrides
    {
        public long[] removeSignOsmIds = Array.Empty<long>();
        public MapSign[] addSigns = Array.Empty<MapSign>();
        public RoadOverride[] roadOverrides = Array.Empty<RoadOverride>();
    }
}
