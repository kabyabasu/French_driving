using PermisVR.Exam.Rules;
using UnityEngine;

namespace PermisVR.ExamUnity.Config
{
    [CreateAssetMenu(fileName = "StopSignRuleConfig", menuName = "PermisVR/Exam/Stop Sign Rule Config")]
    public sealed class StopSignRuleConfigAsset : ScriptableObject
    {
        [SerializeField] private float _speedThresholdKmh = 0.5f;

        public StopSignRuleConfig ToConfig()
        {
            return new StopSignRuleConfig { SpeedThresholdKmh = _speedThresholdKmh };
        }
    }
}
