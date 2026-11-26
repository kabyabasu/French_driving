using PermisVR.Exam;
using UnityEngine;

namespace PermisVR.Traffic
{
    /// Cycles one signal head through green, amber and red; lamps swap materials only on change.
    public sealed class TrafficLight : MonoBehaviour
    {
        [SerializeField] private TrafficLightTiming _timing;
        [Tooltip("0 for phase A; half a cycle for the crossing phase B at the same junction.")]
        [SerializeField] private float _phaseOffsetS;

        [Header("Lamps")]
        [SerializeField] private Renderer _redLamp;
        [SerializeField] private Renderer _amberLamp;
        [SerializeField] private Renderer _greenLamp;
        [SerializeField] private Material _redOn;
        [SerializeField] private Material _amberOn;
        [SerializeField] private Material _greenOn;
        [SerializeField] private Material _off;

        public SignalState State { get; private set; } = SignalState.None;

        private void Update()
        {
            var state = StateAt(Time.time);
            if (state == State)
            {
                return;
            }

            State = state;
            _redLamp.sharedMaterial = state == SignalState.Red ? _redOn : _off;
            _amberLamp.sharedMaterial = state == SignalState.Amber ? _amberOn : _off;
            _greenLamp.sharedMaterial = state == SignalState.Green ? _greenOn : _off;
        }

        private SignalState StateAt(float timeS)
        {
            var t = Mathf.Repeat(timeS + _phaseOffsetS, _timing.CycleS);
            if (t < _timing.greenS)
            {
                return SignalState.Green;
            }

            return t < _timing.greenS + _timing.amberS ? SignalState.Amber : SignalState.Red;
        }
    }
}
