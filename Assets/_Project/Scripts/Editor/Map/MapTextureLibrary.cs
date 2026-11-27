using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// Procedurally generated, seamlessly tiling map textures (no downloaded assets): asphalt,
    /// concrete paving slabs and rendered (crépi) garden walls. Generated once as PNGs, then reused;
    /// delete a PNG to regenerate it. Import settings target Quest: mipmaps, ASTC, 512 px max.
    internal static class MapTextureLibrary
    {
        private const string TexturesDir = "Assets/_Project/Data/Textures/Map";
        private const int Size = 512;

        // Metres covered by one repeat of each texture (the material scale is 1 / this).
        public const float AsphaltRepeatM = 4f;
        public const float PavingRepeatM = 2f;
        public const float WallRepeatM = 2f;

        private const int PavingSlabsPerRepeat = 2;  // 1 m slabs
        private const float GroutWidth = 0.006f;     // fraction of a repeat (~1 cm)

        public static Texture2D Asphalt() => GetOrCreate("Asphalt", 4, PaintAsphalt);
        public static Texture2D Paving() => GetOrCreate("Paving", 2, PaintPaving);
        public static Texture2D WallRender() => GetOrCreate("WallRender", 2, PaintWall);

        /// Façade atlas for the PermisVR/Facade shader: 4 styles x (ground floor, upper storey),
        /// one 3 m x 3 m cell each. Alpha 1 = render (tinted per building), 0 = keeps its colour.
        public static Texture2D FacadeAtlas() =>
            GetOrCreate("FacadeAtlas", 2, PaintFacade, FacadeCellPx * 4, FacadeCellPx * 2, true);

        private const int FacadeCellPx = 256;
        public const float FacadeBayM = 3f;

        private static Texture2D GetOrCreate(string name, int anisoLevel, Func<int, int, Color> paint,
            int width = Size, int height = Size, bool alpha = false)
        {
            var path = $"{TexturesDir}/{name}.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(width, height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
                var pixels = new Color[width * height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        pixels[y * width + x] = paint(x, y);
                    }
                }

                texture.SetPixels(pixels);
                Directory.CreateDirectory(TexturesDir);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            Configure(path, anisoLevel, Mathf.Max(width, height), alpha);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Configure(string path, int anisoLevel, int maxSize, bool alpha)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var android = importer.GetPlatformTextureSettings("Android");
            if (importer.wrapMode == TextureWrapMode.Repeat && importer.mipmapEnabled && importer.anisoLevel == anisoLevel
                && importer.maxTextureSize == maxSize && android.overridden && android.format == TextureImporterFormat.ASTC_6x6)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true; // essential in VR: no shimmering at a distance
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = anisoLevel;
            importer.maxTextureSize = maxSize;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.npotScale = TextureImporterNPOTScale.None;
            android.overridden = true;
            android.maxTextureSize = maxSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        private static Color PaintAsphalt(int x, int y)
        {
            var u = (float)x / Size;
            var v = (float)y / Size;
            var shade = 0.23f + 0.1f * (Fbm(u, v, 16, 5, 11) - 0.5f) + 0.07f * (Fbm(u, v, 2, 3, 12) - 0.5f);

            // Aggregate: scattered light and dark stones.
            var grain = Hash(x, y, 13);
            if (grain > 0.92f)
            {
                shade += 0.16f * Hash(x, y, 14);
            }
            else if (grain < 0.05f)
            {
                shade -= 0.05f;
            }

            return new Color(shade, shade, shade * 1.03f);
        }

        private static Color PaintPaving(int x, int y)
        {
            var u = (float)x / Size;
            var v = (float)y / Size;
            var su = u * PavingSlabsPerRepeat;
            var sv = v * PavingSlabsPerRepeat;
            var fu = su - Mathf.Floor(su);
            var fv = sv - Mathf.Floor(sv);
            var grout = GroutWidth * PavingSlabsPerRepeat;
            if (fu < grout || fu > 1f - grout || fv < grout || fv > 1f - grout)
            {
                return Gray(0.42f + 0.04f * Hash(x, y, 21));
            }

            // Each slab slightly different, with fine concrete grain.
            var slabTint = 0.05f * (Hash((int)su, (int)sv, 22) - 0.5f);
            var shade = 0.62f + slabTint + 0.07f * (Fbm(u, v, 8, 4, 23) - 0.5f) + 0.03f * (Hash(x, y, 24) - 0.5f);
            return Gray(shade);
        }

        private static Color PaintWall(int x, int y)
        {
            var u = (float)x / Size;
            var v = (float)y / Size; // v = 0 at the foot of the wall (UV v is height in metres / 2)
            var shade = 0.94f + 0.12f * (Fbm(u, v, 4, 5, 31) - 0.5f) + 0.04f * (Hash(x, y, 32) - 0.5f);
            var color = new Color(0.82f, 0.78f, 0.70f) * shade;

            // Coping along the top of a 2 m wall, and splash dirt along the bottom.
            if (v > 0.955f)
            {
                return Gray(v < 0.962f ? 0.35f : 0.68f + 0.04f * (Hash(x, y, 33) - 0.5f));
            }

            if (v < 0.12f)
            {
                var dirt = (1f - v / 0.12f) * (0.6f + 0.4f * Fbm(u, v, 16, 3, 34));
                color = Color.Lerp(color, new Color(0.45f, 0.42f, 0.36f), dirt * 0.45f);
            }

            color.a = 1f;
            return color;
        }

        private static Color Gray(float shade) => new(shade, shade, shade);

        // --- Façade atlas -------------------------------------------------------------------

        private static readonly Color Glass = new(0.16f, 0.2f, 0.25f, 0f);
        private static readonly Color FrameWhite = new(0.93f, 0.93f, 0.9f, 0f);
        private static readonly Color Sill = new(0.82f, 0.8f, 0.76f, 0f);
        private static readonly Color ShutterBlue = new(0.52f, 0.6f, 0.64f, 0f);
        private static readonly Color ShutterBrown = new(0.44f, 0.31f, 0.21f, 0f);
        private static readonly Color Iron = new(0.08f, 0.08f, 0.09f, 0f);
        private static readonly Color Plinth = new(0.5f, 0.49f, 0.47f, 0f);

        private static Color PaintFacade(int x, int y)
        {
            var column = x / FacadeCellPx;
            var upper = y >= FacadeCellPx;
            var mx = (x % FacadeCellPx + 0.5f) / FacadeCellPx * FacadeBayM; // metres across the bay
            var my = (y % FacadeCellPx + 0.5f) / FacadeCellPx * FacadeBayM; // metres above the storey floor
            var grain = 0.025f * (Hash(x, y, 41) - 0.5f);
            var wall = new Color(0.9f + grain, 0.9f + grain, 0.9f + grain, 1f);
            if (upper && my > 2.9f)
            {
                wall = new Color(0.96f, 0.96f, 0.95f, 1f); // storey band
            }

            var color = upper ? PaintUpper(column, mx, my) : PaintGround(column, mx, my);
            return color ?? wall;
        }

        private static Color? PaintUpper(int column, float mx, float my)
        {
            switch (column)
            {
                case 1: // French window with balcony railing
                    if (In(mx, my, 0.7f, 0.08f, 2.3f, 0.16f)) return Gray(0.7f).WithAlpha(0f);
                    if (In(mx, my, 0.9f, 0.16f, 2.1f, 1.1f) && (Railing(mx) || my > 1.04f)) return Iron;
                    return Window(mx, my, 0.9f, 0.16f, 2.1f, 2.45f, 0.06f);
                case 2: // flats: wide window under a roller-shutter box
                    if (In(mx, my, 0.65f, 2.2f, 2.35f, 2.42f)) return Gray(0.8f).WithAlpha(0f);
                    if (In(mx, my, 0.6f, 0.84f, 2.4f, 0.9f)) return Sill;
                    return Window(mx, my, 0.65f, 0.9f, 2.35f, 2.2f, 0.06f);
                default: // window with open shutters (blue-grey, or brown for style 3)
                    return ShutteredWindow(mx, my, 0.9f, column == 3 ? ShutterBrown : ShutterBlue);
            }
        }

        private static Color? PaintGround(int column, float mx, float my)
        {
            switch (column)
            {
                case 1: // shopfront: sign band and large glazing
                    if (In(mx, my, 0.05f, 2.35f, 2.95f, 2.8f))
                        return my < 2.4f || my > 2.75f ? Gray(0.2f).WithAlpha(0f) : new Color(0.33f, 0.1f, 0.12f, 0f);
                    if (In(mx, my, 0.12f, 0.3f, 2.88f, 2.32f))
                    {
                        if (my < 0.36f || my > 2.26f || mx < 0.18f || mx > 2.82f || Mathf.Abs(mx - 2.05f) < 0.04f)
                            return Gray(0.22f).WithAlpha(0f);
                        var warm = Mathf.Lerp(0.34f, 0.2f, my / 2.3f);
                        return new Color(warm, warm * 0.92f, warm * 0.78f, 0f);
                    }

                    return my < 0.3f ? Plinth : null;
                case 2: // entrance door with stone surround
                    if (In(mx, my, 0.8f, 0f, 2.2f, 0.12f)) return Gray(0.62f).WithAlpha(0f);
                    if (In(mx, my, 0.82f, 0.12f, 2.18f, 2.42f))
                    {
                        if (!In(mx, my, 0.92f, 0.12f, 2.08f, 2.32f)) return Sill;
                        if (my > 2.0f) return my < 2.04f ? Gray(0.25f).WithAlpha(0f) : Glass;
                        var panel = Mathf.Abs(mx - 1.5f) < 0.025f || Mathf.Abs(my - 1.1f) < 0.025f;
                        return panel ? new Color(0.24f, 0.16f, 0.1f, 0f) : new Color(0.33f, 0.22f, 0.14f, 0f);
                    }

                    return my < 0.35f ? Plinth : null;
                case 3: // garage door
                    if (In(mx, my, 0.22f, 0f, 2.78f, 2.32f))
                    {
                        if (!In(mx, my, 0.28f, 0f, 2.72f, 2.26f)) return Gray(0.3f).WithAlpha(0f);
                        return Gray(my % 0.25f < 0.025f ? 0.62f : 0.78f).WithAlpha(0f);
                    }

                    return my < 0.35f ? Plinth : null;
                default: // ground-floor window with shutters, behind a plinth
                    return my < 0.35f ? Plinth : ShutteredWindow(mx, my, 1.0f, ShutterBlue);
            }
        }

        private static Color? ShutteredWindow(float mx, float my, float sillM, Color shutter)
        {
            const float left = 0.98f, right = 2.02f, top = 2.35f;
            if (In(mx, my, left - 0.06f, sillM - 0.06f, right + 0.06f, sillM)) return Sill;
            var shutterWidth = (right - left) * 0.5f;
            if ((In(mx, my, left - shutterWidth, sillM, left, top) || In(mx, my, right, sillM, right + shutterWidth, top)))
            {
                var louvre = (my - sillM) % 0.08f < 0.012f;
                return louvre ? shutter * 0.75f : shutter;
            }

            return Window(mx, my, left, sillM, right, top, 0.05f);
        }

        /// Framed glass with a central mullion and two glazing bars; the top reflects a little sky.
        private static Color? Window(float mx, float my, float x0, float y0, float x1, float y1, float frame)
        {
            if (!In(mx, my, x0, y0, x1, y1))
            {
                return null;
            }

            var middle = (x0 + x1) * 0.5f;
            if (!In(mx, my, x0 + frame, y0 + frame, x1 - frame, y1 - frame) || Mathf.Abs(mx - middle) < frame * 0.5f)
            {
                return FrameWhite;
            }

            var paneHeight = (y1 - y0) / 3f;
            var bar = Mathf.Abs((my - y0) % paneHeight) < 0.015f;
            if (bar && my > y0 + frame * 2f && my < y1 - frame * 2f)
            {
                return FrameWhite;
            }

            var sky = Mathf.Clamp01((my - y0) / (y1 - y0));
            return Color.Lerp(Glass, new Color(0.42f, 0.5f, 0.58f, 0f), sky * sky * 0.6f);
        }

        private static bool Railing(float mx) => (mx - 0.9f) % 0.12f < 0.022f;

        private static bool In(float mx, float my, float x0, float y0, float x1, float y1) =>
            mx >= x0 && mx < x1 && my >= y0 && my < y1;

        /// Fractal value noise that tiles over [0,1): every octave's lattice repeats exactly.
        private static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            var sum = 0f;
            var amplitude = 1f;
            var total = 0f;
            var period = basePeriod;
            for (var o = 0; o < octaves; o++)
            {
                sum += amplitude * PeriodicNoise(u * period, v * period, period, seed + o * 101);
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }

            return sum / total;
        }

        private static float PeriodicNoise(float x, float y, int period, int seed)
        {
            var ix = Mathf.FloorToInt(x);
            var iy = Mathf.FloorToInt(y);
            var fx = Smooth(x - ix);
            var fy = Smooth(y - iy);
            var x0 = Mod(ix, period);
            var y0 = Mod(iy, period);
            var x1 = Mod(ix + 1, period);
            var y1 = Mod(iy + 1, period);
            var top = Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), fx);
            var bottom = Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), fx);
            return Mathf.Lerp(top, bottom, fy);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static int Mod(int a, int m) => ((a % m) + m) % m;

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = x * 374761393 + y * 668265263 + seed * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
