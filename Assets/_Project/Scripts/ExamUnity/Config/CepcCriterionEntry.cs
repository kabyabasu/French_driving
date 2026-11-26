using System;
using UnityEngine;

namespace PermisVR.ExamUnity.Config
{
    /// Inspector-editable row of the CEPC grid; id must match an ExamCriteria constant.
    [Serializable]
    public sealed class CepcCriterionEntry
    {
        public string id;
        public string labelFr;
        public string familyFr;
        [Range(0, 5)] public int maxPoints = 3;
        public bool isBonus;
        [Tooltip("False while the game has no detector for this line; it then counts at its maximum.")]
        public bool isEvaluated;
    }
}
