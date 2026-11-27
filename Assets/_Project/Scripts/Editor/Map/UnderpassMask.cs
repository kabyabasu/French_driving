using System.Collections.Generic;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// The ground area each underpass covers. The OSM street graph is flat, so a street that
    /// really passes OVER an underpass is drawn on the ground; segments of it that cross the
    /// covered area are hidden, leaving the deck and embankments to stand in for it. Segments
    /// running along the underpass belong to the street underneath and are kept.
    internal sealed class UnderpassMask
    {
        private const float ParallelDot = 0.7f;

        private readonly List<(Vector3[] points, float halfWidthM, Bounds bounds)> _areas = new();

        public UnderpassMask(IEnumerable<MapUnderpass> underpasses, float coverBeyondRoadM)
        {
            foreach (var underpass in underpasses)
            {
                if (underpass.points == null || underpass.points.Length < 2)
                {
                    continue;
                }

                var points = new Vector3[underpass.points.Length];
                var bounds = new Bounds(underpass.points[0].ToVector3(0f), Vector3.zero);
                for (var i = 0; i < points.Length; i++)
                {
                    points[i] = underpass.points[i].ToVector3(0f);
                    bounds.Encapsulate(points[i]);
                }

                var halfWidth = underpass.roadWidthM * 0.5f + coverBeyondRoadM;
                bounds.Expand(new Vector3(halfWidth * 2f, 100f, halfWidth * 2f));
                _areas.Add((points, halfWidth, bounds));
            }
        }

        public bool IsEmpty => _areas.Count == 0;

        /// True if the a-b segment could reach into a covered area (cheap bounds test).
        public bool MayTouch(Vector3 a, Vector3 b)
        {
            var segment = new Bounds(a, Vector3.zero);
            segment.Encapsulate(b);
            segment.Expand(new Vector3(0f, 100f, 0f));
            foreach (var area in _areas)
            {
                if (area.bounds.Intersects(segment))
                {
                    return true;
                }
            }

            return false;
        }

        /// True if a short piece at `point` heading `direction` crosses a covered area.
        public bool HidesCrossing(Vector3 point, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            direction.Normalize();
            point.y = 0f;
            foreach (var (points, halfWidthM, bounds) in _areas)
            {
                if (!bounds.Contains(point))
                {
                    continue;
                }

                for (var i = 0; i < points.Length - 1; i++)
                {
                    var along = points[i + 1] - points[i];
                    if (along.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }

                    // Only beside the underpass, not past its ends: streets joining just outside a
                    // tunnel mouth stay visible.
                    var t = Vector3.Dot(point - points[i], along) / along.sqrMagnitude;
                    if (t < 0f || t > 1f)
                    {
                        continue;
                    }

                    var closest = points[i] + along * t;
                    if ((point - closest).sqrMagnitude <= halfWidthM * halfWidthM
                        && Mathf.Abs(Vector3.Dot(direction, along.normalized)) < ParallelDot)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
