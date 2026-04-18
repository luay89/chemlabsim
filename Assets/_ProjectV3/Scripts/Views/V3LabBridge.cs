// ChemLabSim v3 — V3LabBridge [OBSOLETE]
// Superseded by LabInputController + real input views (Phase 7).
// Kept for quick Inspector-driven testing only.
// For production flow, use LabInputController instead.
//
// THIS IS A TEMPORARY DEVELOPMENT TOOL — not part of the final UI.

using System;
using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    [Obsolete("Use LabInputController with real input views instead. Kept for quick Inspector testing only.")]
    public class V3LabBridge : MonoBehaviour
    {
        [Header("Controller Reference (auto-discovered if null)")]
        [SerializeField] private ReactionController reactionController;

        [Header("Test Mix Parameters")]
        [SerializeField] private string reagent1 = "HCl";
        [SerializeField] private string reagent2 = "NaOH";
        [SerializeField] private string reagent3 = "";
        [SerializeField] private float temperature = 25f;
        [SerializeField] private int medium = 0;          // 0=Neutral, 1=Acidic, 2=Basic
        [SerializeField] private bool stirring = true;
        [SerializeField] private bool grinding = false;
        [SerializeField] private bool hasCatalyst = false;

        private void Start()
        {
            if (reactionController == null)
                reactionController = FindObjectOfType<ReactionController>();

            if (reactionController == null)
                Debug.LogWarning("[V3LabBridge] ReactionController not found in scene.");
            else
                Debug.Log("[V3LabBridge] Ready. Click 'Test Mix' in Game view or Inspector.");
        }

        /// <summary>
        /// Trigger a test reaction. Can be called from a UI Button's OnClick,
        /// from the Inspector context menu, or via the OnGUI button.
        /// </summary>
        [ContextMenu("Test Mix")]
        public void TestMix()
        {
            if (reactionController == null)
            {
                reactionController = FindObjectOfType<ReactionController>();
                if (reactionController == null)
                {
                    Debug.LogError("[V3LabBridge] Cannot mix — ReactionController not found.");
                    return;
                }
            }

            var reagents = new List<string>();
            if (!string.IsNullOrWhiteSpace(reagent1)) reagents.Add(reagent1.Trim());
            if (!string.IsNullOrWhiteSpace(reagent2)) reagents.Add(reagent2.Trim());
            if (!string.IsNullOrWhiteSpace(reagent3)) reagents.Add(reagent3.Trim());

            ReactionMedium safeMedium = (medium >= 0 && medium <= 2)
                ? (ReactionMedium)medium
                : ReactionMedium.Neutral;

            var request = new MixRequest
            {
                ReagentNames = reagents,
                Temperature = temperature,
                Medium = safeMedium,
                Stirring = stirring ? 1f : 0f,
                Grinding = grinding ? 1f : 0f,
                HasCatalyst = hasCatalyst
            };

            Debug.Log($"[V3LabBridge] Requesting mix: {string.Join(" + ", reagents)} | T={temperature}°C | Med={safeMedium}");
            reactionController.RequestMix(request);
        }

        // -- Simple on-screen button for quick testing ---------
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 220, 60));
            if (GUILayout.Button("Test Mix (V3)", GUILayout.Height(50)))
            {
                TestMix();
            }
            GUILayout.EndArea();
        }
    }
}
