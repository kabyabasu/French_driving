using PermisVR.Exam;
using PermisVR.ExamUnity.Config;
using UnityEngine;

namespace PermisVR.ExamUnity
{
    /// Publishes MirrorChecked when the head stays pointed at a mirror for the dwell time.
    /// One event per glance: looking away re-arms it.
    public sealed class MirrorCheckSensor : MonoBehaviour
    {
        [SerializeField] private ExamSession _session;
        [SerializeField] private Transform _head;
        [SerializeField] private MirrorGazeConfigAsset _config;
        [SerializeField] private MirrorTarget[] _mirrors;

        private float[] _dwellS;
        private bool[] _reported;

        private void Awake()
        {
            _dwellS = new float[_mirrors.Length];
            _reported = new bool[_mirrors.Length];
        }

        private void Update()
        {
            var headPosition = _head.position;
            var headForward = _head.forward;
            for (var i = 0; i < _mirrors.Length; i++)
            {
                var angle = Vector3.Angle(headForward, _mirrors[i].target.position - headPosition);
                if (angle > _config.gazeAngleDeg)
                {
                    _dwellS[i] = 0f;
                    _reported[i] = false;
                    continue;
                }

                _dwellS[i] += Time.deltaTime;
                if (!_reported[i] && _dwellS[i] >= _config.dwellS)
                {
                    _reported[i] = true;
                    _session.Publish(ExamEventType.MirrorChecked, _mirrors[i].id, SignalState.None);
                }
            }
        }
    }
}
