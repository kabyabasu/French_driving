using PermisVR.Exam;
using PermisVR.ExamUnity.Config;
using UnityEngine;

namespace PermisVR.ExamUnity
{
    /// Publishes MirrorChecked when the head stays pointed at a mirror for the dwell time, and
    /// BlindSpotChecked when the head stays turned over a shoulder (yaw relative to the car) while
    /// not looking at a mirror: the right door mirror sits near the shoulder-check yaw, and a glance
    /// at it must not count as an angle-mort check.
    /// One event per glance: looking away re-arms it.
    public sealed class MirrorCheckSensor : MonoBehaviour
    {
        private const int Left = 0;
        private const int Right = 1;

        [SerializeField] private ExamSession _session;
        [SerializeField] private Transform _head;
        [SerializeField] private MirrorGazeConfigAsset _config;
        [SerializeField] private MirrorTarget[] _mirrors;

        private float[] _dwellS;
        private bool[] _reported;
        private readonly float[] _blindSpotDwellS = new float[2];
        private readonly bool[] _blindSpotReported = new bool[2];

        private void Awake()
        {
            _dwellS = new float[_mirrors.Length];
            _reported = new bool[_mirrors.Length];
        }

        private void Update()
        {
            var headPosition = _head.position;
            var headForward = _head.forward;
            var lookingAtMirror = false;
            for (var i = 0; i < _mirrors.Length; i++)
            {
                var angle = Vector3.Angle(headForward, _mirrors[i].target.position - headPosition);
                if (angle > _config.gazeAngleDeg)
                {
                    _dwellS[i] = 0f;
                    _reported[i] = false;
                    continue;
                }

                lookingAtMirror = true;
                _dwellS[i] += Time.deltaTime;
                if (!_reported[i] && _dwellS[i] >= _config.dwellS)
                {
                    _reported[i] = true;
                    _session.Publish(ExamEventType.MirrorChecked, _mirrors[i].id, SignalState.None);
                }
            }

            UpdateBlindSpots(headForward, lookingAtMirror);
        }

        private void UpdateBlindSpots(Vector3 headForward, bool lookingAtMirror)
        {
            var carBody = _session.CarBody;
            if (carBody == null)
            {
                return;
            }

            // Yaw of the head around the car's up axis; positive = looking right.
            var car = carBody.transform;
            var yawDeg = Vector3.SignedAngle(Vector3.ProjectOnPlane(car.forward, car.up),
                Vector3.ProjectOnPlane(headForward, car.up), car.up);
            UpdateSide(Left, !lookingAtMirror && yawDeg <= -_config.blindSpotYawDeg, BlindSpotIds.Left);
            UpdateSide(Right, !lookingAtMirror && yawDeg >= _config.blindSpotYawDeg, BlindSpotIds.Right);
        }

        private void UpdateSide(int side, bool turned, string id)
        {
            if (!turned)
            {
                _blindSpotDwellS[side] = 0f;
                _blindSpotReported[side] = false;
                return;
            }

            _blindSpotDwellS[side] += Time.deltaTime;
            if (!_blindSpotReported[side] && _blindSpotDwellS[side] >= _config.blindSpotDwellS)
            {
                _blindSpotReported[side] = true;
                _session.Publish(ExamEventType.BlindSpotChecked, id, SignalState.None);
            }
        }
    }
}
