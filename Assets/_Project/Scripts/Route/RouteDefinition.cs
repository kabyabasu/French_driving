using System;

namespace PermisVR.Route
{
    /// An exam route: centreline polyline in map metres (+X east, +Z north) with the junctions it
    /// passes and the maneuvers the examiner announces, both sorted by distance along the route.
    public sealed class RouteDefinition
    {
        private readonly float[] _x;
        private readonly float[] _z;
        private readonly float[] _s;

        public RouteDefinition(string id, float[] x, float[] z, RouteJunction[] junctions, RouteManeuver[] maneuvers)
        {
            if (x == null || z == null || x.Length != z.Length || x.Length < 2)
            {
                throw new ArgumentException("A route needs at least two points, with as many X as Z values.");
            }

            Id = id ?? "";
            _x = x;
            _z = z;
            _s = new float[x.Length];
            for (var i = 1; i < x.Length; i++)
            {
                var dx = x[i] - x[i - 1];
                var dz = z[i] - z[i - 1];
                _s[i] = _s[i - 1] + (float)Math.Sqrt(dx * dx + dz * dz);
            }

            Junctions = junctions ?? Array.Empty<RouteJunction>();
            Maneuvers = maneuvers ?? Array.Empty<RouteManeuver>();
            Array.Sort(Junctions, (a, b) => a.S.CompareTo(b.S));
            Array.Sort(Maneuvers, (a, b) => a.S.CompareTo(b.S));
        }

        public string Id { get; }
        public RouteJunction[] Junctions { get; }
        public RouteManeuver[] Maneuvers { get; }
        public int PointCount => _x.Length;
        public float LengthM => _s[_s.Length - 1];

        public float X(int i) => _x[i];
        public float Z(int i) => _z[i];
        public float S(int i) => _s[i];

        /// Distance along the route of the closest point to (x, z) on segments overlapping [fromS, toS],
        /// and the lateral distance to it.
        public bool Project(float x, float z, float fromS, float toS, out float s, out float lateralM)
        {
            s = 0f;
            lateralM = float.MaxValue;
            var found = false;
            for (var i = 1; i < _x.Length; i++)
            {
                if (_s[i] < fromS || _s[i - 1] > toS)
                {
                    continue;
                }

                var ax = _x[i - 1];
                var az = _z[i - 1];
                var dx = _x[i] - ax;
                var dz = _z[i] - az;
                var lengthSq = dx * dx + dz * dz;
                var t = lengthSq > 1e-6f ? ((x - ax) * dx + (z - az) * dz) / lengthSq : 0f;
                t = Math.Max(0f, Math.Min(1f, t));
                var px = ax + dx * t;
                var pz = az + dz * t;
                var distance = (float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                if (distance < lateralM)
                {
                    lateralM = distance;
                    s = _s[i - 1] + (_s[i] - _s[i - 1]) * t;
                    found = true;
                }
            }

            return found;
        }

        /// Point and heading (degrees, 0 = north, 90 = east) at distance s along the route.
        public void Sample(float s, out float x, out float z, out float headingDeg)
        {
            s = Math.Max(0f, Math.Min(LengthM, s));
            var i = 1;
            while (i < _s.Length - 1 && _s[i] < s)
            {
                i++;
            }

            var span = _s[i] - _s[i - 1];
            var t = span > 1e-6f ? (s - _s[i - 1]) / span : 0f;
            x = _x[i - 1] + (_x[i] - _x[i - 1]) * t;
            z = _z[i - 1] + (_z[i] - _z[i - 1]) * t;
            headingDeg = (float)(Math.Atan2(_x[i] - _x[i - 1], _z[i] - _z[i - 1]) * 180.0 / Math.PI);
        }
    }
}
