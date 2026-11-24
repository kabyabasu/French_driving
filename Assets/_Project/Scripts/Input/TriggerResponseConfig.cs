using UnityEngine;

namespace PermisVR.Input
{
    [CreateAssetMenu(fileName = "TriggerResponseConfig", menuName = "PermisVR/Input/Trigger Response Config")]
    public sealed class TriggerResponseConfig : ScriptableObject
    {
        [Range(0f, 0.5f)] public float deadZone = 0.05f;
        public AnimationCurve responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public float Apply(float raw)
        {
            if (raw <= deadZone)
            {
                return 0f;
            }

            var normalized = (raw - deadZone) / (1f - deadZone);
            return responseCurve.Evaluate(normalized);
        }
    }
}
