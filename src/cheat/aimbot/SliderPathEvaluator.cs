using OsuParsers.Beatmaps.Objects;
using OsuParsers.Enums.Beatmaps;
using System.Numerics;

namespace Osussist.src.cheat.aimbot
{
    internal sealed class SliderPathEvaluator
    {
        private const float PointEpsilon = 0.001f;
        private const float BezierTolerance = 0.25f;
        private const int MaxBezierDepth = 16;

        private readonly List<Vector2> path;
        private readonly List<float> cumulativeLengths = new List<float>();
        private readonly int startTime;
        private readonly int endTime;
        private readonly int spanCount;

        public SliderPathEvaluator(Slider slider)
        {
            startTime = slider.StartTime;
            endTime = slider.EndTime;
            spanCount = System.Math.Max(1, slider.Repeats);

            var controlPoints = new List<Vector2>(slider.SliderPoints.Count + 1)
            {
                slider.Position
            };
            controlPoints.AddRange(slider.SliderPoints);

            path = BuildPath(controlPoints, slider.CurveType, slider.PixelLength);
            BuildLengthTable();
        }

        public Vector2 PositionAtTime(int currentTime)
        {
            if (endTime <= startTime)
                return PositionAtProgress(0f);

            float overallProgress = Clamp((float)(currentTime - startTime) / (endTime - startTime), 0f, 1f);
            float spanProgress = overallProgress * spanCount;
            int spanIndex = System.Math.Min(spanCount - 1, (int)System.Math.Floor(spanProgress));
            float pathProgress = spanProgress - spanIndex;

            if ((spanIndex & 1) != 0)
                pathProgress = 1f - pathProgress;

            return PositionAtProgress(pathProgress);
        }

        private Vector2 PositionAtProgress(float progress)
        {
            if (path.Count == 0)
                return Vector2.Zero;
            if (path.Count == 1 || cumulativeLengths.Count == 0)
                return path[0];

            float distance = Clamp(progress, 0f, 1f) * cumulativeLengths[cumulativeLengths.Count - 1];
            int low = 0;
            int high = cumulativeLengths.Count - 1;

            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (cumulativeLengths[middle] < distance)
                    low = middle + 1;
                else
                    high = middle;
            }

            if (low == 0)
                return path[0];

            float previousDistance = cumulativeLengths[low - 1];
            float segmentDistance = cumulativeLengths[low] - previousDistance;
            if (segmentDistance <= PointEpsilon)
                return path[low];

            return Lerp(path[low - 1], path[low], (distance - previousDistance) / segmentDistance);
        }

        private static List<Vector2> BuildPath(List<Vector2> controlPoints, CurveType curveType, double expectedLength)
        {
            if (controlPoints.Count == 0)
                return new List<Vector2>();

            List<Vector2> calculatedPath;
            switch (curveType)
            {
                case CurveType.Linear:
                    calculatedPath = CopyPath(controlPoints);
                    break;
                case CurveType.Catmull:
                    calculatedPath = BuildCatmullPath(controlPoints);
                    break;
                case CurveType.PerfectCurve:
                    calculatedPath = BuildPerfectCirclePath(controlPoints);
                    break;
                default:
                    calculatedPath = BuildBezierPath(controlPoints);
                    break;
            }

            return MatchExpectedLength(calculatedPath, (float)expectedLength, HasDuplicateFinalControlPoint(controlPoints));
        }

        private void BuildLengthTable()
        {
            cumulativeLengths.Clear();
            if (path.Count == 0)
                return;

            cumulativeLengths.Add(0f);
            float length = 0f;
            for (int index = 1; index < path.Count; index++)
            {
                length += Vector2.Distance(path[index - 1], path[index]);
                cumulativeLengths.Add(length);
            }
        }

        private static List<Vector2> CopyPath(List<Vector2> points)
        {
            var result = new List<Vector2>(points.Count);
            foreach (Vector2 point in points)
                AddPoint(result, point);
            return result;
        }

        private static List<Vector2> BuildBezierPath(List<Vector2> controlPoints)
        {
            var result = new List<Vector2>();
            var currentSegment = new List<Vector2> { controlPoints[0] };

            for (int index = 1; index < controlPoints.Count; index++)
            {
                if (PointsEqual(controlPoints[index], controlPoints[index - 1]) && currentSegment.Count > 1)
                {
                    AddBezierSegment(currentSegment, result);
                    currentSegment.Clear();
                    currentSegment.Add(controlPoints[index]);
                }
                else
                {
                    currentSegment.Add(controlPoints[index]);
                }
            }

            AddBezierSegment(currentSegment, result);
            return result;
        }

        private static void AddBezierSegment(List<Vector2> controlPoints, List<Vector2> result)
        {
            if (controlPoints.Count == 0)
                return;
            if (controlPoints.Count == 1)
            {
                AddPoint(result, controlPoints[0]);
                return;
            }

            AddPoint(result, controlPoints[0]);
            ApproximateBezier(controlPoints, result, 0);
        }

        private static void ApproximateBezier(List<Vector2> controlPoints, List<Vector2> result, int depth)
        {
            if (depth >= MaxBezierDepth || IsBezierFlatEnough(controlPoints))
            {
                AddPoint(result, controlPoints[controlPoints.Count - 1]);
                return;
            }

            SplitBezier(controlPoints, out List<Vector2> left, out List<Vector2> right);
            ApproximateBezier(left, result, depth + 1);
            ApproximateBezier(right, result, depth + 1);
        }

        private static bool IsBezierFlatEnough(List<Vector2> controlPoints)
        {
            float controlPolygonLength = 0f;
            for (int index = 1; index < controlPoints.Count; index++)
                controlPolygonLength += Vector2.Distance(controlPoints[index - 1], controlPoints[index]);

            float chordLength = Vector2.Distance(controlPoints[0], controlPoints[controlPoints.Count - 1]);
            return controlPolygonLength - chordLength <= BezierTolerance;
        }

        private static void SplitBezier(List<Vector2> controlPoints, out List<Vector2> left, out List<Vector2> right)
        {
            int count = controlPoints.Count;
            var work = new List<Vector2>(controlPoints);
            left = new List<Vector2>(count) { work[0] };
            right = new List<Vector2>(count) { work[count - 1] };

            for (int level = 1; level < count; level++)
            {
                for (int index = 0; index < count - level; index++)
                    work[index] = Lerp(work[index], work[index + 1], 0.5f);

                left.Add(work[0]);
                right.Add(work[count - level - 1]);
            }

            right.Reverse();
        }

        private static List<Vector2> BuildCatmullPath(List<Vector2> controlPoints)
        {
            var result = new List<Vector2>();
            if (controlPoints.Count == 1)
            {
                result.Add(controlPoints[0]);
                return result;
            }

            for (int index = 0; index < controlPoints.Count - 1; index++)
            {
                Vector2 p0 = index == 0 ? controlPoints[index] : controlPoints[index - 1];
                Vector2 p1 = controlPoints[index];
                Vector2 p2 = controlPoints[index + 1];
                Vector2 p3 = index + 2 < controlPoints.Count ? controlPoints[index + 2] : p2;
                int samples = System.Math.Max(8, (int)System.Math.Ceiling(Vector2.Distance(p1, p2) / 2f));

                for (int sample = 0; sample <= samples; sample++)
                    AddPoint(result, CentripetalCatmullRom(p0, p1, p2, p3, (float)sample / samples));
            }

            return result;
        }

        private static Vector2 CentripetalCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float progress)
        {
            float t0 = 0f;
            float t1 = NextCatmullTime(t0, p0, p1);
            float t2 = NextCatmullTime(t1, p1, p2);
            float t3 = NextCatmullTime(t2, p2, p3);

            if (t2 - t1 <= PointEpsilon)
                return Lerp(p1, p2, progress);

            float time = t1 + (t2 - t1) * progress;
            Vector2 a1 = InterpolateAtTime(p0, p1, t0, t1, time);
            Vector2 a2 = InterpolateAtTime(p1, p2, t1, t2, time);
            Vector2 a3 = InterpolateAtTime(p2, p3, t2, t3, time);
            Vector2 b1 = InterpolateAtTime(a1, a2, t0, t2, time);
            Vector2 b2 = InterpolateAtTime(a2, a3, t1, t3, time);
            return InterpolateAtTime(b1, b2, t1, t2, time);
        }

        private static float NextCatmullTime(float time, Vector2 first, Vector2 second)
        {
            return time + (float)System.Math.Sqrt(Vector2.Distance(first, second));
        }

        private static Vector2 InterpolateAtTime(Vector2 first, Vector2 second, float firstTime, float secondTime, float time)
        {
            float duration = secondTime - firstTime;
            if (System.Math.Abs(duration) <= PointEpsilon)
                return second;

            return ((secondTime - time) / duration) * first + ((time - firstTime) / duration) * second;
        }

        private static List<Vector2> BuildPerfectCirclePath(List<Vector2> controlPoints)
        {
            if (controlPoints.Count != 3)
                return BuildBezierPath(controlPoints);

            Vector2 first = controlPoints[0];
            Vector2 middle = controlPoints[1];
            Vector2 last = controlPoints[2];
            float determinant = 2f * Cross(middle - first, last - first);
            if (System.Math.Abs(determinant) <= PointEpsilon)
                return BuildBezierPath(controlPoints);

            float firstSquared = first.LengthSquared();
            float middleSquared = middle.LengthSquared();
            float lastSquared = last.LengthSquared();
            var center = new Vector2(
                (firstSquared * (middle.Y - last.Y) + middleSquared * (last.Y - first.Y) + lastSquared * (first.Y - middle.Y)) / determinant,
                (firstSquared * (last.X - middle.X) + middleSquared * (first.X - last.X) + lastSquared * (middle.X - first.X)) / determinant
            );

            float radius = Vector2.Distance(first, center);
            if (radius <= PointEpsilon)
                return BuildBezierPath(controlPoints);

            float startAngle = (float)System.Math.Atan2(first.Y - center.Y, first.X - center.X);
            float middleAngle = (float)System.Math.Atan2(middle.Y - center.Y, middle.X - center.X);
            float endAngle = (float)System.Math.Atan2(last.Y - center.Y, last.X - center.X);
            float arcAngle = PositiveAngle(endAngle - startAngle);
            if (PositiveAngle(middleAngle - startAngle) > arcAngle)
                arcAngle -= (float)(2d * System.Math.PI);

            int samples = System.Math.Max(2, (int)System.Math.Ceiling(System.Math.Abs(arcAngle) * radius / 2f));
            var result = new List<Vector2>(samples + 1);
            for (int sample = 0; sample <= samples; sample++)
            {
                float angle = startAngle + arcAngle * sample / samples;
                AddPoint(result, center + new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * radius);
            }

            return result;
        }

        private static List<Vector2> MatchExpectedLength(List<Vector2> points, float expectedLength, bool preventExtension)
        {
            if (points.Count == 0 || expectedLength <= 0f)
                return points;

            float length = 0f;
            for (int index = 1; index < points.Count; index++)
                length += Vector2.Distance(points[index - 1], points[index]);

            if (length > expectedLength + PointEpsilon)
            {
                var trimmed = new List<Vector2> { points[0] };
                float traversed = 0f;
                for (int index = 1; index < points.Count; index++)
                {
                    float segmentLength = Vector2.Distance(points[index - 1], points[index]);
                    if (traversed + segmentLength >= expectedLength)
                    {
                        float progress = segmentLength <= PointEpsilon ? 0f : (expectedLength - traversed) / segmentLength;
                        AddPoint(trimmed, Lerp(points[index - 1], points[index], progress));
                        return trimmed;
                    }

                    AddPoint(trimmed, points[index]);
                    traversed += segmentLength;
                }

                return trimmed;
            }

            if (length < expectedLength - PointEpsilon && points.Count >= 2 && !preventExtension)
            {
                Vector2 direction = points[points.Count - 1] - points[points.Count - 2];
                if (direction.LengthSquared() > PointEpsilon * PointEpsilon)
                {
                    direction = Vector2.Normalize(direction);
                    AddPoint(points, points[points.Count - 1] + direction * (expectedLength - length));
                }
            }

            return points;
        }

        private static bool HasDuplicateFinalControlPoint(List<Vector2> controlPoints)
        {
            return controlPoints.Count > 1 && PointsEqual(controlPoints[controlPoints.Count - 1], controlPoints[controlPoints.Count - 2]);
        }

        private static void AddPoint(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 || !PointsEqual(points[points.Count - 1], point))
                points.Add(point);
        }

        private static bool PointsEqual(Vector2 first, Vector2 second)
        {
            return Vector2.DistanceSquared(first, second) <= PointEpsilon * PointEpsilon;
        }

        private static Vector2 Lerp(Vector2 first, Vector2 second, float progress)
        {
            return first + (second - first) * progress;
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.X * second.Y - first.Y * second.X;
        }

        private static float PositiveAngle(float angle)
        {
            float fullCircle = (float)(2d * System.Math.PI);
            angle %= fullCircle;
            return angle < 0f ? angle + fullCircle : angle;
        }

        private static float Clamp(float value, float min, float max)
        {
            return System.Math.Max(min, System.Math.Min(max, value));
        }
    }
}
