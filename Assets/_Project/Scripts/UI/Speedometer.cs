using PermisVR.Vehicle;
using UnityEngine;

namespace PermisVR.UI
{
    /// Dashboard speed readout in km/h. Labels are prebuilt so Update never allocates.
    public sealed class Speedometer : MonoBehaviour
    {
        private const int MaxDisplayedKmh = 250;

        [SerializeField] private CarController _car;
        [SerializeField] private TextMesh _text;

        private string[] _labels;
        private int _shownKmh = -1;

        private void Awake()
        {
            _labels = new string[MaxDisplayedKmh + 1];
            for (var kmh = 0; kmh <= MaxDisplayedKmh; kmh++)
            {
                _labels[kmh] = kmh + " km/h";
            }
        }

        private void Update()
        {
            var kmh = Mathf.Clamp(Mathf.RoundToInt(_car.SpeedKmh), 0, MaxDisplayedKmh);
            if (kmh == _shownKmh)
            {
                return;
            }

            _shownKmh = kmh;
            _text.text = _labels[kmh];
        }
    }
}
