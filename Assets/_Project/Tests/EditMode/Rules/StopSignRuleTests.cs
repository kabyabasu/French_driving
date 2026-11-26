using System.Collections.Generic;
using NUnit.Framework;
using PermisVR.Exam;
using PermisVR.Exam.Rules;

namespace PermisVR.Exam.Tests.Rules
{
    public class StopSignRuleTests
    {
        private static VehicleSnapshot Snapshot(float timeS, float speedKmh, float routePositionM = 0f)
        {
            return new VehicleSnapshot(timeS, speedKmh, routePositionM, headingDeg: 0f);
        }

        private static StopSignRule NewRule(float thresholdKmh = 0.5f)
        {
            return new StopSignRule(new StopSignRuleConfig { SpeedThresholdKmh = thresholdKmh });
        }

        [Test]
        public void NoFault_WhenVehicleStopsBeforeLine()
        {
            var rule = NewRule();
            var faults = new List<Fault>();
            rule.FaultRaised += faults.Add;

            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneEnter, Snapshot(0f, 20f), "stop_1"));
            rule.HandleEvent(new ExamEvent(ExamEventType.VehicleTick, Snapshot(1f, 0.2f), "stop_1"));
            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneExit, Snapshot(2f, 10f), "stop_1"));

            Assert.IsEmpty(faults);
        }

        [Test]
        public void EliminatoryFault_WhenSpeedNeverDropsBelowThreshold()
        {
            var rule = NewRule();
            var faults = new List<Fault>();
            rule.FaultRaised += faults.Add;

            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneEnter, Snapshot(0f, 20f), "stop_1"));
            rule.HandleEvent(new ExamEvent(ExamEventType.VehicleTick, Snapshot(1f, 15f), "stop_1"));
            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneExit, Snapshot(2f, 15f), "stop_1"));

            Assert.That(faults, Has.Count.EqualTo(1));
            Assert.AreEqual("stop_sign", faults[0].RuleId);
            Assert.AreEqual(FaultSeverity.Eliminatory, faults[0].Severity);
        }

        [Test]
        public void Zones_AreTrackedIndependently()
        {
            // There is only one vehicle, so zones are tested back to back (never overlapping),
            // the way a car actually drives through two separate STOP signs on a route.
            var rule = NewRule();
            var faults = new List<Fault>();
            rule.FaultRaised += faults.Add;

            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneEnter, Snapshot(0f, 20f), "stop_a"));
            rule.HandleEvent(new ExamEvent(ExamEventType.VehicleTick, Snapshot(1f, 0.1f), "stop_a"));
            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneExit, Snapshot(2f, 10f), "stop_a"));

            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneEnter, Snapshot(3f, 20f), "stop_b"));
            rule.HandleEvent(new ExamEvent(ExamEventType.VehicleTick, Snapshot(4f, 20f), "stop_b"));
            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneExit, Snapshot(5f, 20f), "stop_b"));

            Assert.That(faults, Has.Count.EqualTo(1));
            Assert.AreEqual("stop_sign", faults[0].RuleId);
        }

        [Test]
        public void Fault_IsScoredUnderApplyRegulations_AndRecordsWhere()
        {
            var rule = NewRule();
            var faults = new List<Fault>();
            rule.FaultRaised += faults.Add;

            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneEnter, Snapshot(0f, 20f), "stop_1"));
            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneExit,
                new VehicleSnapshot(3f, 18f, 0f, 0f, positionX: 120f, positionZ: -40f), "stop_1"));

            Assert.AreEqual(ExamCriteria.ApplyRegulations, faults[0].Criterion);
            Assert.AreEqual(3f, faults[0].TimeS);
            Assert.AreEqual(120f, faults[0].PositionX);
            Assert.AreEqual(-40f, faults[0].PositionZ);
        }

        [Test]
        public void ReEnteringZone_RequiresANewStop()
        {
            var rule = NewRule();
            var faults = new List<Fault>();
            rule.FaultRaised += faults.Add;

            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneEnter, Snapshot(0f, 20f), "stop_1"));
            rule.HandleEvent(new ExamEvent(ExamEventType.VehicleTick, Snapshot(1f, 0f), "stop_1"));
            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneEnter, Snapshot(2f, 10f), "stop_1"));
            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneExit, Snapshot(3f, 10f), "stop_1"));

            Assert.That(faults, Has.Count.EqualTo(1));
        }

        [Test]
        public void NoFault_WhenExitingAZoneNeverEntered()
        {
            var rule = NewRule();
            var faults = new List<Fault>();
            rule.FaultRaised += faults.Add;

            rule.HandleEvent(new ExamEvent(ExamEventType.StopZoneExit, Snapshot(1f, 30f), "stop_1"));

            Assert.IsEmpty(faults);
        }
    }
}
