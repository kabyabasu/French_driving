using System.Collections.Generic;
using PermisVR.Editor.Map;
using UnityEngine;

namespace PermisVR.Editor.Car
{
    /// Geometry for a generic small hatchback (proportions and cabin layout like a Yaris, no
    /// branding), in car-local metres: +Z forward, +X right, ground at about y = -0.8, driver's
    /// eye at (-0.45, 0.6, 0.3). Each part goes into the builder of its material.
    internal static class HatchbackBuilder
    {
        public const string Paint = "Paint";
        public const string TrimBlack = "TrimBlack";
        public const string Charcoal = "Charcoal";
        public const string DarkGrey = "DarkGrey";
        public const string MidGrey = "MidGrey";
        public const string Headliner = "Headliner";
        public const string Fabric = "Fabric";
        public const string Silver = "Silver";
        public const string ScreenOff = "ScreenOff";
        public const string ScreenOn = "ScreenOn";
        public const string ScreenUi = "ScreenUi";
        public const string Carpet = "Carpet";
        public const string Leather = "Leather";

        private static readonly Vector3 CenterScreen = new(0f, 0.365f, 0.87f);
        private static readonly Quaternion CenterScreenYaw = Quaternion.Euler(0f, 12f, 0f); // turned towards the driver

        public static void BuildBody(Dictionary<string, MeshBuilder> parts)
        {
            var paint = Get(parts, Paint);
            var trim = Get(parts, TrimBlack);

            // Hood (what the driver sees over the dashboard) and the rear block.
            CarMeshKit.Hexa(paint, new[]
            {
                V(-0.86f, -0.45f, 1.02f), V(0.86f, -0.45f, 1.02f), V(0.86f, -0.45f, 1.95f), V(-0.86f, -0.45f, 1.95f),
                V(-0.84f, 0.2f, 1.02f), V(0.84f, 0.2f, 1.02f), V(0.8f, 0.06f, 1.95f), V(-0.8f, 0.06f, 1.95f),
            });
            CarMeshKit.Hexa(paint, new[]
            {
                V(-0.86f, -0.45f, -1.95f), V(0.86f, -0.45f, -1.95f), V(0.86f, -0.45f, -1.48f), V(-0.86f, -0.45f, -1.48f),
                V(-0.8f, 0.2f, -1.95f), V(0.8f, 0.2f, -1.95f), V(0.84f, 0.24f, -1.48f), V(-0.84f, 0.24f, -1.48f),
            });

            // Door skins, roof, headers.
            Mirrored(paint, V(-0.89f, -0.45f, -1.48f), V(-0.86f, 0.22f, 1.02f));
            CarMeshKit.Box(paint, V(-0.74f, 1f, -1.45f), V(0.74f, 1.03f, 0.62f));
            CarMeshKit.Box(paint, V(-0.74f, 0.96f, 0.58f), V(0.74f, 1f, 0.66f));
            CarMeshKit.Box(paint, V(-0.74f, 0.94f, -1.47f), V(0.74f, 1f, -1.4f));

            // Pillars: painted outer half, trim inner half (seen from inside).
            Pillar(parts, -0.86f, -0.82f, -0.79f, 0.2f, 0.96f, 1.02f, 0.11f, 0.58f - 0.96f);       // A: leans back to the roof
            Pillar(parts, -0.86f, -0.83f, -0.8f, 0.22f, -0.5f, -0.4f, 0.1f, 0f);                 // B
            Pillar(parts, -0.86f, -0.83f, -0.8f, 0.24f, -1.48f, -1.36f, 0.1f, 0.03f);            // C

            // Black cowl under the windscreen, bumpers, sills; lamps.
            CarMeshKit.Box(trim, V(-0.84f, 0.18f, 0.98f), V(0.84f, 0.24f, 1.06f));
            CarMeshKit.Box(trim, V(-0.88f, -0.5f, 1.93f), V(0.88f, -0.2f, 2.02f));
            CarMeshKit.Box(trim, V(-0.88f, -0.5f, -2.02f), V(0.88f, -0.2f, -1.93f));
            Mirrored(trim, V(-0.9f, -0.5f, -1.5f), V(-0.86f, -0.4f, 1f));
            Mirrored(Get(parts, Silver), V(-0.78f, 0f, 1.92f), V(-0.5f, 0.08f, 1.97f));
        }

        public static void BuildInterior(Dictionary<string, MeshBuilder> parts)
        {
            var charcoal = Get(parts, Charcoal);
            var darkGrey = Get(parts, DarkGrey);
            var trim = Get(parts, TrimBlack);
            var silver = Get(parts, Silver);
            var fabric = Get(parts, Fabric);
            var carpet = Get(parts, Carpet);

            // Headliner and sun visors.
            CarMeshKit.Box(Get(parts, Headliner), V(-0.72f, 0.95f, -1.4f), V(0.72f, 0.98f, 0.6f));
            Mirrored(Get(parts, Headliner), V(-0.68f, 0.9f, 0.44f), V(-0.24f, 0.925f, 0.6f));

            // Dashboard: main body sloping towards the driver, soft top pad, lower knee panel.
            CarMeshKit.Hexa(charcoal, new[]
            {
                V(-0.8f, -0.12f, 0.64f), V(0.8f, -0.12f, 0.64f), V(0.8f, -0.12f, 1.06f), V(-0.8f, -0.12f, 1.06f),
                V(-0.8f, 0.25f, 0.72f), V(0.8f, 0.25f, 0.72f), V(0.8f, 0.25f, 1.06f), V(-0.8f, 0.25f, 1.06f),
            });
            CarMeshKit.Box(darkGrey, V(-0.8f, 0.25f, 0.7f), V(0.8f, 0.27f, 1.04f));
            CarMeshKit.Hexa(charcoal, new[]
            {
                V(-0.8f, -0.38f, 0.8f), V(0.8f, -0.38f, 0.8f), V(0.8f, -0.38f, 1.06f), V(-0.8f, -0.38f, 1.06f),
                V(-0.8f, -0.12f, 0.64f), V(0.8f, -0.12f, 0.64f), V(0.8f, -0.12f, 1.06f), V(-0.8f, -0.12f, 1.06f),
            });
            CarMeshKit.Box(Get(parts, MidGrey), V(0.15f, 0.02f, 0.655f), V(0.78f, 0.035f, 0.69f)); // passenger trim strip

            // Instrument binnacle over the driver's display (kept low so the hood stays visible).
            CarMeshKit.Box(charcoal, V(-0.65f, 0.4f, 0.8f), V(-0.25f, 0.42f, 1f));
            CarMeshKit.Box(charcoal, V(-0.65f, 0.25f, 0.8f), V(-0.63f, 0.4f, 1f));
            CarMeshKit.Box(charcoal, V(-0.27f, 0.25f, 0.8f), V(-0.25f, 0.4f, 1f));
            CarMeshKit.Box(charcoal, V(-0.65f, 0.25f, 0.96f), V(-0.25f, 0.4f, 1f));
            CarMeshKit.Box(Get(parts, ScreenOff), V(-0.6f, 0.3f, 0.935f), V(-0.3f, 0.4f, 0.945f));
            CarMeshKit.Box(Get(parts, ScreenUi), V(-0.58f, 0.302f, 0.93f), V(-0.32f, 0.307f, 0.935f));

            // Floating centre touchscreen, angled towards the driver, with a simple map-like UI.
            CarMeshKit.OrientedBox(trim, CenterScreen, CenterScreenYaw, new Vector3(0.24f, 0.14f, 0.02f));
            var screenFace = CenterScreen + CenterScreenYaw * new Vector3(0f, 0f, -0.012f);
            CarMeshKit.OrientedBox(Get(parts, ScreenOn), screenFace, CenterScreenYaw, new Vector3(0.22f, 0.12f, 0.004f));
            var ui = Get(parts, ScreenUi);
            CarMeshKit.OrientedBox(ui, screenFace + CenterScreenYaw * new Vector3(0f, 0.05f, -0.003f), CenterScreenYaw, new Vector3(0.22f, 0.016f, 0.002f));
            CarMeshKit.OrientedBox(ui, screenFace + CenterScreenYaw * new Vector3(-0.02f, -0.01f, -0.003f),
                CenterScreenYaw * Quaternion.Euler(0f, 0f, 30f), new Vector3(0.012f, 0.1f, 0.002f));
            CarMeshKit.OrientedBox(ui, screenFace + CenterScreenYaw * new Vector3(0.04f, -0.02f, -0.003f),
                CenterScreenYaw * Quaternion.Euler(0f, 0f, -70f), new Vector3(0.008f, 0.09f, 0.002f));

            // Vents: rectangular pair under the screen, round ones at the dash ends; climate buttons.
            Mirrored(trim, V(-0.1f, 0.13f, 0.665f), V(-0.02f, 0.2f, 0.69f));
            foreach (var x in new[] { -0.72f, 0.72f })
            {
                CarMeshKit.Prism(trim, V(x, 0.17f, 0.71f), Vector3.back, 0.045f, 0.02f, 12);
                CarMeshKit.Torus(silver, V(x, 0.17f, 0.69f), Vector3.forward, 0.045f, 0.006f, 16, 4);
            }

            for (var i = 0; i < 4; i++)
            {
                var x = -0.09f + i * 0.047f;
                CarMeshKit.Box(darkGrey, V(x, 0.05f, 0.645f), V(x + 0.036f, 0.08f, 0.665f));
            }

            // Centre console with gear selector, cup holders and armrest.
            CarMeshKit.Hexa(charcoal, new[]
            {
                V(-0.13f, -0.45f, -0.4f), V(0.13f, -0.45f, -0.4f), V(0.13f, -0.45f, 0.66f), V(-0.13f, -0.45f, 0.66f),
                V(-0.13f, -0.17f, -0.4f), V(0.13f, -0.17f, -0.4f), V(0.13f, -0.14f, 0.66f), V(-0.13f, -0.14f, 0.66f),
            });
            CarMeshKit.Box(silver, V(-0.05f, -0.152f, 0.18f), V(0.05f, -0.142f, 0.32f));
            CarMeshKit.Prism(charcoal, V(0f, -0.142f, 0.25f), Vector3.up, 0.02f, 0.07f, 8);
            CarMeshKit.Prism(silver, V(0f, -0.072f, 0.25f), Vector3.up, 0.021f, 0.008f, 8);
            CarMeshKit.Prism(trim, V(-0.05f, -0.16f, 0.02f), Vector3.up, 0.035f, 0.004f, 10);
            CarMeshKit.Prism(trim, V(0.05f, -0.16f, 0.02f), Vector3.up, 0.035f, 0.004f, 10);
            CarMeshKit.Box(darkGrey, V(-0.13f, -0.17f, -0.4f), V(0.13f, -0.08f, -0.12f));

            // Floor, sloping footwell, pedals.
            CarMeshKit.Box(carpet, V(-0.82f, -0.48f, -1.45f), V(0.82f, -0.45f, 0.85f));
            CarMeshKit.OrientedBox(carpet, V(0f, -0.285f, 0.955f), Quaternion.Euler(-57.5f, 0f, 0f), new Vector3(1.6f, 0.02f, 0.39f));
            CarMeshKit.OrientedBox(trim, V(-0.36f, -0.33f, 0.86f), Quaternion.Euler(-60f, 0f, 0f), new Vector3(0.05f, 0.01f, 0.13f));
            CarMeshKit.OrientedBox(trim, V(-0.52f, -0.3f, 0.84f), Quaternion.Euler(-60f, 0f, 0f), new Vector3(0.09f, 0.012f, 0.07f));

            // Front seats (driver and passenger) and rear bench.
            foreach (var x in new[] { -0.45f, 0.45f })
            {
                CarMeshKit.Box(fabric, V(x - 0.24f, -0.3f, 0f), V(x + 0.24f, -0.15f, 0.5f));
                CarMeshKit.Box(darkGrey, V(x - 0.15f, -0.15f, 0.06f), V(x + 0.15f, -0.145f, 0.44f));
                CarMeshKit.OrientedBox(fabric, V(x, 0.185f, -0.08f), Quaternion.Euler(-13.4f, 0f, 0f), new Vector3(0.48f, 0.69f, 0.11f));
                CarMeshKit.Box(fabric, V(x - 0.12f, 0.55f, -0.21f), V(x + 0.12f, 0.71f, -0.13f));
            }

            CarMeshKit.Box(fabric, V(-0.7f, -0.3f, -1.25f), V(0.7f, -0.15f, -0.8f));
            CarMeshKit.OrientedBox(fabric, V(0f, 0.17f, -1.3f), Quaternion.Euler(-15f, 0f, 0f), new Vector3(1.4f, 0.66f, 0.12f));
            Mirrored(fabric, V(-0.57f, 0.58f, -1.42f), V(-0.33f, 0.72f, -1.34f));

            // Door panels: front and rear, both sides.
            foreach (var (from, to) in new[] { (-0.45f, 1f), (-1.45f, -0.5f) })
            {
                Mirrored(charcoal, V(-0.86f, -0.45f, from), V(-0.8f, 0.2f, to));
                Mirrored(darkGrey, V(-0.8f, 0f, from + 0.05f), V(-0.79f, 0.13f, to - 0.05f));
                Mirrored(trim, V(-0.87f, 0.2f, from), V(-0.79f, 0.225f, to));
                Mirrored(darkGrey, V(-0.8f, -0.04f, from + 0.45f), V(-0.72f, 0f, from + 0.9f));
                Mirrored(silver, V(-0.8f, 0.06f, to - 0.45f), V(-0.785f, 0.08f, to - 0.34f));
                foreach (var side in new[] { -1f, 1f })
                {
                    CarMeshKit.Prism(trim, V(side * 0.795f, -0.3f, to - 0.4f), Vector3.right * side, 0.07f, 0.01f, 12);
                }
            }
        }

        /// Rotating part of the three-spoke wheel, in SteeringWheelCenter-local space (driver at -Z).
        public static void BuildSteeringWheel(Dictionary<string, MeshBuilder> parts, float rimRadiusM)
        {
            var leather = Get(parts, Leather);
            var charcoal = Get(parts, Charcoal);
            var silver = Get(parts, Silver);
            var darkGrey = Get(parts, DarkGrey);

            CarMeshKit.Torus(leather, Vector3.zero, Vector3.forward, rimRadiusM, 0.017f, 28, 8);
            CarMeshKit.Prism(charcoal, V(0f, 0f, 0.02f), Vector3.back, 0.065f, 0.05f, 10);
            Mirrored(charcoal, V(-rimRadiusM + 0.01f, -0.035f, -0.012f), V(-0.06f, 0f, 0.012f));
            CarMeshKit.Box(charcoal, V(-0.025f, -rimRadiusM + 0.01f, -0.012f), V(0.025f, -0.06f, 0.012f));
            Mirrored(silver, V(-0.15f, -0.012f, -0.016f), V(-0.08f, -0.004f, -0.012f));
            CarMeshKit.Box(silver, V(-0.012f, -0.15f, -0.016f), V(0.012f, -0.08f, -0.012f));
            Mirrored(darkGrey, V(-0.14f, -0.032f, -0.016f), V(-0.12f, -0.02f, -0.012f));
            Mirrored(darkGrey, V(-0.11f, -0.032f, -0.016f), V(-0.09f, -0.02f, -0.012f));
        }

        /// Fixed steering column and shroud behind the wheel, in SteeringWheelCenter-local space.
        public static void BuildSteeringColumn(Dictionary<string, MeshBuilder> parts)
        {
            var charcoal = Get(parts, Charcoal);
            CarMeshKit.Prism(charcoal, V(0f, 0f, 0.03f), Vector3.forward, 0.035f, 0.3f, 10);
            CarMeshKit.OrientedBox(charcoal, V(0f, -0.02f, 0.2f), Quaternion.identity, new Vector3(0.1f, 0.09f, 0.2f));
        }

        /// A left-side pillar from its base (x outer..inner at height baseY, z from..to) up to the roof
        /// at y = 1, its top moved inwards by topInwardX and along by topShiftZ; outer half painted,
        /// inner half trim. Each half is also mirrored to the right side.
        private static void Pillar(Dictionary<string, MeshBuilder> parts, float outerX, float middleX, float innerX,
            float baseY, float fromZ, float toZ, float topInwardX, float topShiftZ)
        {
            Half(Get(parts, Paint), outerX, middleX, baseY, fromZ, toZ, topInwardX, topShiftZ);
            Half(Get(parts, MidGrey), middleX, innerX, baseY, fromZ, toZ, topInwardX, topShiftZ);
        }

        private static void Half(MeshBuilder builder, float x0, float x1, float baseY, float fromZ, float toZ,
            float shiftX, float shiftZ)
        {
            var corners = new[]
            {
                V(x0, baseY, fromZ), V(x1, baseY, fromZ), V(x1, baseY, toZ), V(x0, baseY, toZ),
                V(x0 + shiftX, 1f, fromZ + shiftZ), V(x1 + shiftX, 1f, fromZ + shiftZ),
                V(x1 + shiftX, 1f, toZ + shiftZ), V(x0 + shiftX, 1f, toZ + shiftZ),
            };
            CarMeshKit.Hexa(builder, corners);
            for (var i = 0; i < corners.Length; i++)
            {
                corners[i] = CarMeshKit.MirrorX(corners[i]);
            }

            CarMeshKit.Hexa(builder, corners);
        }

        /// Axis-aligned box on the left side plus its mirror on the right.
        private static void Mirrored(MeshBuilder builder, Vector3 min, Vector3 max)
        {
            CarMeshKit.Box(builder, min, max);
            CarMeshKit.Box(builder, new Vector3(-max.x, min.y, min.z), new Vector3(-min.x, max.y, max.z));
        }

        private static MeshBuilder Get(Dictionary<string, MeshBuilder> parts, string material)
        {
            if (!parts.TryGetValue(material, out var builder))
            {
                builder = new MeshBuilder();
                parts[material] = builder;
            }

            return builder;
        }

        private static Vector3 V(float x, float y, float z) => new(x, y, z);
    }
}
