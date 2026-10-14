// ChemLabSim v3 — small UI cards pinned to points of the 3D bench
// One overlay canvas (drawn under the main UI) holding cards that follow world positions:
// instrument readings next to the probes, reagent labels under the beaker and the flask.
// Cards grow with their text, so long captions never spill out.

using ChemLabSimV3.Views.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Bench3D
{
    public class PinnedCards : MonoBehaviour
    {
        public class Card
        {
            public RectTransform Rect;
            public TextMeshProUGUI Title, Value, Caption;
            public Transform Anchor;
            public Vector3 LocalPoint;     // point on the anchor that the card is pinned to
            public Vector2 Offset;         // in canvas units
            public bool Hidden;
        }

        private RectTransform canvasRect;
        private readonly System.Collections.Generic.List<Card> cards = new System.Collections.Generic.List<Card>();

        public static PinnedCards Create(Transform parent)
        {
            var go = new GameObject("PinnedCards", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1;   // under the lab panels
            var scaler = go.AddComponent<CanvasScaler>();
            bool phone = UnityEngine.Application.isMobilePlatform;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = phone ? new Vector2(1280f, 720f) : new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = phone ? 1f : 0.5f;
            var cards = go.AddComponent<PinnedCards>();
            cards.canvasRect = (RectTransform)go.transform;
            return cards;
        }

        /// <param name="pivot">(0, 0.5) grows to the right of the point, (1, 0.5) to the left, (0.5, 1) below.</param>
        public Card Add(string name, string title, Color tint, Transform anchor, Vector3 localPoint, Vector2 pivot, Vector2 offset,
                        float width, float valueSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(width, 40f);
            LabTheme.Round(go.AddComponent<Image>(), tint, 1.5f);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 6, 7);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var card = new Card
            {
                Rect = rect,
                Anchor = anchor,
                LocalPoint = localPoint,
                Offset = offset,
                Title = Label(rect, title, 11f, new Color(0.55f, 0.85f, 0.7f), FontStyles.Bold),
                Value = Label(rect, "--", valueSize, new Color(0.65f, 1f, 0.82f), FontStyles.Bold),
                Caption = Label(rect, string.Empty, 11f, new Color(0.6f, 0.82f, 0.72f), FontStyles.Normal)
            };
            card.Title.characterSpacing = 4;
            card.Title.gameObject.SetActive(!string.IsNullOrEmpty(title));
            card.Caption.enableWordWrapping = true;
            cards.Add(card);
            return card;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string text, float size, Color colour, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = colour;
            tmp.fontStyle = style;
            tmp.richText = true;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var card in cards)
            {
                bool show = !card.Hidden && card.Anchor != null && card.Anchor.gameObject.activeInHierarchy;
                Vector3 screen = show ? cam.WorldToScreenPoint(card.Anchor.TransformPoint(card.LocalPoint)) : Vector3.zero;
                show &= screen.z > 0f;
                if (card.Rect.gameObject.activeSelf != show) card.Rect.gameObject.SetActive(show);
                if (show && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                    card.Rect.anchoredPosition = local + canvasRect.rect.size * 0.5f + card.Offset;
            }
        }
    }
}
