using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace PermisVR.Input
{
    /// Forces the seated ("Device") tracking origin, per CLAUDE.md's comfort rule that this
    /// is seated play with the cockpit as a stable frame of reference. Without this, OpenXR's
    /// default room-scale ("Floor") origin maps the player's physical position in their room
    /// to world space, instead of starting them in the car's cockpit.
    public sealed class SeatedTrackingOrigin : MonoBehaviour
    {
        private readonly List<XRInputSubsystem> _subsystems = new();

        private void Start()
        {
            SubsystemManager.GetSubsystems(_subsystems);
            foreach (var subsystem in _subsystems)
            {
                subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
                subsystem.TryRecenter();
            }
        }
    }
}
