// ChemLabSim v3 — Gas Color Palette
// Static lookup mapping common gaseous chemical formulas to physically
// representative tints used by the vapor/smoke VFX layer. Centralized so
// every controller that needs a "what color is this gas?" answer agrees.
//
// Conventions:
//   • Keys are case-insensitive chemical formulas (e.g. "NO2", "Cl2", "I2").
//   • RGB values reflect the real visible color of the pure gas; alpha is
//     left at 1.0 here and is re-tuned by the caller (e.g. forced to the
//     vapor-default alpha so it reads as gas, not liquid).
//   • The default fallback is a translucent off-white "steam".

using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Views
{
    /// <summary>
    /// Formula → color table for visible gaseous products. Lookup is
    /// case-insensitive. Returns <see cref="DefaultSteam"/> when unknown.
    /// </summary>
    public static class GasColorPalette
    {
        /// <summary>Translucent off-white — generic steam / unknown vapor.</summary>
        public static readonly Color DefaultSteam = new Color(0.92f, 0.92f, 0.92f, 0.6f);

        // RGB picked to match commonly cited visible colors for each gas.
        // Alpha kept at 1 — the caller usually overwrites it with the
        // vapor-default alpha to preserve translucency.
        private static readonly Dictionary<string, Color> Map =
            new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase)
            {
                // Reddish-brown — nitrogen dioxide
                { "NO2", new Color(0.65f, 0.20f, 0.10f, 1f) },
                // Pale green — chlorine
                { "Cl2", new Color(0.70f, 0.90f, 0.50f, 1f) },
                // Deep purple — iodine vapor
                { "I2",  new Color(0.45f, 0.20f, 0.55f, 1f) },
            };

        /// <summary>
        /// Resolve a vapor color for a chemical formula. Returns
        /// <paramref name="fallback"/> (or <see cref="DefaultSteam"/>) when
        /// the formula is null/empty/unknown.
        /// </summary>
        public static Color Resolve(string formula, Color? fallback = null)
        {
            Color fb = fallback ?? DefaultSteam;
            if (string.IsNullOrWhiteSpace(formula)) return fb;
            return Map.TryGetValue(formula.Trim(), out Color c) ? c : fb;
        }

        /// <summary>True if a specific tint exists for this formula.</summary>
        public static bool HasMapping(string formula)
        {
            return !string.IsNullOrWhiteSpace(formula) && Map.ContainsKey(formula.Trim());
        }
    }
}
