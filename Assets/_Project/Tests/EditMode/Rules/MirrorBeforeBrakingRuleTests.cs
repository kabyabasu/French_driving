using System;
using System.Collections.Generic;
using NUnit.Framework;
using PermisVR.Exam.Rules;

namespace PermisVR.Exam.Tests.Rules
{
    public class MirrorBeforeBrakingRuleTests
    {
        private const float Dt = 0.02f;

        private static (MirrorBeforeBrakingRule rule, List<Fault> faults) NewRule()
        {
            var rule = new MirrorBeforeBrakingRule(new MirrorRulesConfig());
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

        /// Cruise at startKmh until 3 s, then slow at decelMs2 down to 0.
        private static Func<float, float> SlowDown(float startKmh, float decelMs2) =>
            t => t < 3f ? startKmh : Math.Max(0f, startKmh - decelMs2 * 3.6f * (t - 3f));

        [Test]
        public void MinorFault_WhenBrakingFirmlyWithoutInteriorCheck()
        {
            var (rule, faults) = NewRule();

            Drive(rule, 0f, 8f, SlowDown(50f, 5f));

            Assert.That(faults, Has.Count.EqualTo(1));
            Assert.AreEqual(ExamCriteria.TakeInformation, faults[0].Criterion);
            StringAssert.Contains("freiner", faults[0].ExplanationFr);
        }

        [Test]
        public void NoFault_WhenInteriorCheckedBeforeBraking()
        {
            var (rule, faults) = NewRule();
            Check(rule, MirrorIds.Interior, 1.5f);

            Drive(rule, 0f, 8f, SlowDown(50f, 5f));

            Assert.IsEmpty(faults);
        }

        [Test]
        public void ASideMirror_DoesNotReplaceTheInteriorMirror()
        {
            var (rule, faults) = NewRule();
            Check(rule, MirrorIds.Left, 1.5f);

            Drive(rule, 0f, 8f, SlowDown(50f, 5f));

            Assert.That(faults, Has.Count.EqualTo(1));
        }

        [Test]
        public void NoFault_WhenJustCoasting()
        {
            var (rule, faults) = NewRule();

            Drive(rule, 0f, 25f, SlowDown(50f, 0.8f));

            Assert.IsEmpty(faults);
        }

        [Test]
        public void NoFault_WhenBrakingFromLowSpeed()
        {
            var (rule, faults) = NewRule();

            Drive(rule, 0f, 8f, SlowDown(15f, 5f));

            Assert.IsEmpty(faults);
        }

        [Test]
        public void OneFaultPerBrakingEpisode()
        {
            var (rule, faults) = NewRule();
            var first = SlowDown(50f, 5f);

            // Brake to a stop, cruise again from 10 s, brake again at 13 s.
            Drive(rule, 0f, 20f, t => t < 10f ? first(t) : t < 13f ? 50f : Math.Max(0f, 50f - 18f * (t - 13f)));

            Assert.That(faults, Has.Count.EqualTo(2));
        }
    }
}
