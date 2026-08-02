using OsuParsers.Beatmaps.Objects;
using OsuParsers.Enums.Beatmaps;
using Osussist.src.cheat.aimbot;
using System.Numerics;

namespace Osussist.Tests
{
    internal static class Program
    {
        private const float Tolerance = 0.5f;

        private static void Main()
        {
            LinearPathUsesSliderLength();
            RepeatReversesPathProgress();
            BezierPathTraversesControlCurve();
            BezierRedAnchorStartsNewSegment();
            PerfectCirclePathTraversesArc();
            CatmullPathTraversesSpline();
            VirtualCursorUsesSentPosition();
            SpinnerPathUsesPlaybackTime();
        }

        private static void LinearPathUsesSliderLength()
        {
            var path = new SliderPathEvaluator(CreateSlider(CurveType.Linear, new Vector2(50f, 0f), 1, 100d));

            AssertNear(new Vector2(0f, 0f), path.PositionAtTime(0));
            AssertNear(new Vector2(50f, 0f), path.PositionAtTime(500));
            AssertNear(new Vector2(100f, 0f), path.PositionAtTime(1000));
        }

        private static void RepeatReversesPathProgress()
        {
            var path = new SliderPathEvaluator(CreateSlider(CurveType.Linear, new Vector2(100f, 0f), 2, 100d));

            AssertNear(new Vector2(50f, 0f), path.PositionAtTime(250));
            AssertNear(new Vector2(100f, 0f), path.PositionAtTime(500));
            AssertNear(new Vector2(50f, 0f), path.PositionAtTime(750));
            AssertNear(new Vector2(0f, 0f), path.PositionAtTime(1000));
        }

        private static void BezierPathTraversesControlCurve()
        {
            var path = new SliderPathEvaluator(CreateSlider(CurveType.Bezier, new Vector2(50f, 100f), new Vector2(100f, 0f), 1, 0d));
            Vector2 midpoint = path.PositionAtTime(500);

            AssertNear(new Vector2(0f, 0f), path.PositionAtTime(0));
            AssertNear(new Vector2(100f, 0f), path.PositionAtTime(1000));
            AssertTrue(midpoint.Y > 20f, "Bezier midpoint did not leave the linear path.");
        }

        private static void PerfectCirclePathTraversesArc()
        {
            var path = new SliderPathEvaluator(CreateSlider(CurveType.PerfectCurve, new Vector2(100f, 0f), new Vector2(100f, 100f), 1, 222.15d));
            Vector2 midpoint = path.PositionAtTime(500);

            AssertNear(new Vector2(0f, 0f), path.PositionAtTime(0));
            AssertNear(new Vector2(100f, 100f), path.PositionAtTime(1000));
            AssertTrue(midpoint.X > 100f, "Perfect-circle midpoint did not follow the arc through its control point.");
        }

        private static void BezierRedAnchorStartsNewSegment()
        {
            var path = new SliderPathEvaluator(CreateSlider(
                CurveType.Bezier,
                new List<Vector2>
                {
                    new Vector2(50f, 100f),
                    new Vector2(100f, 0f),
                    new Vector2(100f, 0f),
                    new Vector2(150f, -100f),
                    new Vector2(200f, 0f)
                },
                1,
                0d
            ));

            AssertNear(new Vector2(100f, 0f), path.PositionAtTime(500));
        }

        private static void CatmullPathTraversesSpline()
        {
            var path = new SliderPathEvaluator(CreateSlider(CurveType.Catmull, new Vector2(50f, 100f), new Vector2(100f, 0f), 1, 0d));
            Vector2 midpoint = path.PositionAtTime(500);

            AssertNear(new Vector2(0f, 0f), path.PositionAtTime(0));
            AssertNear(new Vector2(100f, 0f), path.PositionAtTime(1000));
            AssertTrue(midpoint.Y > 20f, "Catmull midpoint did not leave the linear path.");
        }

        private static void VirtualCursorUsesSentPosition()
        {
            var tracker = new SliderCursorTracker();
            tracker.Reset(Vector2.Zero);

            AssertNear(Vector2.Zero, tracker.MoveTowards(new Vector2(0.75f, 0f)));
            AssertNear(new Vector2(1f, 0f), tracker.MoveTowards(new Vector2(1f, 0f)));
            AssertNear(Vector2.Zero, tracker.MoveTowards(new Vector2(1f, 0f)));
            AssertNear(new Vector2(-1f, 0f), tracker.MoveTowards(Vector2.Zero));
            AssertNear(Vector2.Zero, tracker.CurrentPosition);
        }

        private static void SpinnerPathUsesPlaybackTime()
        {
            Vector2 center = new Vector2(256f, 192f);
            const float radius = 96f;

            AssertNear(new Vector2(352f, 192f), SpinnerPath.PositionAt(center, radius, 1000, 1000));
            Vector2 quarterTurn = SpinnerPath.PositionAt(center, radius, 1000, 1031);
            AssertTrue(quarterTurn.Y > center.Y && quarterTurn.X > center.X, "Spinner path did not advance clockwise from its start point.");
            AssertNear(new Vector2(352f, 192f), SpinnerPath.PositionAt(center, radius, 1000, 1125));
        }

        private static Slider CreateSlider(CurveType curveType, Vector2 firstPoint, int repeats = 1, double pixelLength = 100d)
        {
            return CreateSlider(curveType, new List<Vector2> { firstPoint }, repeats, pixelLength);
        }

        private static Slider CreateSlider(CurveType curveType, Vector2 firstPoint, Vector2 secondPoint, int repeats = 1, double pixelLength = 100d)
        {
            return CreateSlider(curveType, new List<Vector2> { firstPoint, secondPoint }, repeats, pixelLength);
        }

        private static Slider CreateSlider(CurveType curveType, List<Vector2> points, int repeats, double pixelLength)
        {
            return new Slider(
                Vector2.Zero,
                0,
                1000,
                (HitSoundType)0,
                curveType,
                points,
                repeats,
                pixelLength,
                false,
                0
            );
        }

        private static void AssertNear(Vector2 expected, Vector2 actual)
        {
            if (Vector2.Distance(expected, actual) > Tolerance)
                throw new InvalidOperationException($"Expected {expected}, received {actual}.");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
