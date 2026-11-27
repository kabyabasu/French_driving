using System.Collections.Generic;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// Procedural texture for French road signs (no downloaded artwork): 4 x 4 cells of 256 px.
    /// Each sign's mesh maps its shape onto its cell, so the painted borders line up with the
    /// geometry and no alpha cut-out is needed. Text is drawn with a small stroke font.
    internal static class SignAtlas
    {
        public const int Stop = 0, Yield = 1, Zone30 = 10, Back = 11, Post = 12, Housing = 13, White = 14;
        private const int Columns = 4;
        private const int CellPx = 256;
        private const int SizePx = CellPx * Columns;

        // Speed limits with their own B14 cell.
        public static readonly int[] SpeedKmh = { 20, 30, 50, 70, 80, 90, 110, 130 };

        public static int SpeedCell(int kmh)
        {
            var index = System.Array.IndexOf(SpeedKmh, kmh);
            return index < 0 ? -1 : 2 + index;
        }

        public static Texture2D Texture() => MapTextureLibrary.GetOrCreate("SignAtlas", 4, Paint, SizePx, SizePx);

        /// Atlas UV of a point (u, v in 0..1) inside a cell.
        public static Vector2 Uv(int cell, float u, float v)
        {
            var column = cell % Columns;
            var row = cell / Columns;
            var inset = 2f / CellPx; // keep away from the neighbouring cells when mipmapped
            u = Mathf.Lerp(inset, 1f - inset, u);
            v = Mathf.Lerp(inset, 1f - inset, v);
            return new Vector2((column + u) / Columns, 1f - (row + 1 - v) / Columns);
        }

        // ---------------------------------------------------------------- painting

        private static readonly Color Red = new(0.78f, 0.06f, 0.08f);
        private static readonly Color SignWhite = new(0.96f, 0.96f, 0.95f);
        private static readonly Color Black = new(0.06f, 0.06f, 0.06f);

        private static Color Paint(int x, int y)
        {
            var column = x / CellPx;
            var row = Columns - 1 - y / CellPx; // row 0 at the top of the image
            var cell = row * Columns + column;
            var p = new Vector2((x % CellPx + 0.5f) / CellPx, (y % CellPx + 0.5f) / CellPx);
            switch (cell)
            {
                case Stop: return PaintStop(p);
                case Yield: return PaintYield(p);
                case Zone30: return PaintZone30(p);
                case Back: return Gray(0.55f + 0.03f * Mathf.PerlinNoise(p.x * 9f, p.y * 9f));
                case Post: return Gray(0.68f);
                case Housing: return Gray(0.08f);
                case White: return SignWhite;
                default:
                    var index = cell - 2;
                    return index >= 0 && index < SpeedKmh.Length ? PaintSpeed(p, SpeedKmh[index]) : Gray(0.5f);
            }
        }

        /// Octagon with flat top: vertex radius 0.5. Red, thin white rim, white "STOP".
        public static float StopApothem => 0.5f * Mathf.Cos(Mathf.PI / 8f);

        private static Color PaintStop(Vector2 p)
        {
            var q = p - new Vector2(0.5f, 0.5f);
            var d = float.MinValue;
            for (var k = 0; k < 8; k++)
            {
                var angle = k * Mathf.PI / 4f;
                d = Mathf.Max(d, q.x * Mathf.Cos(angle) + q.y * Mathf.Sin(angle));
            }

            d -= StopApothem;
            var color = d > -0.028f ? SignWhite : Red;
            return Text(color, "STOP", p, new Vector2(0.5f, 0.5f), 0.25f, SignWhite);
        }

        /// Downward triangle (vertices YieldCorners): red border, white middle (French AB3a has no text).
        public static readonly Vector2[] YieldCorners = { new(0.02f, 0.93f), new(0.98f, 0.93f), new(0.5f, 0.0986f) };

        private static Color PaintYield(Vector2 p)
        {
            var inside = float.MaxValue; // distance to the nearest edge, positive inside
            for (var k = 0; k < 3; k++)
            {
                var a = YieldCorners[k];
                var b = YieldCorners[(k + 1) % 3];
                var edge = (b - a).normalized;
                var normal = new Vector2(edge.y, -edge.x); // corners are clockwise in UV: this points inward
                inside = Mathf.Min(inside, Vector2.Dot(p - a, normal));
            }

            if (inside < 0.012f) return SignWhite;
            return inside < 0.12f ? Red : SignWhite;
        }

        /// B14: white disc, red ring, black number.
        private static Color PaintSpeed(Vector2 p, int kmh)
        {
            var r = Vector2.Distance(p, new Vector2(0.5f, 0.5f));
            var color = r > 0.49f ? SignWhite : r > 0.39f ? Red : SignWhite;
            var text = kmh.ToString();
            return Text(color, text, p, new Vector2(0.5f, 0.5f), text.Length > 2 ? 0.27f : 0.34f, Black);
        }

        /// B30 zone 30 entry: white panel (2:3, mapped to u 1/6..5/6), black edge, a 30 disc and "ZONE".
        private static Color PaintZone30(Vector2 p)
        {
            if (p.x < 1f / 6f + 0.02f || p.x > 5f / 6f - 0.02f || p.y < 0.02f || p.y > 0.98f)
            {
                return Black;
            }

            var center = new Vector2(0.5f, 0.6f);
            var r = Vector2.Distance(p, center);
            var color = r > 0.24f ? SignWhite : r > 0.19f ? Red : SignWhite;
            color = Text(color, "30", p, center, 0.16f, Black);
            return Text(color, "ZONE", p, new Vector2(0.5f, 0.2f), 0.11f, Black);
        }

        private static Color Gray(float v) => new(v, v, v);

        // ---------------------------------------------------------------- stroke font

        private const float GlyphWidth = 0.6f;     // in glyph heights
        private const float GlyphSpacing = 0.16f;
        private const float StrokeHalfWidth = 0.075f;

        private static Dictionary<char, List<Vector2[]>> _glyphs;

        /// Draws `text` centred at `center` (cell UV), `height` tall, over `under`.
        private static Color Text(Color under, string text, Vector2 p, Vector2 center, float height, Color ink)
        {
            var width = height * (text.Length * GlyphWidth + (text.Length - 1) * GlyphSpacing);
            var left = center.x - width * 0.5f;
            var bottom = center.y - height * 0.5f;
            if (p.x < left - height * 0.1f || p.x > left + width + height * 0.1f || p.y < bottom - height * 0.1f
                || p.y > bottom + height * 1.1f)
            {
                return under;
            }

            var glyphs = Glyphs();
            var pixel = 1f / CellPx / height; // one pixel in glyph units, for antialiasing
            var coverage = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                if (!glyphs.TryGetValue(text[i], out var strokes))
                {
                    continue;
                }

                var origin = new Vector2(left + i * height * (GlyphWidth + GlyphSpacing), bottom);
                var local = (p - origin) / height;
                foreach (var stroke in strokes)
                {
                    for (var k = 0; k < stroke.Length - 1; k++)
                    {
                        var d = DistanceToSegment(local, stroke[k], stroke[k + 1]);
                        coverage = Mathf.Max(coverage, Mathf.Clamp01((StrokeHalfWidth + pixel * 0.5f - d) / pixel));
                    }
                }
            }

            return Color.Lerp(under, ink, coverage);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private static Vector2[] Line(params float[] xy)
        {
            var points = new Vector2[xy.Length / 2];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            }

            return points;
        }

        /// Elliptical arc from one angle to another (degrees, either direction).
        private static Vector2[] Arc(float cx, float cy, float rx, float ry, float fromDeg, float toDeg)
        {
            const int steps = 24;
            var points = new Vector2[steps + 1];
            for (var i = 0; i <= steps; i++)
            {
                var a = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, i / (float)steps);
                points[i] = new Vector2(cx + rx * Mathf.Cos(a), cy + ry * Mathf.Sin(a));
            }

            return points;
        }

        // Glyph box: x 0..0.6, y 0..1; strokes are centred lines, so keep them StrokeHalfWidth inside.
        private static Dictionary<char, List<Vector2[]>> Glyphs()
        {
            if (_glyphs != null)
            {
                return _glyphs;
            }

            const float w = StrokeHalfWidth;
            _glyphs = new Dictionary<char, List<Vector2[]>>
            {
                ['S'] = new() { Arc(0.3f, 0.74f, 0.22f, 0.26f - w * 0.5f, 25f, 270f), Arc(0.3f, 0.26f + w * 0.5f, 0.22f, 0.26f - w * 0.5f, 90f, -155f) },
                ['T'] = new() { Line(w, 1f - w, 0.6f - w, 1f - w), Line(0.3f, 1f - w, 0.3f, w) },
                ['O'] = new() { Arc(0.3f, 0.5f, 0.3f - w, 0.5f - w, 0f, 360f) },
                ['P'] = new() { Line(w, w, w, 1f - w, 0.32f, 1f - w), Arc(0.32f, 0.74f, 0.26f - w, 0.26f - w, 90f, -90f), Line(0.32f, 0.48f + w, w, 0.48f + w) },
                ['Z'] = new() { Line(w, 1f - w, 0.6f - w, 1f - w, w, w, 0.6f - w, w) },
                ['N'] = new() { Line(w, w, w, 1f - w, 0.6f - w, w, 0.6f - w, 1f - w) },
                ['E'] = new() { Line(0.6f - w, 1f - w, w, 1f - w, w, w, 0.6f - w, w), Line(w, 0.5f, 0.5f, 0.5f) },
                ['0'] = new() { Arc(0.3f, 0.5f, 0.3f - w, 0.5f - w, 0f, 360f) },
                ['1'] = new() { Line(0.12f, 0.78f, 0.38f, 1f - w, 0.38f, w) },
                ['2'] = new() { Arc(0.3f, 0.72f, 0.3f - w, 0.28f - w * 0.5f, 165f, -35f), Line(0.47f, 0.57f, w, w, 0.6f - w, w) },
                ['3'] = new() { Arc(0.3f, 0.75f, 0.27f - w, 0.25f - w * 0.5f, 155f, -90f), Arc(0.3f, 0.27f, 0.3f - w, 0.27f - w * 0.5f, 90f, -155f) },
                ['5'] = new() { Line(0.6f - w, 1f - w, 0.12f, 1f - w, 0.09f, 0.56f), Arc(0.3f, 0.32f, 0.3f - w, 0.32f - w, 135f, -150f) },
                ['7'] = new() { Line(w, 1f - w, 0.6f - w, 1f - w, 0.2f, w) },
                ['8'] = new() { Arc(0.3f, 0.75f, 0.25f - w * 0.5f, 0.25f - w * 0.5f, 0f, 360f), Arc(0.3f, 0.27f, 0.3f - w, 0.27f - w * 0.5f, 0f, 360f) },
                ['9'] = new() { Arc(0.3f, 0.68f, 0.3f - w, 0.32f - w, 0f, 360f), Line(0.6f - w, 0.68f, 0.6f - w, 0.32f), Arc(0.3f, 0.32f, 0.3f - w, 0.3f - w, 0f, -145f) },
            };
            return _glyphs;
        }
    }
}
