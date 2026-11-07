// ChemLabSim v3 — amount input under a reagent row
// [ 25.0 ] [ mL ]  1.0 M bench stock
// The unit button only cycles through the units that make sense for the reagent's state
// (given by the controller); the note shows the bottle concentration or the density used,
// or says that data is missing. No chemistry here — values go to LabInputController.

using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views
{
    public class ReagentAmountView : MonoBehaviour
    {
        public event Action<double, string> OnAmountChanged;

        private TMP_InputField field;
        private TextMeshProUGUI unitLabel, note;
        private string[] units = { "g" };
        private int unitIndex;

        public double Value { get; private set; } = double.NaN;
        public string Unit => units.Length > 0 ? units[unitIndex] : string.Empty;

        /// <summary>Adds an amount row right below the reagent row in the Lab Controls panel.</summary>
        public static ReagentAmountView Attach(ReagentDropdownView reagentRow)
        {
            var row = reagentRow.transform;
            var font = reagentRow.GetComponentInChildren<TextMeshProUGUI>(true)?.font;

            var go = new GameObject(row.name.Replace("Row", "") + "AmountRow", typeof(RectTransform));
            go.layer = row.gameObject.layer;
            go.transform.SetParent(row.parent, false);
            go.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = 26f;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var view = go.AddComponent<ReagentAmountView>();
            view.Build(go.transform, font);
            return view;
        }

        private void Build(Transform parent, TMP_FontAsset font)
        {
            field = CreateField(parent, font);
            field.onEndEdit.AddListener(_ => ReadField());

            var button = CreateBox(parent, "Unit", 52f, new Color(0.16f, 0.22f, 0.32f));
            var b = button.gameObject.AddComponent<Button>();
            b.targetGraphic = button.GetComponent<Image>();
            b.onClick.AddListener(NextUnit);
            unitLabel = CreateText(button.transform, font, 13f, TextAlignmentOptions.Center, Color.white);

            var noteGo = new GameObject("Note", typeof(RectTransform));
            noteGo.transform.SetParent(parent, false);
            noteGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
            note = CreateText(noteGo.transform, font, 11f, TextAlignmentOptions.MidlineLeft, new Color(0.62f, 0.7f, 0.8f));
        }

        /// <summary>New reagent: allowed units, a starting amount and the note about its data.</summary>
        public void Configure(string[] allowedUnits, double defaultValue, string noteText)
        {
            gameObject.SetActive(allowedUnits != null && allowedUnits.Length > 0);
            units = allowedUnits != null && allowedUnits.Length > 0 ? allowedUnits : new[] { "g" };
            unitIndex = 0;
            Value = defaultValue;
            field.SetTextWithoutNotify(defaultValue.ToString("0.###", CultureInfo.InvariantCulture));
            unitLabel.text = Unit;
            note.text = noteText ?? string.Empty;
        }

        public void SetNote(string text) => note.text = text ?? string.Empty;

        /// <summary>Sets the amount as if typed (keeps the current unit).</summary>
        public void SetValue(double value)
        {
            field.SetTextWithoutNotify(value.ToString("0.###", CultureInfo.InvariantCulture));
            ReadField();
        }

        private void NextUnit()
        {
            unitIndex = (unitIndex + 1) % units.Length;
            unitLabel.text = Unit;
            OnAmountChanged?.Invoke(Value, Unit);
        }

        private void ReadField()
        {
            string text = (field.text ?? string.Empty).Trim().Replace(',', '.');
            Value = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
            OnAmountChanged?.Invoke(Value, Unit);
        }

        // ── Building blocks ────────────────────────────────────────────

        private static TMP_InputField CreateField(Transform parent, TMP_FontAsset font)
        {
            var box = CreateBox(parent, "Amount", 70f, new Color(0.04f, 0.06f, 0.10f));
            box.gameObject.SetActive(false);   // configure before TMP_InputField.OnEnable runs
            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            var ar = (RectTransform)area.transform;
            ar.SetParent(box, false);
            ar.anchorMin = Vector2.zero;
            ar.anchorMax = Vector2.one;
            ar.offsetMin = new Vector2(6f, 0f);
            ar.offsetMax = new Vector2(-6f, 0f);
            var text = CreateText(ar, font, 13f, TextAlignmentOptions.MidlineRight, Color.white);
            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = ar;
            field.textComponent = text;
            field.contentType = TMP_InputField.ContentType.DecimalNumber;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 8;
            field.targetGraphic = box.GetComponent<Image>();
            box.gameObject.SetActive(true);
            return field;
        }

        private static RectTransform CreateBox(Transform parent, string name, float width, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = colour;
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI CreateText(Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align, Color colour)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.color = colour;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
