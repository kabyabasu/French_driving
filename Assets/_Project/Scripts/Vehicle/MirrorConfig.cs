using UnityEngine;

namespace PermisVR.Vehicle
{
    /// Cost controls for the rendered mirrors (each one is an extra camera on the Quest GPU).
    [CreateAssetMenu(fileName = "MirrorConfig", menuName = "PermisVR/Vehicle/Mirror Config")]
    public sealed class MirrorConfig : ScriptableObject
    {
        [Tooltip("Mirror cameras don't draw beyond this.")]
        public float farClipM = 80f;
        [Tooltip("Each mirror redraws every Nth frame (staggered between mirrors).")]
        [Range(1, 4)] public int renderEveryNFrames = 2;
        [Tooltip("Turn rendered mirrors off entirely, e.g. to measure their cost.")]
        public bool renderMirrors = true;
    }
}
