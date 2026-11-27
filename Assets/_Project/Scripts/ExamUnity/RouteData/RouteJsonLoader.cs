using System;
using PermisVR.Route;
using UnityEngine;

namespace PermisVR.ExamUnity.RouteData
{
    /// Reads a route JSON file into a RouteDefinition.
    public static class RouteJsonLoader
    {
        public static RouteDefinition Load(string json, out RouteJson raw)
        {
            raw = JsonUtility.FromJson<RouteJson>(json);
            if (raw == null || raw.points == null || raw.points.Length < 2)
            {
                throw new ArgumentException("Route JSON has no points.");
            }

            var x = new float[raw.points.Length];
            var z = new float[raw.points.Length];
            for (var i = 0; i < raw.points.Length; i++)
            {
                x[i] = raw.points[i].x;
                z[i] = raw.points[i].z;
            }

            var junctionsJson = raw.junctions ?? Array.Empty<RouteJunctionJson>();
            var junctions = new RouteJunction[junctionsJson.Length];
            for (var i = 0; i < junctions.Length; i++)
            {
                junctions[i] = new RouteJunction(junctionsJson[i].s, junctionsJson[i].left, junctionsJson[i].right);
            }

            var maneuversJson = raw.maneuvers ?? Array.Empty<RouteManeuverJson>();
            var maneuvers = new RouteManeuver[maneuversJson.Length];
            for (var i = 0; i < maneuvers.Length; i++)
            {
                var m = maneuversJson[i];
                maneuvers[i] = new RouteManeuver(m.s, ParseType(m.type), m.exit, m.street);
            }

            return new RouteDefinition(raw.id, x, z, junctions, maneuvers);
        }

        private static ManeuverType ParseType(string type)
        {
            switch (type)
            {
                case "left": return ManeuverType.Left;
                case "right": return ManeuverType.Right;
                case "straight": return ManeuverType.Straight;
                case "roundabout": return ManeuverType.Roundabout;
                default: throw new ArgumentException($"Unknown maneuver type '{type}' in route JSON.");
            }
        }
    }
}
