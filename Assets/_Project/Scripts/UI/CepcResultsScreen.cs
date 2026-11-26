using System.Text;
using PermisVR.Exam;
using PermisVR.ExamUnity;
using PermisVR.ExamUnity.Config;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PermisVR.UI
{
    /// World-space CEPC-style results sheet in the cockpit, toggled by the Pause action.
    /// Rows are built once in Awake; text is only rewritten when the sheet is opened.
    public sealed class CepcResultsScreen : MonoBehaviour
    {
        private const float CanvasWidth = 800f;
        private const float Margin = 24f;
        private const float CellWidth = 40f;
        private const float RowHeight = 30f;
        private const float FamilyRowHeight = 28f;
        private const int MaxCellPoints = 4;
        private const int EliminatedCell = MaxCellPoints + 1;
        private const int MaxListedFaults = 6;

        private static readonly Color Ink = new(0.12f, 0.12f, 0.16f);
        private static readonly Color Muted = new(0.45f, 0.45f, 0.5f);
        private static readonly Color FamilyBand = new(0.86f, 0.87f, 0.9f);
        private static readonly Color EmptyCell = new(0.92f, 0.92f, 0.92f);
        private static readonly Color ScoredCell = new(0.13f, 0.25f, 0.55f);
        private static readonly Color EliminatedFill = new(0.8f, 0.1f, 0.1f);
        private static readonly Color PassGreen = new(0.1f, 0.5f, 0.2f);

        [SerializeField] private ExamSession _session;
        [SerializeField] private CepcGridAsset _gridAsset;
        [SerializeField] private InputActionReference _toggleAction;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _content;
        [SerializeField] private Font _font;

        private CepcGrid _grid;
        private Image[][] _cells;
        private Text[][] _cellLabels;
        private Text _totalText;
        private Text _resultText;
        private Text _faultsText;
        private readonly StringBuilder _builder = new();

        private void Awake()
        {
            _grid = _gridAsset.ToGrid();
            BuildSheet();
            _canvas.enabled = false;
        }

        private void Update()
        {
            if (_toggleAction != null && _toggleAction.action.WasPressedThisFrame())
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            if (!_canvas.enabled)
            {
                Refresh();
            }

            _canvas.enabled = !_canvas.enabled;
        }

        private void BuildSheet()
        {
            var y = Margin;
            CreateText("CERTIFICAT D'EXAMEN DU PERMIS DE CONDUIRE", Margin, y, CanvasWidth - 2 * Margin, 34, 26,
                FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            y += 36f;
            CreateText("Bilan des compétences — épreuve pratique, catégorie B", Margin, y, CanvasWidth - 2 * Margin, 24, 17,
                FontStyle.Normal, TextAnchor.MiddleCenter, Muted);
            y += 36f;

            var cellsX = CanvasWidth - Margin - (MaxCellPoints + 2) * CellWidth;
            CreateText("Compétence", Margin, y, cellsX - Margin, 24, 15, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            for (var cell = 0; cell <= EliminatedCell; cell++)
            {
                CreateText(cell == EliminatedCell ? "E" : cell.ToString(), cellsX + cell * CellWidth, y, CellWidth, 24, 15,
                    FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            }

            y += 28f;
            _cells = new Image[_grid.Criteria.Count][];
            _cellLabels = new Text[_grid.Criteria.Count][];
            string family = null;

            for (var i = 0; i < _grid.Criteria.Count; i++)
            {
                var criterion = _grid.Criteria[i];
                if (criterion.FamilyFr != family)
                {
                    family = criterion.FamilyFr;
                    CreateImage(Margin, y, CanvasWidth - 2 * Margin, FamilyRowHeight - 2, FamilyBand);
                    CreateText(family, Margin + 8, y, CanvasWidth - 2 * Margin - 16, FamilyRowHeight - 2, 15,
                        FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
                    y += FamilyRowHeight;
                }

                var label = criterion.IsEvaluated ? criterion.LabelFr : criterion.LabelFr + "  (non évalué)";
                CreateText(label, Margin + 12, y, cellsX - Margin - 16, RowHeight, 15,
                    criterion.IsEvaluated ? FontStyle.Normal : FontStyle.Italic, TextAnchor.MiddleLeft,
                    criterion.IsEvaluated ? Ink : Muted);

                _cells[i] = new Image[EliminatedCell + 1];
                _cellLabels[i] = new Text[EliminatedCell + 1];
                for (var cell = 0; cell <= EliminatedCell; cell++)
                {
                    var exists = cell == EliminatedCell ? !criterion.IsBonus : cell <= criterion.MaxPoints;
                    if (!exists)
                    {
                        continue;
                    }

                    var x = cellsX + cell * CellWidth + 4;
                    _cells[i][cell] = CreateImage(x, y + 3, CellWidth - 8, RowHeight - 6, EmptyCell);
                    _cellLabels[i][cell] = CreateText(cell == EliminatedCell ? "E" : cell.ToString(), x, y + 3,
                        CellWidth - 8, RowHeight - 6, 14, FontStyle.Normal, TextAnchor.MiddleCenter, Muted);
                }

                y += RowHeight;
            }

            y += 14f;
            _totalText = CreateText(string.Empty, Margin, y, CanvasWidth - 2 * Margin, 30, 21, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            y += 32f;
            _resultText = CreateText(string.Empty, Margin, y, CanvasWidth - 2 * Margin, 34, 24, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            y += 44f;
            CreateText("Fautes relevées", Margin, y, CanvasWidth - 2 * Margin, 22, 16, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            y += 24f;
            _faultsText = CreateText(string.Empty, Margin, y, CanvasWidth - 2 * Margin, MaxListedFaults * 22, 14,
                FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            y += MaxListedFaults * 22 + 8;
            CreateText("Barème à vérifier avec le Guide B officiel. Lignes non évaluées : comptées au maximum. " +
                       "Bouton menu (manette gauche) pour fermer.", Margin, y, CanvasWidth - 2 * Margin, 36, 12,
                FontStyle.Italic, TextAnchor.UpperLeft, Muted);
        }

        private void Refresh()
        {
            var result = _session.ScoreSheet.Grade(_grid);

            for (var i = 0; i < result.Scores.Count; i++)
            {
                var score = result.Scores[i];
                for (var cell = 0; cell <= EliminatedCell; cell++)
                {
                    var box = _cells[i][cell];
                    if (box == null)
                    {
                        continue;
                    }

                    var filled = cell == EliminatedCell ? score.IsEliminated : !score.IsEliminated && cell == score.Points;
                    box.color = filled ? (cell == EliminatedCell ? EliminatedFill : ScoredCell) : EmptyCell;
                    _cellLabels[i][cell].color = filled ? Color.white : Muted;
                }
            }

            _totalText.text = $"Total : {result.TotalPoints} / {result.MaxTotalPoints}    (minimum requis : {result.PassThresholdPoints})";

            if (result.Passed)
            {
                _resultText.text = "Résultat : FAVORABLE";
                _resultText.color = PassGreen;
            }
            else
            {
                _resultText.text = result.HasEliminatoryFault
                    ? "Résultat : DÉFAVORABLE — faute éliminatoire"
                    : "Résultat : DÉFAVORABLE — total insuffisant";
                _resultText.color = EliminatedFill;
            }

            _faultsText.text = DescribeFaults(result);
        }

        private string DescribeFaults(ExamResult result)
        {
            if (result.Faults.Count == 0)
            {
                return "Aucune faute relevée.";
            }

            _builder.Clear();
            var first = Mathf.Max(0, result.Faults.Count - MaxListedFaults);
            for (var i = first; i < result.Faults.Count; i++)
            {
                var fault = result.Faults[i];
                var seconds = Mathf.FloorToInt(fault.TimeS);
                _builder.Append($"{seconds / 60:00}:{seconds % 60:00}   ");
                _builder.Append(fault.Severity == FaultSeverity.Eliminatory ? "[E] " : "[–] ");
                _builder.Append(fault.ExplanationFr);
                _builder.Append($"   (x {fault.PositionX:0}, z {fault.PositionZ:0})");
                _builder.Append('\n');
            }

            if (first > 0)
            {
                _builder.Append($"… et {first} faute(s) plus ancienne(s)");
            }

            return _builder.ToString();
        }

        private RectTransform CreateRect(string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_content, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private Image CreateImage(float x, float y, float width, float height, Color color)
        {
            var image = CreateRect("Box", x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(string content, float x, float y, float width, float height, int size, FontStyle style,
            TextAnchor anchor, Color color)
        {
            var text = CreateRect("Text", x, y, width, height).gameObject.AddComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
