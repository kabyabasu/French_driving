using System;
using UnityEngine;

namespace PermisVR.Editor.Map
{
    /// A map position in metres from the exam start point: +X east, +Z north.
    [Serializable]
    public sealed class MapPoint
    {
        public float x;
        public float z;

        public Vector3 ToVector3(float y) => new(x, y, z);
    }
}
