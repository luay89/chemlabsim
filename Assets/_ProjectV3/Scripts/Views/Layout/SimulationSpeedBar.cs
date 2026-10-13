// ChemLabSim v3 — simulation speed bar
// Pause / slow motion / normal / fast for everything that runs on scaled time
// (live simulation, particles, liquid). The UI keeps working while paused.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Layout
{
    public class SimulationSpeedBar : MonoBehaviour
    {
        private static readonly (string label, float scale)[] Speeds =
        {
            ("Pause", 0f), ("0.25×", 0.25f), ("1×", 1f), ("3×", 3f)
        };

        private Image[] buttons;

        public static SimulationSpeedBar Create(RectTransform parent)
        {
            var go = new GameObject("SpeedBar", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(350f, 44f);
            LabTheme.Round(go.AddComponent<Image>(), LabTheme.PanelColor, 1.5f);

            var row = go.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(6, 6, 6, 6);
            row.spacing = 6f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;

            var bar = go.AddComponent<SimulationSpeedBar>();
            bar.buttons = new Image[Speeds.Length];
            for (int i = 0; i < Speeds.Length; i++)
                bar.buttons[i] = bar.AddButton(rect, i);
            bar.Select(2);
            return bar;
        }

        private Image AddButton(RectTransform parent, int index)
        {
            var go = new GameObject(Speeds[index].label + "Speed", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            LabTheme.Round(image, LabTheme.ControlColor, 2.5f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Select(index));

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.layer = go.layer;
            var tr = (RectTransform)textGo.transform;
            tr.SetParent(go.transform, false);
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = Speeds[index].label;
            text.fontSize = 15;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return image;
        }

        private void Select(int index)
        {
            Time.timeScale = Speeds[index].scale;
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].color = i == index ? LabTheme.Accent : LabTheme.ControlColor;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;   // never leave the app paused or fast after the lab closes
        }
    }
}
