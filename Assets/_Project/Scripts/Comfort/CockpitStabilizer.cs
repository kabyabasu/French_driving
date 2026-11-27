using UnityEngine;

namespace PermisVR.Comfort
{
    /// Keeps what the driver sees level: the cockpit, interior, mirrors and XR rig follow the car's
    /// position and heading but not its suspension pitch, roll or bounce (comfort rule: the camera
    /// never moves independently of the car's travel, and suspension motion is felt by nobody's
    /// inner ear). Physics is untouched: these are visual children with no colliders, re-posed in
    /// LateUpdate after Rigidbody interpolation and before rendering.
    [DefaultExecutionOrder(-1000)]
    public sealed class CockpitStabilizer : MonoBehaviour
    {
        [SerializeField] private ComfortConfig _config;
        [Tooltip("Visual-only children of the car (no colliders), e.g. Cockpit, Hatchback, Mirrors.")]
        [SerializeField] private Transform[] _visuals;

        private Vector3[] _localPositions;
        private Quaternion[] _localRotations;
        private float _smoothedHeight;
        private float _heightVelocity;
        private bool _initialised;

        private void Awake()
        {
            _localPositions = new Vector3[_visuals.Length];
            _localRotations = new Quaternion[_visuals.Length];
            for (var i = 0; i < _visuals.Length; i++)
            {
                _localPositions[i] = _visuals[i].localPosition;
                _localRotations[i] = _visuals[i].localRotation;
            }
        }

        private void LateUpdate()
        {
            var body = transform;
            var position = body.position;
            if (!_initialised)
            {
                _smoothedHeight = position.y;
                _initialised = true;
            }

            _smoothedHeight = Mathf.SmoothDamp(_smoothedHeight, position.y, ref _heightVelocity,
                Mathf.Max(_config.heightSmoothingS, 0.01f));

            var forward = Vector3.ProjectOnPlane(body.forward, Vector3.up);
            var heading = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, Vector3.up) : body.rotation;
            var rotation = Quaternion.Slerp(heading, body.rotation, _config.pitchRollFollow);
            var origin = new Vector3(position.x, _smoothedHeight, position.z);

            for (var i = 0; i < _visuals.Length; i++)
            {
                _visuals[i].SetPositionAndRotation(origin + rotation * _localPositions[i], rotation * _localRotations[i]);
            }
        }
    }
}
