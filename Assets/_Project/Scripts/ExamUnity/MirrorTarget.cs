using System;
using UnityEngine;

namespace PermisVR.ExamUnity
{
    /// A mirror the driver can look at; id is a MirrorIds value.
    [Serializable]
    public sealed class MirrorTarget
    {
        public string id;
        public Transform target;
    }
}
