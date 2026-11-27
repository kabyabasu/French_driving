using System.Collections.Generic;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// Where speed-limit signs go: at the start of every road whose OSM maxspeed differs from a road
    /// that leads into it, facing the traffic entering it. Only tagged limits get a sign (an
    /// untagged road is the 50 km/h town default and shows nothing). One-way roads are entered at
    /// their first point (build_map exports them in travel direction). Motorways are left out.
    internal static class SpeedSignPlanner
    {
        public struct Placement
        {
            public Vector3 Foot;
            public Vector3 Travel;
            public int Kmh;
            public bool ZoneEntry;
        }

        private const int TownDefaultKmh = 50;
        private const float SetbackM = 10f;         // from the junction or change point
        private const float MinRoadLengthM = 20f;
        private const float DuplicateWithinM = 15f;

        private static readonly HashSet<string> Skipped = new() { "motorway", "motorway_link" };

        public static List<Placement> Plan(MapRoad[] roads, float edgeOffsetM)
        {
            var ends = new Dictionary<Vector2Int, List<(MapRoad road, bool atStart)>>();
            foreach (var road in roads)
            {
                if (road.points == null || road.points.Length < 2 || Skipped.Contains(road.highway))
                {
                    continue;
                }

                Add(ends, Key(road.points[0]), (road, true));
                Add(ends, Key(road.points[road.points.Length - 1]), (road, false));
            }

            var placements = new List<Placement>();
            foreach (var road in roads)
            {
                if (road.points == null || road.points.Length < 2 || Skipped.Contains(road.highway) || road.maxspeedKmh <= 0)
                {
                    continue;
                }

                var points = new List<Vector3>();
                foreach (var p in road.points)
                {
                    points.Add(p.ToVector3(0f));
                }

                if (MapGeometry.Length(points) < MinRoadLengthM)
                {
                    continue;
                }

                for (var end = 0; end < 2; end++)
                {
                    var atStart = end == 0;
                    if (road.oneWay && !atStart)
                    {
                        continue; // a one-way road is only entered at its first point
                    }

                    var node = Key(atStart ? road.points[0] : road.points[road.points.Length - 1]);
                    if (!EnteredFromDifferentLimit(ends[node], road))
                    {
                        continue;
                    }

                    var path = atStart ? points : Reversed(points);
                    var foot = MapGeometry.PointAt(path, SetbackM);
                    var ahead = MapGeometry.PointAt(path, SetbackM + 1f) - foot;
                    ahead.y = 0f;
                    if (ahead.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }

                    var travel = ahead.normalized;
                    foot += MapGeometry.Right(travel) * (road.widthM * 0.5f + edgeOffsetM);
                    if (IsDuplicate(placements, foot, travel, road.maxspeedKmh))
                    {
                        continue;
                    }

                    placements.Add(new Placement
                    {
                        Foot = foot,
                        Travel = travel,
                        Kmh = road.maxspeedKmh,
                        ZoneEntry = road.maxspeedKmh == 30,
                    });
                }
            }

            return placements;
        }

        /// True if some other road arriving at this node (one-ways only at their last point) has a different limit.
        private static bool EnteredFromDifferentLimit(List<(MapRoad road, bool atStart)> atNode, MapRoad entered)
        {
            foreach (var (other, otherAtStart) in atNode)
            {
                if (ReferenceEquals(other, entered) || (other.oneWay && otherAtStart))
                {
                    continue;
                }

                var limit = other.maxspeedKmh > 0 ? other.maxspeedKmh : TownDefaultKmh;
                if (limit != entered.maxspeedKmh)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDuplicate(List<Placement> placements, Vector3 foot, Vector3 travel, int kmh)
        {
            foreach (var p in placements)
            {
                if (p.Kmh == kmh && Vector3.Dot(p.Travel, travel) > 0.7f && Vector3.Distance(p.Foot, foot) < DuplicateWithinM)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<Vector3> Reversed(List<Vector3> points)
        {
            var copy = new List<Vector3>(points);
            copy.Reverse();
            return copy;
        }

        private static Vector2Int Key(MapPoint p) => new(Mathf.RoundToInt(p.x * 100f), Mathf.RoundToInt(p.z * 100f));

        private static void Add(Dictionary<Vector2Int, List<(MapRoad, bool)>> ends, Vector2Int key, (MapRoad, bool) value)
        {
            if (!ends.TryGetValue(key, out var list))
            {
                list = new List<(MapRoad, bool)>();
                ends[key] = list;
            }

            list.Add(value);
        }
    }
}
