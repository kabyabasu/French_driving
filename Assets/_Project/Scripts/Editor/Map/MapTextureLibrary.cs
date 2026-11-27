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

        private static Texture2D GetOrCreate(string name, int anisoLevel, Func<int, int, Color> paint)
        {
            var path = $"{TexturesDir}/{name}.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                var pixels = new Color[Size * Size];
                for (var y = 0; y < Size; y++)
                {
                    for (var x = 0; x < Size; x++)
                    {
                        pixels[y * Size + x] = paint(x, y);
                    }
                }

                texture.SetPixels(pixels);
                Directory.CreateDirectory(TexturesDir);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            Configure(path, anisoLevel);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Configure(string path, int anisoLevel)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var android = importer.GetPlatformTextureSettings("Android");
            if (importer.wrapMode == TextureWrapMode.Repeat && importer.mipmapEnabled && importer.anisoLevel == anisoLevel
                && android.overridden && android.format == TextureImporterFormat.ASTC_6x6)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true; // essential in VR: no shimmering at a distance
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = anisoLevel;
            importer.maxTextureSize = Size;
            android.overridden = true;
            android.maxTextureSize = Size;
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
