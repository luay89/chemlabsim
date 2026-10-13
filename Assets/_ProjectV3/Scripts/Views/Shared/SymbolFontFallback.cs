// ChemLabSim v3 — symbol font fallback
// LiberationSans (the TMP default) has no ✓ ✗ ⚠ ▶ ▸ ● ■ and no chemistry super/subscripts
// (H⁺, H₂O), so TMP draws □ instead. This registers DejaVu Sans (Resources/Fonts) as a
// global dynamic TMP fallback before the first scene loads. Glyphs are rasterized on demand.

using TMPro;
using UnityEngine;

namespace ChemLabSimV3.Views
{
    public static class SymbolFontFallback
    {
        private const string FontResource = "Fonts/DejaVuSans";

        private static TMP_FontAsset fallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (fallback != null || TMP_Settings.instance == null) return;

            var font = Resources.Load<Font>(FontResource);
            if (font == null)
            {
                Debug.LogWarning($"[SymbolFontFallback] Resources/{FontResource} not found; symbols may render as boxes.");
                return;
            }

            fallback = TMP_FontAsset.CreateFontAsset(font);
            if (fallback == null)
            {
                Debug.LogWarning("[SymbolFontFallback] Could not create a TMP font asset from DejaVu Sans.");
                return;
            }
            fallback.name = "DejaVuSans SDF (runtime fallback)";

            var list = TMP_Settings.fallbackFontAssets;
            if (list != null && !list.Contains(fallback))
                list.Add(fallback);
        }
    }
}
