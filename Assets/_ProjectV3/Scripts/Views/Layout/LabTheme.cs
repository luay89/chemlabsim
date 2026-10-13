// ChemLabSim v3 — LabV3 visual theme (runtime)
// One look for every panel and control: rounded corners from a sprite generated in code
// (no texture assets), a single panel colour, accent colour for titles and the Mix button.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Layout
{
    public static class LabTheme
    {
        public static readonly Color PanelColor  = new Color(0.055f, 0.08f, 0.125f, 0.90f);
        public static readonly Color ControlColor = new Color(0.11f, 0.15f, 0.22f, 1f);
        public static readonly Color Accent      = new Color(0.25f, 0.58f, 1f, 1f);
        public static readonly Color TitleColor  = new Color(0.50f, 0.74f, 1f, 1f);
        public static readonly Color TextColor   = new Color(0.88f, 0.92f, 0.97f, 1f);
        public static readonly Color MutedText   = new Color(0.62f, 0.70f, 0.80f, 1f);

        public static readonly Color Success = new Color(0.30f, 0.85f, 0.50f);
        public static readonly Color Partial = new Color(1.00f, 0.74f, 0.28f);
        public static readonly Color Fail    = new Color(1.00f, 0.42f, 0.42f);

        private const int SpriteSize = 64;
        private const int Radius = 16;
        private static Sprite rounded;

        /// <summary>White rounded rectangle, 9-sliced (corner radius 16 units at multiplier 1).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "LabThemeRounded"
                };
                var pixels = new Color32[SpriteSize * SpriteSize];
                for (int y = 0; y < SpriteSize; y++)
                for (int x = 0; x < SpriteSize; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, Radius, SpriteSize - Radius);
                    float cy = Mathf.Clamp(y + 0.5f, Radius, SpriteSize - Radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(Radius - d + 0.5f) * 255f);
                    pixels[y * SpriteSize + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                rounded = Sprite.Create(tex, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f),
                                        100f, 0, SpriteMeshType.FullRect, new Vector4(Radius, Radius, Radius, Radius));
                rounded.name = "LabThemeRounded";
                return rounded;
            }
        }

        /// <summary>Rounded background; a larger multiplier gives smaller corners.</summary>
        public static void Round(Image image, Color color, float cornerScale = 1f)
        {
            if (image == null) return;
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = cornerScale;
            image.color = color;
        }

        /// <summary>Styles the LabV3 canvas: panels, panel titles, controls and the Mix button.</summary>
        public static void Apply(Transform root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                string n = image.gameObject.name;
                if (n.EndsWith("Panel"))
                {
                    Round(image, PanelColor, 1f);
                    var outline = image.GetComponent<Outline>();
                    if (outline != null) outline.enabled = false;
                }
            }

            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.gameObject.name == "PanelTitle")
                {
                    text.color = TitleColor;
                    text.fontSize = 13;
                    text.characterSpacing = 4;
                }
            }

            foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                Round(dropdown.targetGraphic as Image, ControlColor, 2.5f);
                if (dropdown.template != null)
                    Round(dropdown.template.GetComponent<Image>(), new Color(0.08f, 0.11f, 0.17f, 0.98f), 2f);
            }

            foreach (var field in root.GetComponentsInChildren<TMP_InputField>(true))
                Round(field.targetGraphic as Image, new Color(0.04f, 0.06f, 0.10f, 1f), 2.5f);

            foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
                Round(toggle.targetGraphic as Image, ControlColor, 3f);

            var mix = Object.FindObjectOfType<MixButtonView>(true);
            var mixButton = mix != null ? mix.GetComponentInChildren<Button>(true) : null;
            if (mixButton != null)
            {
                Round(mixButton.targetGraphic as Image, Accent, 1.5f);
                var colors = mixButton.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.85f);
                mixButton.colors = colors;
                var label = mixButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.fontSize = 22;
                    label.fontStyle = FontStyles.Bold;
                    label.characterSpacing = 2;
                }
            }
        }
    }
}
