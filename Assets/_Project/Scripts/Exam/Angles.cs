namespace PermisVR.Exam
{
    /// Heading maths without UnityEngine (this assembly has noEngineReferences).
    public static class Angles
    {
        /// Shortest signed difference from a to b in degrees, in [-180, 180]. Positive = clockwise (right).
        public static float Delta(float fromDeg, float toDeg)
        {
            var delta = (toDeg - fromDeg) % 360f;
            if (delta > 180f)
            {
                delta -= 360f;
            }
            else if (delta < -180f)
            {
                delta += 360f;
            }

            return delta;
        }
    }
}
