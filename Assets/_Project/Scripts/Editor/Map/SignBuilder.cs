using System.Collections.Generic;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// Road signs as merged geometry with the SignAtlas texture: a galvanised post, a plate shaped
    /// like the real sign (octagon, triangle, disc, panel) whose front maps onto its atlas cell, and
    /// a grey metal back. All signs of a tile share one mesh and one material (one draw call).
    /// Positions are the foot of the post; `travel` is the direction of the traffic the sign faces.
    internal static class SignBuilder
    {
        private const float PlateThicknessM = 0.02f;
        private const float PostRadiusM = 0.03f;
        private const int PostSides = 6;
        private const int DiscSides = 24;

        // Plate sizes (urban "petite gamme") and the height of the plate centre.
        private const float StopAcrossFlatsM = 0.7f;
        private const float YieldSideM = 0.8f;
        private const float SpeedDiameterM = 0.65f;
        private const float Zone30WidthM = 0.7f;
        private const float Zone30HeightM = 1.05f;
        private const float PlateCenterM = 2.5f;

        public static void AddStop(MeshBuilder b, Vector3 foot, Vector3 travel)
        {
            var radius = StopAcrossFlatsM * 0.5f / Mathf.Cos(Mathf.PI / 8f);
            var shape = new Vector2[8];
            var uv = new Vector2[8];
            for (var k = 0; k < 8; k++)
            {
                var a = Mathf.PI / 8f + k * Mathf.PI / 4f; // flat top
                shape[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                uv[k] = new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a));
            }

            AddPost(b, foot, PlateCenterM);
            AddPlate(b, foot, travel, PlateCenterM, shape, uv, SignAtlas.Stop);
        }

        public static void AddYield(MeshBuilder b, Vector3 foot, Vector3 travel)
        {
            // Match the atlas triangle: map its UV corners to metres, centred on the plate centre.
            var corners = SignAtlas.YieldCorners;
            var scale = YieldSideM / (corners[1].x - corners[0].x);
            var center = (corners[0] + corners[1] + corners[2]) / 3f;
            var shape = new Vector2[3];
            for (var k = 0; k < 3; k++)
            {
                shape[k] = (corners[k] - center) * scale;
            }

            AddPost(b, foot, PlateCenterM);
            AddPlate(b, foot, travel, PlateCenterM, shape, corners, SignAtlas.Yield);
        }

        /// B14 speed limit, or the zone 30 entry panel. False if there's no artwork for that limit.
        public static bool AddSpeedLimit(MeshBuilder b, Vector3 foot, Vector3 travel, int kmh, bool zoneEntry)
        {
            if (zoneEntry && kmh == 30)
            {
                var hw = Zone30WidthM * 0.5f;
                var hh = Zone30HeightM * 0.5f;
                var shape = new[] { new Vector2(-hw, -hh), new Vector2(hw, -hh), new Vector2(hw, hh), new Vector2(-hw, hh) };
                var uv = new[] { new Vector2(1f / 6f, 0f), new Vector2(5f / 6f, 0f), new Vector2(5f / 6f, 1f), new Vector2(1f / 6f, 1f) };
                AddPost(b, foot, PlateCenterM);
                AddPlate(b, foot, travel, PlateCenterM, shape, uv, SignAtlas.Zone30);
                return true;
            }

            var cell = SignAtlas.SpeedCell(kmh);
            if (cell < 0)
            {
                return false;
            }

            var disc = new Vector2[DiscSides];
            var discUv = new Vector2[DiscSides];
            for (var k = 0; k < DiscSides; k++)
            {
                var a = k * Mathf.PI * 2f / DiscSides;
                disc[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (SpeedDiameterM * 0.5f);
                discUv[k] = new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a));
            }

            AddPost(b, foot, PlateCenterM);
            AddPlate(b, foot, travel, PlateCenterM, disc, discUv, cell);
            return true;
        }

        /// Pole and lamp housing of a traffic light (the lamps themselves stay separate renderers).
        /// Housing centre is at `housingCenterM` above the foot; returns nothing, lamps go 1 cm in front.
        public static void AddTrafficLightBody(MeshBuilder b, Vector3 foot, Vector3 travel, float housingCenterM,
            Vector3 housingSize)
        {
            AddPost(b, foot, housingCenterM - housingSize.y * 0.5f);
            var right = MapGeometry.Right(travel);
            var center = foot + Vector3.up * housingCenterM;
            AddBox(b, center, right, travel, housingSize, SignAtlas.Housing);

            // Visor over each of the three lamps, sticking out towards the driver.
            var pitch = housingSize.y / 3f;
            for (var k = -1; k <= 1; k++)
            {
                var visorCenter = center + Vector3.up * (k * pitch + pitch * 0.42f) - travel * (housingSize.z * 0.5f + 0.06f);
                AddBox(b, visorCenter, right, travel, new Vector3(housingSize.x * 0.8f, 0.015f, 0.12f), SignAtlas.Housing);
            }
        }

        private static void AddPost(MeshBuilder b, Vector3 foot, float heightM)
        {
            var uv = SignAtlas.Uv(SignAtlas.Post, 0.5f, 0.5f);
            for (var k = 0; k < PostSides; k++)
            {
                var a0 = k * Mathf.PI * 2f / PostSides;
                var a1 = (k + 1) * Mathf.PI * 2f / PostSides;
                var r0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * PostRadiusM;
                var r1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * PostRadiusM;
                var top = Vector3.up * heightM;
                b.AddQuad(foot + r0, foot + r1, foot + r1 + top, foot + r0 + top, uv, uv, uv, uv, r0 + r1, White);
            }
        }

        /// Flat plate: front faces the approaching driver (-travel) with the shape's atlas cell;
        /// back and rim are grey metal. `shape` is in metres (x to the driver's right, y up).
        private static void AddPlate(MeshBuilder b, Vector3 foot, Vector3 travel, float centerM, Vector2[] shape,
            Vector2[] cellUv, int cell)
        {
            var right = MapGeometry.Right(travel);
            // In front of the post, so the post doesn't poke through the face.
            var center = foot + Vector3.up * centerM - travel * (PostRadiusM + PlateThicknessM * 0.5f);
            var front = -travel * (PlateThicknessM * 0.5f);
            var back = travel * (PlateThicknessM * 0.5f);
            var backUv = SignAtlas.Uv(SignAtlas.Back, 0.5f, 0.5f);
            var frontCenterUv = SignAtlas.Uv(cell, Average(cellUv).x, Average(cellUv).y);
            var shapeCenter = Average(shape);

            var points = new Vector3[shape.Length];
            for (var k = 0; k < shape.Length; k++)
            {
                points[k] = center + right * shape[k].x + Vector3.up * shape[k].y;
            }

            var middle = center + right * shapeCenter.x + Vector3.up * shapeCenter.y;
            for (var k = 0; k < shape.Length; k++)
            {
                var n = (k + 1) % shape.Length;
                b.AddTriangle(middle + front, points[k] + front, points[n] + front,
                    frontCenterUv, SignAtlas.Uv(cell, cellUv[k].x, cellUv[k].y), SignAtlas.Uv(cell, cellUv[n].x, cellUv[n].y),
                    -travel, White);
                b.AddTriangle(middle + back, points[k] + back, points[n] + back, backUv, backUv, backUv, travel, White);
                var outward = (points[k] + points[n]) * 0.5f - middle;
                b.AddQuad(points[k] + front, points[n] + front, points[n] + back, points[k] + back,
                    backUv, backUv, backUv, backUv, outward, White);
            }
        }

        private static void AddBox(MeshBuilder b, Vector3 center, Vector3 right, Vector3 forward, Vector3 size, int cell)
        {
            var uv = SignAtlas.Uv(cell, 0.5f, 0.5f);
            var x = right * (size.x * 0.5f);
            var y = Vector3.up * (size.y * 0.5f);
            var z = forward * (size.z * 0.5f);
            var axes = new[] { (x, y, z), (-x, y, z), (y, z, x), (-y, z, x), (z, x, y), (-z, x, y) };
            foreach (var (normal, u, v) in axes)
            {
                var c = center + normal;
                b.AddQuad(c - u - v, c + u - v, c + u + v, c - u + v, uv, uv, uv, uv, normal, White);
            }
        }

        private static Vector2 Average(IReadOnlyList<Vector2> points)
        {
            var sum = Vector2.zero;
            foreach (var p in points)
            {
                sum += p;
            }

            return sum / points.Count;
        }

        private static readonly Color32 White = new(255, 255, 255, 255);
    }
}
