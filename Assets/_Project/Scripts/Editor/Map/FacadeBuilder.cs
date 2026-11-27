using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// Extrudes building footprints with data for the PermisVR/Facade shader: window bays along each
    /// wall, a style (house, shops, flats, house with garage), a render colour, and pitched tiled
    /// roofs on small rectangular houses. Purely visual: no exam rule reads buildings.
    ///
    /// OSM has no heights here (almost every building is the 12 m default), so storeys are guessed
    /// from the footprint: small = pavillon, larger = flats, very large = hall. Tagged heights win.
    internal static class FacadeBuilder
    {
        private const float StoreyM = MapTextureLibrary.FacadeBayM;
        private const float DefaultHeightM = 12f;          // build_map.py's default (4 levels): means "unknown"
        private const float MinWindowWallM = 2.2f;         // shorter walls are blind
        private const float AnnexMaxAreaM2 = 30f;           // garden shed / garage: one storey
        private const float PavillonMaxAreaM2 = 150f;
        private const float SmallBlockMaxAreaM2 = 400f;
        private const float BlockMaxAreaM2 = 1500f;
        private const float HallHeightM = 7f;
        private const float ShopRoadDistanceM = 8f;        // wall this close to a main road's edge: shops
        private const float RectangularRatio = 0.85f;      // footprint area / bounding rectangle area
        private const float MaxGableSpanM = 14f;
        private const float RoofPitchRatio = 0.35f;        // ridge height / span (~35°)
        private const float EaveOverhangM = 0.3f;

        private static readonly HashSet<string> MainRoads = new() { "primary", "secondary", "tertiary", "primary_link", "secondary_link" };

        private const int StyleHouse = 0, StyleShops = 1, StyleFlats = 2, StyleGarage = 3;

        private struct Segment
        {
            public Vector2 A, B;
            public float HalfWidth;
        }

        public static int Build(MapData data, System.Func<Vector3, MeshBuilder> tileFor, float minAreaM2)
        {
            var mainRoads = MainRoadSegments(data.roads);
            var count = 0;
            foreach (var building in data.buildings)
            {
                if (building.footprint == null || building.footprint.Length < 3)
                {
                    continue;
                }

                var ring = building.footprint.Select(p => new Vector2(p.x, p.z)).ToList();
                var area = MapGeometry.SignedArea(ring);
                if (Mathf.Abs(area) < minAreaM2)
                {
                    continue;
                }

                var random = new System.Random(unchecked((int)(building.osmId ^ (building.osmId >> 32))));
                var centroid = ring.Aggregate(Vector2.zero, (sum, p) => sum + p) / ring.Count;
                var builder = tileFor(new Vector3(centroid.x, 0f, centroid.y));
                BuildOne(builder, ring, area, building.heightM, random, mainRoads);
                count++;
            }

            return count;
        }

        private static void BuildOne(MeshBuilder builder, List<Vector2> ring, float signedArea, float taggedHeightM,
            System.Random random, List<Segment> mainRoads)
        {
            var area = Mathf.Abs(signedArea);
            var unknownHeight = taggedHeightM <= 0f || Mathf.Approximately(taggedHeightM, DefaultHeightM);
            float height;
            bool hall = false;
            if (!unknownHeight)
            {
                height = taggedHeightM;
            }
            else if (area < AnnexMaxAreaM2)
            {
                height = StoreyM;
            }
            else if (area < PavillonMaxAreaM2)
            {
                height = 2 * StoreyM;
            }
            else if (area < SmallBlockMaxAreaM2)
            {
                height = 3 * StoreyM;
            }
            else if (area < BlockMaxAreaM2)
            {
                height = (4 + random.Next(2)) * StoreyM;
            }
            else
            {
                height = HallHeightM;
                hall = true;
            }

            var levels = Mathf.Max(1, Mathf.RoundToInt(height / StoreyM));
            var onMainRoad = NearMainRoad(ring, mainRoads);
            int style;
            if (unknownHeight && area < AnnexMaxAreaM2)
            {
                style = StyleGarage;
            }
            else if (levels <= 2)
            {
                style = onMainRoad && random.NextDouble() < 0.5 ? StyleShops : random.NextDouble() < 0.3 ? StyleGarage : StyleHouse;
            }
            else
            {
                style = onMainRoad ? StyleShops : levels >= 4 ? StyleFlats : StyleHouse;
            }

            // Wall render colour (brick is rarer) and roof colour (tiles on pitched, greys on flat).
            var tint = random.NextDouble() < 0.08 ? 6 : new[] { 0, 1, 2, 3, 4, 5, 7 }[random.Next(7)];
            if (hall)
            {
                tint = 2;
            }

            var counterclockwise = signedArea > 0f;
            for (var i = 0; i < ring.Count; i++)
            {
                var p = ring[i];
                var q = ring[(i + 1) % ring.Count];
                var edge = q - p;
                var length = edge.magnitude;
                if (length < 1e-2f)
                {
                    continue;
                }

                var outward = counterclockwise ? new Vector3(edge.y, 0f, -edge.x) : new Vector3(-edge.y, 0f, edge.x);
                var windows = !hall && length >= MinWindowWallM;
                var bays = windows ? Mathf.Max(1, Mathf.RoundToInt(length / StoreyM)) : 0;
                var u0 = windows ? random.Next(8) : 0; // different door positions on each wall
                var color = WallColor(tint, style, windows);
                builder.AddQuad(
                    new Vector3(p.x, 0f, p.y), new Vector3(p.x, height, p.y), new Vector3(q.x, height, q.y), new Vector3(q.x, 0f, q.y),
                    new Vector2(u0, 0f), new Vector2(u0, height), new Vector2(u0 + bays, height), new Vector2(u0 + bays, 0f),
                    outward, color);
            }

            if (levels <= 2 && !hall && TryGableRoof(builder, ring, area, height, tint, random))
            {
                return;
            }

            var roofColor = RoofColor(new[] { 1, 3, 5, 7 }[random.Next(4)]);
            var roof = MapGeometry.Triangulate(ring);
            for (var t = 0; t < roof.Count; t += 3)
            {
                builder.AddTriangle(At(ring[roof[t]], height), At(ring[roof[t + 1]], height), At(ring[roof[t + 2]], height),
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector3.up, roofColor);
            }
        }

        /// Gable roof over the footprint's bounding rectangle, ridge along its long side, when the
        /// footprint is nearly that rectangle and narrow enough for a house roof.
        private static bool TryGableRoof(MeshBuilder builder, List<Vector2> ring, float area, float height, int tint, System.Random random)
        {
            if (!MinAreaRectangle(ring, out var center, out var axisLong, out var halfLong, out var halfShort)
                || area / (4f * halfLong * halfShort) < RectangularRatio || 2f * halfShort > MaxGableSpanM)
            {
                return false;
            }

            var axisShort = new Vector2(-axisLong.y, axisLong.x);
            var ridge = height + 2f * halfShort * RoofPitchRatio;
            var eaveHalf = halfShort + EaveOverhangM;
            var eaveDrop = EaveOverhangM * (ridge - height) / halfShort;
            var roofColor = RoofColor(random.NextDouble() < 0.8 ? new[] { 0, 2, 4, 6 }[random.Next(4)] : new[] { 1, 3 }[random.Next(2)]);
            var gableColor = WallColor(tint, StyleHouse, false);

            Vector3 P(float along, float across, float y)
            {
                var v = center + axisLong * along + axisShort * across;
                return new Vector3(v.x, y, v.y);
            }

            var l = halfLong + EaveOverhangM;
            for (var side = -1; side <= 1; side += 2)
            {
                var a = P(-l, side * eaveHalf, height - eaveDrop);
                var b = P(l, side * eaveHalf, height - eaveDrop);
                var c = P(l, 0f, ridge);
                var d = P(-l, 0f, ridge);
                var normal = Vector3.Cross(b - a, d - a).normalized;
                var facing = normal.y < 0f ? -normal : normal;
                builder.AddQuad(a, b, c, d, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, facing, roofColor);
                // underside of the eave, so it isn't see-through from below
                builder.AddQuad(a, b, c, d, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, -facing, roofColor);
            }

            for (var end = -1; end <= 1; end += 2)
            {
                var outward3 = new Vector3(axisLong.x, 0f, axisLong.y) * end;
                builder.AddTriangle(P(end * halfLong, -halfShort, height), P(end * halfLong, halfShort, height),
                    P(end * halfLong, 0f, ridge), Vector2.zero, Vector2.zero, Vector2.zero, outward3, gableColor);
            }

            return true;
        }

        /// Smallest-area bounding rectangle, testing each footprint edge direction.
        private static bool MinAreaRectangle(List<Vector2> ring, out Vector2 center, out Vector2 axisLong,
            out float halfLong, out float halfShort)
        {
            center = Vector2.zero;
            axisLong = Vector2.right;
            halfLong = halfShort = 0f;
            var best = float.MaxValue;
            for (var i = 0; i < ring.Count; i++)
            {
                var edge = ring[(i + 1) % ring.Count] - ring[i];
                if (edge.sqrMagnitude < 1e-4f)
                {
                    continue;
                }

                var u = edge.normalized;
                var v = new Vector2(-u.y, u.x);
                float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
                foreach (var p in ring)
                {
                    var pu = Vector2.Dot(p, u);
                    var pv = Vector2.Dot(p, v);
                    minU = Mathf.Min(minU, pu);
                    maxU = Mathf.Max(maxU, pu);
                    minV = Mathf.Min(minV, pv);
                    maxV = Mathf.Max(maxV, pv);
                }

                var rectArea = (maxU - minU) * (maxV - minV);
                if (rectArea >= best)
                {
                    continue;
                }

                best = rectArea;
                center = u * ((minU + maxU) * 0.5f) + v * ((minV + maxV) * 0.5f);
                var lu = (maxU - minU) * 0.5f;
                var lv = (maxV - minV) * 0.5f;
                axisLong = lu >= lv ? u : v;
                halfLong = Mathf.Max(lu, lv);
                halfShort = Mathf.Min(lu, lv);
            }

            return best < float.MaxValue && halfShort > 0.5f;
        }

        private static bool NearMainRoad(List<Vector2> ring, List<Segment> roads)
        {
            for (var i = 0; i < ring.Count; i++)
            {
                var mid = (ring[i] + ring[(i + 1) % ring.Count]) * 0.5f;
                foreach (var road in roads)
                {
                    if (DistanceToSegment(mid, road.A, road.B) < road.HalfWidth + ShopRoadDistanceM)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<Segment> MainRoadSegments(MapRoad[] roads)
        {
            var segments = new List<Segment>();
            foreach (var road in roads)
            {
                if (road.points == null || !MainRoads.Contains(road.highway))
                {
                    continue;
                }

                for (var i = 0; i < road.points.Length - 1; i++)
                {
                    segments.Add(new Segment
                    {
                        A = new Vector2(road.points[i].x, road.points[i].z),
                        B = new Vector2(road.points[i + 1].x, road.points[i + 1].z),
                        HalfWidth = road.widthM * 0.5f,
                    });
                }
            }

            return segments;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private static Color32 WallColor(int tint, int style, bool windows) =>
            new((byte)Mathf.RoundToInt(tint / 7f * 255f), (byte)Mathf.RoundToInt(style / 3f * 255f), 0, windows ? (byte)255 : (byte)0);

        private static Color32 RoofColor(int index) =>
            new((byte)Mathf.RoundToInt(index / 7f * 255f), 0, 255, 255);

        private static Vector3 At(Vector2 p, float height) => new(p.x, height, p.y);
    }
}
