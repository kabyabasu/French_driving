using System.Collections.Generic;
using NUnit.Framework;

namespace PermisVR.Exam.Tests.Grading
{
    public class CepcGraderTests
    {
        // Small test grid: 3 + 2 scored, 1 bonus => max 6.
        private static CepcGrid Grid(int passThreshold = 4, int penalty = 1)
        {
            return new CepcGrid(new List<CriterionDefinition>
            {
                new("rules", "Appliquer la réglementation", "Appréhender la route", 3, isBonus: false, isEvaluated: true),
                new("speed", "Adapter son allure", "Appréhender la route", 2, isBonus: false, isEvaluated: true),
                new("courtesy", "Courtoisie", "Conduite autonome", 1, isBonus: true, isEvaluated: false),
            }, passThreshold, penalty);
        }

        private static Fault Minor(string criterion) =>
            new("rule_x", criterion, FaultSeverity.Minor, 10f, 0f, "Faute mineure");

        private static Fault Eliminatory(string criterion) =>
            new("rule_y", criterion, FaultSeverity.Eliminatory, 20f, 0f, "Faute éliminatoire");

        private static CriterionScore ScoreOf(ExamResult result, string id)
        {
            foreach (var score in result.Scores)
            {
                if (score.Definition.Id == id)
                {
                    return score;
                }
            }

            Assert.Fail($"No score for {id}");
            return default;
        }

        [Test]
        public void NoFaults_ScoredLinesAtMax_BonusAtZero_Passes()
        {
            var result = CepcGrader.Grade(Grid(), new List<Fault>());

            Assert.AreEqual(3, ScoreOf(result, "rules").Points);
            Assert.AreEqual(2, ScoreOf(result, "speed").Points);
            Assert.AreEqual(0, ScoreOf(result, "courtesy").Points);
            Assert.AreEqual(5, result.TotalPoints);
            Assert.AreEqual(6, result.MaxTotalPoints);
            Assert.IsTrue(result.Passed);
        }

        [Test]
        public void MinorFault_DeductsFromItsOwnLineOnly()
        {
            var result = CepcGrader.Grade(Grid(), new List<Fault> { Minor("speed") });

            Assert.AreEqual(3, ScoreOf(result, "rules").Points);
            Assert.AreEqual(1, ScoreOf(result, "speed").Points);
            Assert.AreEqual(1, ScoreOf(result, "speed").MinorFaultCount);
            Assert.AreEqual(4, result.TotalPoints);
        }

        [Test]
        public void MinorFaults_NeverTakeALineBelowZero()
        {
            var faults = new List<Fault> { Minor("speed"), Minor("speed"), Minor("speed"), Minor("speed") };

            var result = CepcGrader.Grade(Grid(), faults);

            Assert.AreEqual(0, ScoreOf(result, "speed").Points);
            Assert.AreEqual(3, result.TotalPoints);
        }

        [Test]
        public void PenaltyPerFault_ComesFromTheGrid()
        {
            var result = CepcGrader.Grade(Grid(penalty: 2), new List<Fault> { Minor("rules") });

            Assert.AreEqual(1, ScoreOf(result, "rules").Points);
        }

        [Test]
        public void EliminatoryFault_MarksLineE_AndFailsDespiteEnoughPoints()
        {
            var result = CepcGrader.Grade(Grid(passThreshold: 1), new List<Fault> { Eliminatory("rules") });

            var rules = ScoreOf(result, "rules");
            Assert.IsTrue(rules.IsEliminated);
            Assert.AreEqual(0, rules.Points);
            Assert.IsTrue(result.HasEliminatoryFault);
            Assert.GreaterOrEqual(result.TotalPoints, result.PassThresholdPoints);
            Assert.IsFalse(result.Passed);
        }

        [Test]
        public void ExactlyAtPassMark_Passes_OneBelow_Fails()
        {
            Assert.IsTrue(CepcGrader.Grade(Grid(passThreshold: 5), new List<Fault>()).Passed);
            Assert.IsFalse(CepcGrader.Grade(Grid(passThreshold: 5), new List<Fault> { Minor("rules") }).Passed);
        }

        [Test]
        public void UnknownCriterion_MinorChangesNothing_EliminatoryStillFails()
        {
            Assert.AreEqual(5, CepcGrader.Grade(Grid(), new List<Fault> { Minor("not_on_grid") }).TotalPoints);
            Assert.IsFalse(CepcGrader.Grade(Grid(), new List<Fault> { Eliminatory("not_on_grid") }).Passed);
        }

        [Test]
        public void Result_KeepsEveryFault_ForExplanation()
        {
            var faults = new List<Fault> { Minor("speed"), Eliminatory("rules") };

            var result = CepcGrader.Grade(Grid(), faults);
            faults.Clear();

            Assert.AreEqual(2, result.Faults.Count);
        }
    }
}
