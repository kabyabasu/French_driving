using PermisVR.Exam;
using PermisVR.Traffic;
using UnityEngine;

namespace PermisVR.ExamUnity
{
    /// Trigger volume that turns "the player car entered me" into an ExamEvent. With forwardOnly,
    /// only entry while moving along this transform's +Z counts (e.g. not reversing back over a line).
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ExamTriggerSensor : MonoBehaviour
    {
        [SerializeField] private ExamSession _session;
        [SerializeField] private ExamEventType _eventType;
        [SerializeField] private string _zoneId;
        [SerializeField] private bool _forwardOnly = true;
        [Tooltip("Optional: the light whose state is attached to the event.")]
        [SerializeField] private TrafficLight _signal;
        [Tooltip("Optional: also publish an event when the car has fully left (e.g. IntersectionExit).")]
        [SerializeField] private bool _publishOnExit;
        [SerializeField] private ExamEventType _exitEventType;

        // The car has several colliders; only the first one entering counts.
        private int _carCollidersInside;

        private void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody == null || other.attachedRigidbody != _session.CarBody)
            {
                return;
            }

            if (_carCollidersInside++ > 0)
            {
                return;
            }

            if (_forwardOnly && Vector3.Dot(_session.CarBody.linearVelocity, transform.forward) <= 0f)
            {
                return;
            }

            _session.Publish(_eventType, _zoneId, _signal != null ? _signal.State : SignalState.None);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.attachedRigidbody == null || other.attachedRigidbody != _session.CarBody || _carCollidersInside == 0)
            {
                return;
            }

            _carCollidersInside--;
            if (_carCollidersInside == 0 && _publishOnExit)
            {
                _session.Publish(_exitEventType, _zoneId, SignalState.None);
            }
        }
    }
}
