using PermisVR.Editor.Map;
using UnityEngine;

namespace PermisVR.Editor.Car
{
    /// Builds simple low-poly hand meshes (palm, three-segment fingers, two-segment thumb, cuff).
    /// Hand-local frame (right hand): +Z along the fingers, +Y back of the hand, thumb on -X.
    /// The left hand is the mirror image. The grip pose wraps the fingers around a rim tube
    /// centred at (0, -0.03, 0.02), which SteeringHandVisual relies on.
    internal static class HandMeshBuilder
    {
        private const float FingerThicknessM = 0.016f;

        // index, middle, ring, pinky (thumb side first)
        private static readonly float[] FingerX = { -0.027f, -0.009f, 0.009f, 0.026f };
        private static readonly float[] FingerWidth = { 0.017f, 0.018f, 0.017f, 0.015f };
        private static readonly float[][] SegmentLength =
        {
            new[] { 0.042f, 0.025f, 0.020f },
            new[] { 0.047f, 0.028f, 0.021f },
            new[] { 0.044f, 0.026f, 0.020f },
            new[] { 0.034f, 0.020f, 0.018f },
        };

        private static readonly float[] GripCurlDeg = { 55f, 75f, 60f };
        private static readonly float[] OpenCurlDeg = { 10f, 15f, 10f };

        public static Mesh Build(string name, bool left, bool grip)
        {
            var builder = new MeshBuilder();

            // Palm and cuff.
            CarMeshKit.OrientedBox(builder, Vector3.zero, Quaternion.identity, new Vector3(0.08f, 0.026f, 0.09f), left);
            CarMeshKit.OrientedBox(builder, new Vector3(0f, 0.002f, -0.075f), Quaternion.identity, new Vector3(0.062f, 0.042f, 0.06f), left);

            var curl = grip ? GripCurlDeg : OpenCurlDeg;
            for (var f = 0; f < FingerX.Length; f++)
            {
                AddFinger(builder, new Vector3(FingerX[f], -0.004f, 0.045f), FingerWidth[f], SegmentLength[f], curl, left);
            }

            AddThumb(builder, grip, left);
            return builder.Build(name);
        }

        /// Segments laid end to end, each bending further towards the palm (-Y) around the X axis.
        private static void AddFinger(MeshBuilder builder, Vector3 basePoint, float width, float[] lengths, float[] curlDeg, bool left)
        {
            var point = basePoint;
            var bend = 0f;
            for (var s = 0; s < lengths.Length; s++)
            {
                bend += curlDeg[s];
                var rotation = Quaternion.Euler(bend, 0f, 0f); // +X rotation pitches the finger down
                var direction = rotation * Vector3.forward;
                var center = point + direction * (lengths[s] * 0.5f);
                CarMeshKit.OrientedBox(builder, center, rotation, new Vector3(width, FingerThicknessM, lengths[s]), left);
                point += direction * lengths[s];
            }
        }

        private static void AddThumb(MeshBuilder builder, bool grip, bool left)
        {
            var basePoint = new Vector3(-0.038f, -0.008f, -0.005f);
            var first = grip ? new Vector3(-0.25f, -0.55f, 0.8f) : new Vector3(-0.6f, -0.2f, 0.75f);
            var second = grip ? new Vector3(0.35f, -0.6f, 0.7f) : new Vector3(-0.35f, -0.15f, 0.9f);
            var lengths = new[] { 0.036f, 0.03f };
            var directions = new[] { first.normalized, second.normalized };
            var point = basePoint;
            for (var s = 0; s < 2; s++)
            {
                var rotation = Quaternion.LookRotation(directions[s], Vector3.up);
                CarMeshKit.OrientedBox(builder, point + directions[s] * (lengths[s] * 0.5f), rotation,
                    new Vector3(0.019f, 0.018f, lengths[s]), left);
                point += directions[s] * lengths[s];
            }
        }
    }
}
