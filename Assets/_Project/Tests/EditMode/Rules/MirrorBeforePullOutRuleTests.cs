using System;
using System.Collections.Generic;
using NUnit.Framework;
using PermisVR.Exam.Rules;

namespace PermisVR.Exam.Tests.Rules
{
    public class MirrorBeforePullOutRuleTests
    {
        private const float Dt = 0.02f;

        private static (MirrorBeforePullOutRule rule, List<Fault> faults) NewRule()
        {
            var rule = new MirrorBeforePullOutRule(new MirrorRulesConfig());
            var faults = new List<Fault>();
            rule.FaultRaised += faults.Add;
            return (rule, faults);
        }

        private static void Check(IExamRule rule, string mirror, float timeS) =>
            rule.HandleEvent(new ExamEvent(ExamEventType.MirrorChecked, new VehicleSnapshot(timeS, 0f, 0f, 0f), mirror));

        private static void Drive(IExamRule rule, float fromS, float toS, Func<float, float> speedAt)
        {
            for (var t = fromS; t < toS; t += Dt)
            {
                rule.HandleEvent(new ExamEvent(ExamEventType.VehicleTick, new VehicleSnapshot(t, speedAt(t), 0f, 0f)));
            }
        }

        // Stationary until 3 s, then accelerating to 20 km/h over 2 s (passes 1 km/h at ~3.1 s).
        private static float MoveOffAt3(float t) => t < 3f ? 0f : Math.Min(20f, 10f * (t - 3f));

        [Test]
        public void MinorFault_WhenMovingOffAtExamStartWithoutChecks()
        {
            var (rule, faults) = NewRule();

            Drive(rule, 0f, 8f, MoveOffAt3);

            Assert.That(faults, Has.Count.EqualTo(1));
            Assert.AreEqual(ExamCriteria.TakeInformation, faults[0].Criterion);
            StringAssert.Contains("démarrer", faults[0].ExplanationFr);
        }

        [Test]
        public void NoFault_WhenInteriorAndLeftCheckedBeforeMovingOff()
        {
            var (rule, faults) = NewRule();
            Check(rule, MirrorIds.Interior, 1.5f);
            Check(rule, MirrorIds.Left, 2f);

            Drive(rule, 0f, 8f, MoveOffAt3);

            Assert.IsEmpty(faults);
        }

        [Test]
        public void Fault_NamesTheMissingMirror()
        {
            var (rule, faults) = NewRule();
            Check(rule, MirrorIds.Interior, 2f);

            Drive(rule, 0f, 8f, MoveOffAt3);

            StringAssert.Contains("manque : gauche", faults[0].ExplanationFr);
        }

        [Test]
        public void CheckJustAfterMovingOff_CountsWithinGrace()
        {
            var (rule, faults) = NewRule();
            Check(rule, MirrorIds.Interior, 2f);
            Check(rule, MirrorIds.Left, 3.3f);

            Drive(rule, 0f, 8f, MoveOffAt3);

            Assert.IsEmpty(faults);
        }

        [Test]
        public void ShortStop_DoesNotNeedAnotherCheck()
        {
            var (rule, faults) = NewRule();
            Check(rule, MirrorIds.Interior, 1.5f);
            Check(rule, MirrorIds.Left, 2f);

            // Moves off, stops for 3 s at 10 s, moves again.
            Drive(rule, 0f, 20f, t => t < 10f ? MoveOffAt3(t) : t < 13f ? 0f : 20f);

            Assert.IsEmpty(faults);
        }

        [Test]
        public void LongStopAwayFromAJunction_NeedsAnotherCheck()
        {
            var (rule, faults) = NewRule();
            Check(rule, MirrorIds.Interior, 1.5f);
            Check(rule, MirrorIds.Left, 2f);

            Drive(rule, 0f, 30f, t => t < 10f ? MoveOffAt3(t) : t < 25f ? 0f : 20f);

            Assert.That(faults, Has.Count.EqualTo(1));
        }

        [Test]
        public void LongStopInsideAJunction_IsNotJudged()
        {
            var (rule, faults) = NewRule();
            Check(rule, MirrorIds.Interior, 1.5f);
            Check(rule, MirrorIds.Left, 2f);
            Drive(rule, 0f, 9f, MoveOffAt3);
            rule.HandleEvent(new ExamEvent(ExamEventType.IntersectionEnter, new VehicleSnapshot(9f, 20f, 0f, 0f), "junction_1"));

            Drive(rule, 9f, 30f, t => t < 10f ? 20f : t < 25f ? 0f : 20f);

            Assert.IsEmpty(faults);
        }

        [Test]
        public void SettlingAtSpawn_IsNotMovingOff()
        {
            var (rule, faults) = NewRule();

            // Suspension bounce in the first second (peaks at 2 km/h), then parked.
            Drive(rule, 0f, 8f, t => t < 1f ? 2f * (float)Math.Abs(Math.Sin(t * 10f)) : 0f);

            Assert.IsEmpty(faults);
        }

        [Test]
        public void CreepingBelowMoveOffSpeed_IsNotJudgedUntilReallyMoving()
        {
            var (rule, faults) = NewRule();

            // Creeps at 2 km/h from 1 s, then really moves off at 10 s: judged at 10 s, not 1 s.
            Drive(rule, 0f, 10f, t => t < 1f ? 0f : 2f);
            Assert.IsEmpty(faults);

            Drive(rule, 10f, 14f, _ => 20f);
            Assert.That(faults, Has.Count.EqualTo(1));
            Assert.That(faults[0].TimeS, Is.GreaterThanOrEqualTo(10f));
        }
    }
}
