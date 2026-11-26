using System;
using System.Collections.Generic;
using NUnit.Framework;
using PermisVR.Exam.Rules;

namespace PermisVR.Exam.Tests.Rules
{
    public class MirrorBeforeTurnRuleTests
    {
        private const float Dt = 0.02f;

        private static (MirrorBeforeTurnRule rule, List<Fault> faults) NewRule()
        {
            var rule = new MirrorBeforeTurnRule(new MirrorRulesConfig());
            var faults = new List<Fault>();
            rule.FaultRaised += faults.Add;
            return (rule, faults);
        }

        private static void Check(IExamRule rule, string mirror, float timeS) =>
            rule.HandleEvent(new ExamEvent(ExamEventType.MirrorChecked, new VehicleSnapshot(timeS, 0f, 0f, 0f), mirror));

        private static void Junction(IExamRule rule, ExamEventType type, float timeS) =>
            rule.HandleEvent(new ExamEvent(type, new VehicleSnapshot(timeS, 20f, 0f, 0f), "junction_1"));

        private static void Drive(IExamRule rule, float fromS, float toS, Func<float, float> headingAt, float speedKmh = 20f)
        {
            for (var t = fromS; t < toS; t += Dt)
            {
                rule.HandleEvent(new ExamEvent(ExamEventType.VehicleTick, new VehicleSnapshot(t, speedKmh, 0f, headingAt(t))));
            }
        }

        // Straight until 2 s, turn 90° over 3 s (10° reached at ~2.33 s), then straight.
        private static float RightTurn(float t) => t < 2f ? 0f : t < 5f ? 90f * (t - 2f) / 3f : 90f;
        private static float LeftTurn(float t) => -RightTurn(t);

        [Test]
        public void NoFault_WhenInteriorAndTurnSideMirrorCheckedBeforeTurn()
        {
            var (rule, faults) = NewRule();
            Junction(rule, ExamEventType.IntersectionEnter, 0f);
            Check(rule, MirrorIds.Interior, 0.5f);
            Check(rule, MirrorIds.Right, 1f);

            Drive(rule, 0f, 9f, RightTurn);

            Assert.IsEmpty(faults);
        }

        [Test]
        public void MinorFault_WhenNoMirrorChecked()
        {
            var (rule, faults) = NewRule();
            Junction(rule, ExamEventType.IntersectionEnter, 0f);

            Drive(rule, 0f, 9f, RightTurn);

            Assert.That(faults, Has.Count.EqualTo(1));
            Assert.AreEqual(FaultSeverity.Minor, faults[0].Severity);
            Assert.AreEqual(ExamCriteria.TakeInformation, faults[0].Criterion);
            StringAssert.Contains("droite", faults[0].ExplanationFr);
        }

        [Test]
        public void Fault_NamesTheMissingMirror()
        {
            var (rule, faults) = NewRule();
            Junction(rule, ExamEventType.IntersectionEnter, 0f);
            Check(rule, MirrorIds.Interior, 1f);

            Drive(rule, 0f, 9f, RightTurn);

            Assert.That(faults, Has.Count.EqualTo(1));
            StringAssert.Contains("manque : droit", faults[0].ExplanationFr);
        }

        [Test]
        public void Fault_WhenChecksAreTooLongBeforeTheTurn()
        {
            var (rule, faults) = NewRule();
            Junction(rule, ExamEventType.IntersectionEnter, 0f);
            Check(rule, MirrorIds.Interior, -6f);
            Check(rule, MirrorIds.Right, -6f);

            Drive(rule, 0f, 9f, RightTurn);

            Assert.That(faults, Has.Count.EqualTo(1));
        }

        [Test]
        public void LeftTurn_NeedsTheLeftMirror_NotTheRight()
        {
            var (rule, faults) = NewRule();
            Junction(rule, ExamEventType.IntersectionEnter, 0f);
            Check(rule, MirrorIds.Interior, 0.5f);
            Check(rule, MirrorIds.Right, 1f);

            Drive(rule, 0f, 9f, LeftTurn);

            Assert.That(faults, Has.Count.EqualTo(1));
            StringAssert.Contains("gauche", faults[0].ExplanationFr);
        }

        [Test]
        public void NoFault_WhenTurningOutsideAJunction()
        {
            var (rule, faults) = NewRule();

            Drive(rule, 0f, 9f, RightTurn);

            Assert.IsEmpty(faults);
        }

        [Test]
        public void NoFault_ForAGentleCurveOfTheRoad()
        {
            var (rule, faults) = NewRule();
            Junction(rule, ExamEventType.IntersectionEnter, 0f);

            Drive(rule, 0f, 22f, t => 90f * Math.Min(t, 20f) / 20f, speedKmh: 40f);

            Assert.IsEmpty(faults);
        }

        [Test]
        public void OnlyOneFaultPerTurn()
        {
            var (rule, faults) = NewRule();
            Junction(rule, ExamEventType.IntersectionEnter, 0f);

            Drive(rule, 0f, 20f, RightTurn);

            Assert.That(faults, Has.Count.EqualTo(1));
        }

        [Test]
        public void TurnAfterLeavingJunction_IsNotJudged()
        {
            var (rule, faults) = NewRule();
            Junction(rule, ExamEventType.IntersectionEnter, 0f);
            Junction(rule, ExamEventType.IntersectionExit, 1f);

            Drive(rule, 0f, 9f, RightTurn);

            Assert.IsEmpty(faults);
        }
    }
}
