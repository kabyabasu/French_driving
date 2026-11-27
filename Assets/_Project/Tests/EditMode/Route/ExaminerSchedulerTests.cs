using System.Collections.Generic;
using NUnit.Framework;
using PermisVR.Route;

namespace PermisVR.Exam.Tests.Route
{
    public class ExaminerSchedulerTests
    {
        private const float Dt = 0.02f;

        private sealed class Run
        {
            public readonly ExaminerScheduler Scheduler;
            public readonly List<(ExaminerPhrase phrase, float s, float t)> Said = new();
            public float T;
            public float S;

            public Run(RouteDefinition route, ExaminerConfig config = null)
            {
                Scheduler = new ExaminerScheduler(route, config ?? new ExaminerConfig());
            }

            /// Stand still for a while (the opening instruction comes after DepartDelayS).
            public Run Wait(float durationS, float lateralM = 0f)
            {
                for (var end = T + durationS; T < end; T += Dt)
                {
                    Tick(0f, lateralM);
                }

                return this;
            }

            public Run Drive(float toS, float speedKmh, float lateralM = 0f)
            {
                var step = speedKmh / 3.6f * Dt;
                while (S < toS)
                {
                    S += step;
                    T += Dt;
                    Tick(speedKmh, lateralM);
                }

                return this;
            }

            private void Tick(float speedKmh, float lateralM)
            {
                if (Scheduler.Tick(T, S, lateralM, speedKmh, out var phrase))
                {
                    Said.Add((phrase, S, T));
                }
            }

            public List<ExaminerPhrase> Phrases()
            {
                var list = new List<ExaminerPhrase>();
                foreach (var entry in Said)
                {
                    list.Add(entry.phrase);
                }

                return list;
            }
        }

        private static RouteDefinition Route(RouteJunction[] junctions, params RouteManeuver[] maneuvers) =>
            new("test", new[] { 0f, 0f }, new[] { 0f, 1000f }, junctions, maneuvers);

        private static RouteJunction Right(float s) => new(s, false, true);
        private static RouteJunction Left(float s) => new(s, true, false);

        [Test]
        public void Depart_IsSaidAfterTheDelay()
        {
            var run = new Run(Route(null)).Wait(2.9f);
            Assert.That(run.Said, Is.Empty);
            run.Wait(0.2f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart }));
        }

        [Test]
        public void RightTurn_AnnouncedOnce_AtSpeedDependentDistance()
        {
            var run = new Run(Route(new[] { Right(500f) }, new RouteManeuver(500f, ManeuverType.Right)))
                .Wait(4f).Drive(600f, 50f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart, ExaminerPhrase.NextRight }));
            // 50 km/h x 7 s = 97 m before the junction.
            Assert.That(run.Said[1].s, Is.EqualTo(500f - 50f / 3.6f * 7f).Within(1f));
        }

        [Test]
        public void SlowCar_StillGetsTheMinimumDistance()
        {
            var run = new Run(Route(new[] { Left(300f) }, new RouteManeuver(300f, ManeuverType.Left)))
                .Wait(4f).Drive(320f, 10f);
            Assert.That(run.Said[1].phrase, Is.EqualTo(ExaminerPhrase.NextLeft));
            Assert.That(run.Said[1].s, Is.EqualTo(260f).Within(0.5f));
        }

        [Test]
        public void SecondRight_WhenAnotherRightStreetComesFirst()
        {
            var run = new Run(Route(new[] { Right(450f), Left(470f), Right(500f) }, new RouteManeuver(500f, ManeuverType.Right)))
                .Wait(4f).Drive(600f, 50f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart, ExaminerPhrase.SecondRight }));
        }

        [Test]
        public void WaitsUntilTheTurnIsAtMostTheSecondStreet()
        {
            var run = new Run(Route(new[] { Right(420f), Right(450f), Right(500f) }, new RouteManeuver(500f, ManeuverType.Right)))
                .Wait(4f).Drive(600f, 50f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart, ExaminerPhrase.SecondRight }));
            // Once the first street is within JunctionBehindM (5 m) it is being crossed and no longer counts.
            Assert.That(run.Said[1].s, Is.GreaterThan(415f).And.LessThan(417f));
        }

        [Test]
        public void StraightOn_AndRoundaboutExit()
        {
            var run = new Run(Route(new[] { Right(300f), Left(600f) },
                    new RouteManeuver(300f, ManeuverType.Straight),
                    new RouteManeuver(600f, ManeuverType.Roundabout, 3)))
                .Wait(4f).Drive(700f, 50f);
            Assert.That(run.Phrases(), Is.EqualTo(new[]
            {
                ExaminerPhrase.Depart, ExaminerPhrase.StraightOn, ExaminerPhrase.RoundaboutExit3,
            }));
        }

        [Test]
        public void StraightOn_WaitsUntilItIsTheNextJunction()
        {
            var run = new Run(Route(new[] { Left(560f), Right(600f), new RouteJunction(640f, true, true) },
                    new RouteManeuver(640f, ManeuverType.Straight)))
                .Wait(4f).Drive(700f, 50f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart, ExaminerPhrase.StraightOn }));
            Assert.That(run.Said[1].s, Is.GreaterThan(595f).And.LessThan(597f));
        }

        [Test]
        public void CloseManeuvers_KeepTheMinimumGap()
        {
            var run = new Run(Route(new[] { Right(400f), Left(420f) },
                    new RouteManeuver(400f, ManeuverType.Right),
                    new RouteManeuver(420f, ManeuverType.Left)))
                .Wait(4f).Drive(500f, 30f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart, ExaminerPhrase.NextRight, ExaminerPhrase.NextLeft }));
            Assert.That(run.Said[2].t - run.Said[1].t, Is.GreaterThanOrEqualTo(4f - 0.001f));
        }

        [Test]
        public void OffRoute_ToldOnceAfterTheDelay_AndAgainAfterRejoining()
        {
            var run = new Run(Route(null)).Wait(4f).Drive(100f, 30f);
            run.Wait(2.5f, 40f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart }));
            run.Wait(10f, 40f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart, ExaminerPhrase.OffRoute }));
            run.Drive(200f, 30f, 0f).Wait(5f, 40f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart, ExaminerPhrase.OffRoute, ExaminerPhrase.OffRoute }));
        }

        [Test]
        public void OffRoute_IsNeverAFault_ButSilencesTurnInstructions()
        {
            var run = new Run(Route(new[] { Right(300f) }, new RouteManeuver(300f, ManeuverType.Right)))
                .Wait(4f).Drive(150f, 30f);
            run.Drive(260f, 30f, 40f);
            Assert.That(run.Phrases(), Has.No.Member(ExaminerPhrase.NextRight));
        }

        [Test]
        public void PullOver_ThenExamFinishedOnceStopped()
        {
            var run = new Run(Route(null)).Wait(4f).Drive(870f, 50f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart }));
            run.Drive(900f, 50f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart, ExaminerPhrase.PullOver }));
            run.Drive(950f, 20f).Wait(1.5f);
            Assert.That(run.Scheduler.Finished, Is.False);
            run.Wait(1f);
            Assert.That(run.Phrases(), Is.EqualTo(new[] { ExaminerPhrase.Depart, ExaminerPhrase.PullOver, ExaminerPhrase.ExamFinished }));
            Assert.That(run.Scheduler.Finished, Is.True);
        }
    }
}
