// ChemLabSim v3 — small builders for the runtime-made panels of the right column
// (rounded panel with a title, a row of buttons, a button, a wrapping text), shared by the
// Tests and Learn tabs so they look and behave the same.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Layout
{
    public static class UiKit
    {
        public static RectTransform Panel(RectTransform parent, string name, string title)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            LabTheme.Round(go.AddComponent<Image>(), LabTheme.PanelColor);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 12);
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var t = Text((RectTransform)go.transform, "PanelTitle", 13f, LabTheme.TitleColor);
            t.text = title;
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 4;
            return (RectTransform)go.transform;
        }

        public static RectTransform Row(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 34f;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return (RectTransform)go.transform;
        }

        public static Button AddButton(RectTransform row, string name, string label, Color colour, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = row.gameObject.layer;
            go.transform.SetParent(row, false);
            var image = go.GetComponent<Image>();
            LabTheme.Round(image, colour, 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var text = new GameObject("Label", typeof(RectTransform));
            var tr = (RectTransform)text.transform;
            tr.SetParent(go.transform, false);
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(4f, 0f);
            tr.offsetMax = new Vector2(-4f, 0f);
            var tmp = text.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10f;
            tmp.fontSizeMax = 14f;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return button;
        }

        public static TextMeshProUGUI Text(RectTransform parent, string name, float size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = colour;
            t.richText = true;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            return t;
        }
    }
}
