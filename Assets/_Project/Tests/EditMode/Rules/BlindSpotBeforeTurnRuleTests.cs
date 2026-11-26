using System;
using System.Collections.Generic;
using NUnit.Framework;
using PermisVR.Exam.Rules;

namespace PermisVR.Exam.Tests.Rules
{
    public class BlindSpotBeforeTurnRuleTests
    {
        private const float Dt = 0.02f;

        private static (BlindSpotBeforeTurnRule rule, List<Fault> faults) NewRule(MirrorRulesConfig config = null)
        {
            var rule = new BlindSpotBeforeTurnRule(config ?? new MirrorRulesConfig());
            var faults = new List<Fault>();
            rule.FaultRaised += faults.Add;
            return (rule, faults);
        }

        private static void Shoulder(IExamRule rule, string side, float timeS) =>
            rule.HandleEvent(new ExamEvent(ExamEventType.BlindSpotChecked, new VehicleSnapshot(timeS, 20f, 0f, 0f), side));

        private static void EnterJunction(IExamRule rule, float timeS) =>
            rule.HandleEvent(new ExamEvent(ExamEventType.IntersectionEnter, new VehicleSnapshot(timeS, 20f, 0f, 0f), "junction_1"));

        private static void Drive(IExamRule rule, float fromS, float toS, Func<float, float> headingAt)
        {
            for (var t = fromS; t < toS; t += Dt)
            {
                rule.HandleEvent(new ExamEvent(ExamEventType.VehicleTick, new VehicleSnapshot(t, 20f, 0f, headingAt(t))));
            }
        }

        // Straight until 2 s, turn 90° over 3 s (10° reached at ~2.33 s), then straight.
        private static float RightTurn(float t) => t < 2f ? 0f : t < 5f ? 90f * (t - 2f) / 3f : 90f;
        private static float LeftTurn(float t) => -RightTurn(t);

        [Test]
        public void MinorFault_WhenTurningRightWithoutRightShoulderCheck()
        {
            var (rule, faults) = NewRule();
            EnterJunction(rule, 0f);

            Drive(rule, 0f, 9f, RightTurn);

            Assert.That(faults, Has.Count.EqualTo(1));
            Assert.AreEqual(FaultSeverity.Minor, faults[0].Severity);
            Assert.AreEqual(ExamCriteria.TakeInformation, faults[0].Criterion);
            StringAssert.Contains("Angle mort droit", faults[0].ExplanationFr);
        }

        [Test]
        public void NoFault_WhenTurnSideShoulderChecked()
        {
            var (rule, faults) = NewRule();
            EnterJunction(rule, 0f);
            Shoulder(rule, BlindSpotIds.Right, 1f);

            Drive(rule, 0f, 9f, RightTurn);

            Assert.IsEmpty(faults);
        }

        [Test]
        public void LeftTurn_NeedsTheLeftShoulder_NotTheRight()
        {
            var (rule, faults) = NewRule();
            EnterJunction(rule, 0f);
            Shoulder(rule, BlindSpotIds.Right, 1f);

            Drive(rule, 0f, 9f, LeftTurn);

            Assert.That(faults, Has.Count.EqualTo(1));
            StringAssert.Contains("Angle mort gauche", faults[0].ExplanationFr);
        }

        [Test]
        public void NoFault_WhenTurningOutsideAJunction()
        {
            var (rule, faults) = NewRule();

            Drive(rule, 0f, 9f, RightTurn);

            Assert.IsEmpty(faults);
        }

        [Test]
        public void OnlyOneFaultPerTurn()
        {
            var (rule, faults) = NewRule();
            EnterJunction(rule, 0f);

            Drive(rule, 0f, 15f, RightTurn);

            Assert.That(faults, Has.Count.EqualTo(1));
        }

        [Test]
        public void NotJudged_WhenTurnChecksAreSwitchedOff()
        {
            var (rule, faults) = NewRule(new MirrorRulesConfig { BlindSpotOnTurns = false });
            EnterJunction(rule, 0f);

            Drive(rule, 0f, 9f, RightTurn);

            Assert.IsEmpty(faults);
        }
    }
}
