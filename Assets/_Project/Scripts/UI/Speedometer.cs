using PermisVR.Vehicle;
using UnityEngine;

namespace PermisVR.UI
{
    /// Dashboard gear + speed readout ("D 42 km/h"). Labels are prebuilt so Update never allocates.
    public sealed class Speedometer : MonoBehaviour
    {
        private const int MaxDisplayedKmh = 250;

        [SerializeField] private CarController _car;
        [SerializeField] private TextMesh _text;

        private string[] _driveLabels;
        private string[] _reverseLabels;
        private int _shownKmh = -1;
        private Gear _shownGear;

        private void Awake()
        {
            _driveLabels = BuildLabels("D ");
            _reverseLabels = BuildLabels("R ");
        }

        private static string[] BuildLabels(string prefix)
        {
            var labels = new string[MaxDisplayedKmh + 1];
            for (var kmh = 0; kmh <= MaxDisplayedKmh; kmh++)
            {
                labels[kmh] = prefix + kmh + " km/h";
            }

            return labels;
        }

        private void Update()
        {
            var kmh = Mathf.Clamp(Mathf.RoundToInt(_car.SpeedKmh), 0, MaxDisplayedKmh);
            var gear = _car.CurrentGear;
            if (kmh == _shownKmh && gear == _shownGear)
            {
                return;
            }

            _shownKmh = kmh;
            _shownGear = gear;
            _text.text = gear == Gear.Reverse ? _reverseLabels[kmh] : _driveLabels[kmh];
        }
    }
}
