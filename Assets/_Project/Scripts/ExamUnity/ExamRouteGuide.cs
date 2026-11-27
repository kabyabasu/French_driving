using System;
using System.Text;
using PermisVR.ExamUnity.Config;
using PermisVR.ExamUnity.RouteData;
using PermisVR.Route;
using UnityEngine;

namespace PermisVR.ExamUnity
{
    /// The examiner on the passenger seat: follows the car along the exam route and speaks each
    /// instruction (pre-recorded clip + subtitle). Also gives ExamSession the route position that
    /// every fault records.
    public sealed class ExamRouteGuide : MonoBehaviour
    {
        private const int SubtitleLineChars = 38;

        [SerializeField] private TextAsset _routeJson;
        [SerializeField] private ExaminerConfigAsset _config;
        [SerializeField] private Rigidbody _carBody;
        [SerializeField] private AudioSource _voice;
        [Tooltip("One clip per ExaminerPhrase, in enum order.")]
        [SerializeField] private AudioClip[] _clips;
        [SerializeField] private TextMesh _subtitle;

        private RouteTracker _tracker;
        private ExaminerScheduler _scheduler;
        private string[] _subtitles;
        private float _subtitleUntilS = float.NegativeInfinity;

        public bool HasRoute => _tracker != null;
        public float RoutePositionM => _tracker != null ? _tracker.PositionM : 0f;
        public float RouteLengthM { get; private set; }
        public string RouteName { get; private set; } = "";

        private void Awake()
        {
            if (_routeJson == null || _config == null || _carBody == null)
            {
                Debug.LogError("ExamRouteGuide: route, config or car missing; run PermisVR > Route > Set Up Exam Route.");
                enabled = false;
                return;
            }

            RouteDefinition route;
            try
            {
                route = RouteJsonLoader.Load(_routeJson.text, out var raw);
                RouteName = raw.name ?? "";
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"ExamRouteGuide: can't read {_routeJson.name}: {e.Message}");
                enabled = false;
                return;
            }

            RouteLengthM = route.LengthM;
            _tracker = new RouteTracker(route, _config.ToTrackerConfig());
            _scheduler = new ExaminerScheduler(route, _config.ToConfig());

            var phrases = (ExaminerPhrase[])Enum.GetValues(typeof(ExaminerPhrase));
            _subtitles = new string[phrases.Length];
            foreach (var phrase in phrases)
            {
                _subtitles[(int)phrase] = Wrap(ExaminerPhrases.Text(phrase));
            }

            if (_subtitle != null)
            {
                _subtitle.text = "";
            }
        }

        private void FixedUpdate()
        {
            var carTransform = _carBody.transform;
            var position = carTransform.position;
            _tracker.Update(position.x, position.z);
            var speedKmh = Vector3.ProjectOnPlane(_carBody.linearVelocity, carTransform.up).magnitude * 3.6f;
            if (_scheduler.Tick(Time.time, _tracker.PositionM, _tracker.LateralM, speedKmh, out var phrase))
            {
                Speak(phrase);
            }
        }

        private void Update()
        {
            if (_subtitle != null && Time.time > _subtitleUntilS && _subtitle.text.Length > 0)
            {
                _subtitle.text = "";
            }
        }

        private void Speak(ExaminerPhrase phrase)
        {
            var index = (int)phrase;
            var clip = _clips != null && index < _clips.Length ? _clips[index] : null;
            var lengthS = 3f;
            if (clip != null && _voice != null)
            {
                _voice.clip = clip;
                _voice.Play();
                lengthS = clip.length;
            }

            if (_subtitle != null)
            {
                _subtitle.text = _subtitles[index];
                _subtitleUntilS = Time.time + lengthS + _config.subtitleExtraS;
            }
        }

        private static string Wrap(string text)
        {
            var result = new StringBuilder(text.Length + 8);
            var lineLength = 0;
            foreach (var word in text.Split(' '))
            {
                if (lineLength > 0 && lineLength + 1 + word.Length > SubtitleLineChars)
                {
                    result.Append('\n');
                    lineLength = 0;
                }
                else if (lineLength > 0)
                {
                    result.Append(' ');
                    lineLength++;
                }

                result.Append(word);
                lineLength += word.Length;
            }

            return result.ToString();
        }
    }
}
