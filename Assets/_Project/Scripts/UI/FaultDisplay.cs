using PermisVR.Exam;
using PermisVR.ExamUnity;
using UnityEngine;

namespace PermisVR.UI
{
    /// Interim dashboard readout of the latest exam fault (the CEPC results screen comes in M4).
    /// Text is only built when a fault happens, never per frame.
    public sealed class FaultDisplay : MonoBehaviour
    {
        [SerializeField] private ExamSession _session;
        [SerializeField] private TextMesh _text;
        [SerializeField] private Color _eliminatoryColor = new(1f, 0.25f, 0.2f);
        [SerializeField] private Color _minorColor = new(1f, 0.75f, 0.2f);

        private int _faultCount;

        private void OnEnable()
        {
            _session.ScoreSheet.FaultAdded += Show;
            _text.text = string.Empty;
        }

        private void OnDisable()
        {
            _session.ScoreSheet.FaultAdded -= Show;
        }

        private void Show(Fault fault)
        {
            _faultCount++;
            var eliminatory = fault.Severity == FaultSeverity.Eliminatory;
            _text.color = eliminatory ? _eliminatoryColor : _minorColor;
            var label = eliminatory ? "FAUTE ÉLIMINATOIRE" : "Faute";
            _text.text = $"{label} ({_faultCount})\n{fault.ExplanationFr}";
        }
    }
}
