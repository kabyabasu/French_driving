using PermisVR.Exam;
using PermisVR.Exam.Rules;
using PermisVR.ExamUnity.Config;
using PermisVR.Vehicle;
using UnityEngine;

namespace PermisVR.ExamUnity
{
    /// Owns one exam run: the event bus, the rule evaluators and the ScoreSheet. Publishes a
    /// VehicleTick every physics step; scene sensors publish zone/line events through Publish.
    public sealed class ExamSession : MonoBehaviour
    {
        [SerializeField] private CarController _car;
        [SerializeField] private StopSignRuleConfigAsset _stopSignConfig;
        [SerializeField] private MirrorRulesConfigAsset _mirrorRulesConfig;

        private readonly ExamEventBus _bus = new();
        private Rigidbody _carBody;

        public ScoreSheet ScoreSheet { get; } = new();
        public Rigidbody CarBody => _carBody;

        private void Awake()
        {
            _carBody = _car.GetComponent<Rigidbody>();
            Register(new StopSignRule(_stopSignConfig.ToConfig()));
            Register(new RedLightRule());

            if (_mirrorRulesConfig == null)
            {
                Debug.LogError("ExamSession: no MirrorRulesConfig assigned, mirror and blind-spot checks are NOT graded. " +
                               "Run PermisVR > Scene > Add Mirrors.");
                return;
            }

            var mirrorRules = _mirrorRulesConfig.ToConfig();
            Register(new MirrorBeforeTurnRule(mirrorRules));
            Register(new MirrorBeforePullOutRule(mirrorRules));
            Register(new MirrorBeforeBrakingRule(mirrorRules));
            Register(new BlindSpotBeforePullOutRule(mirrorRules));
            Register(new BlindSpotBeforeTurnRule(mirrorRules));
        }

        private void Register(IExamRule rule)
        {
            ScoreSheet.Watch(rule);
            _bus.EventRaised += evt => rule.HandleEvent(evt);
        }

        private void FixedUpdate()
        {
            _bus.Publish(new ExamEvent(ExamEventType.VehicleTick, Snapshot()));
        }

        public void Publish(ExamEventType type, string zoneId, SignalState signal)
        {
            _bus.Publish(new ExamEvent(type, Snapshot(), zoneId, signal));
        }

        private VehicleSnapshot Snapshot()
        {
            var carTransform = _car.transform;
            var position = carTransform.position;
            // Speed along the road only: the car settling on its suspension at spawn (vertical
            // motion) must not look like moving off. Physics is m/s; km/h from here on.
            var speedKmh = Vector3.ProjectOnPlane(_carBody.linearVelocity, carTransform.up).magnitude * 3.6f;
            // Route position stays 0 until exam routes exist (M5).
            return new VehicleSnapshot(Time.time, speedKmh, 0f, carTransform.eulerAngles.y, position.x, position.z);
        }
    }
}
