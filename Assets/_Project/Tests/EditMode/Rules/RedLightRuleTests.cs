using System.Collections.Generic;
using NUnit.Framework;
using PermisVR.Exam.Rules;

namespace PermisVR.Exam.Tests.Rules
{
    public class RedLightRuleTests
    {
        private static VehicleSnapshot Snapshot(float timeS, float speedKmh, float x = 0f, float z = 0f)
        {
            return new VehicleSnapshot(timeS, speedKmh, routePositionM: 0f, headingDeg: 0f, positionX: x, positionZ: z);
        }

        private static List<Fault> Watch(IExamRule rule)
        {
            var faults = new List<Fault>();
            rule.FaultRaised += faults.Add;
            return faults;
        }

        [Test]
        public void EliminatoryFault_WhenLineCrossedOnRed()
        {
            var rule = new RedLightRule();
            var faults = Watch(rule);

            rule.HandleEvent(new ExamEvent(ExamEventType.StopLineCrossed, Snapshot(12f, 30f, 40f, -15f), "light_1", SignalState.Red));

            Assert.That(faults, Has.Count.EqualTo(1));
            Assert.AreEqual("red_light", faults[0].RuleId);
            Assert.AreEqual(FaultSeverity.Eliminatory, faults[0].Severity);
            Assert.AreEqual(ExamCriteria.ReadTheRoad, faults[0].Criterion);
        }

        [Test]
        public void Fault_RecordsWhenAndWhere()
        {
            var rule = new RedLightRule();
            var faults = Watch(rule);

            rule.HandleEvent(new ExamEvent(ExamEventType.StopLineCrossed, Snapshot(12f, 30f, 40f, -15f), "light_1", SignalState.Red));

            Assert.AreEqual(12f, faults[0].TimeS);
            Assert.AreEqual(40f, faults[0].PositionX);
            Assert.AreEqual(-15f, faults[0].PositionZ);
            Assert.IsNotEmpty(faults[0].ExplanationFr);
        }

        [TestCase(SignalState.Green)]
        [TestCase(SignalState.Amber)]
        [TestCase(SignalState.None)]
        public void NoFault_WhenLineCrossedOnAnythingButRed(SignalState signal)
        {
            var rule = new RedLightRule();
            var faults = Watch(rule);

            rule.HandleEvent(new ExamEvent(ExamEventType.StopLineCrossed, Snapshot(5f, 30f), "light_1", signal));

            Assert.IsEmpty(faults);
        }

        [Test]
        public void NoFault_ForOtherEventsWhileRed()
        {
            var rule = new RedLightRule();
            var faults = Watch(rule);

            rule.HandleEvent(new ExamEvent(ExamEventType.VehicleTick, Snapshot(1f, 30f), null, SignalState.Red));
            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneExit, Snapshot(2f, 30f), "light_1", SignalState.Red));

            Assert.IsEmpty(faults);
        }
    }
}
