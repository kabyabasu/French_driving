using System.Collections.Generic;
using PermisVR.Exam;
using UnityEngine;

namespace PermisVR.ExamUnity.Config
{
    /// The CEPC grading grid as data. Values must be verified against the official Sécurité
    /// routière "Guide B" before release.
    [CreateAssetMenu(fileName = "CepcGrid", menuName = "PermisVR/Exam/CEPC Grid")]
    public sealed class CepcGridAsset : ScriptableObject
    {
        public List<CepcCriterionEntry> criteria = new();
        [Tooltip("Pass = at least this many points AND no eliminatory fault.")]
        public int passThresholdPoints = 20;
        [Tooltip("Points a scored line loses per minor fault (floor 0).")]
        public int minorFaultPenaltyPoints = 1;

        public CepcGrid ToGrid()
        {
            var definitions = new List<CriterionDefinition>(criteria.Count);
            foreach (var entry in criteria)
            {
                definitions.Add(new CriterionDefinition(entry.id, entry.labelFr, entry.familyFr, entry.maxPoints,
                    entry.isBonus, entry.isEvaluated));
            }

            return new CepcGrid(definitions, passThresholdPoints, minorFaultPenaltyPoints);
        }
    }
}
