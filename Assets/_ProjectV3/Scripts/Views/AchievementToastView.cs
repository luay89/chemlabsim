// ChemLabSim v3 — AchievementToastView
// Shows a brief toast notification when an achievement is unlocked.
// Pure display — no unlock logic. UIController pushes data here.
// Auto-hides after a configurable duration.

using System.Collections;
using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class AchievementToastView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("Settings")]
        [SerializeField] private float displayDuration = 3f;

        private Coroutine hideCoroutine;

        /// <summary>Show the toast with achievement info, then auto-hide.</summary>
        public void ShowToast(string displayName)
        {
            Show();

            if (titleText != null)
                titleText.text = V3Labels.Get("achievementUnlocked");

            if (nameText != null)
                nameText.text = displayName ?? string.Empty;

            // Restart auto-hide timer
            if (hideCoroutine != null)
                StopCoroutine(hideCoroutine);
            hideCoroutine = StartCoroutine(AutoHide());
        }

        private IEnumerator AutoHide()
        {
            yield return new WaitForSeconds(displayDuration);
            Hide();
            hideCoroutine = null;
        }

        /// <summary>Reset view.</summary>
        public void Clear()
        {
            if (titleText != null) titleText.text = string.Empty;
            if (nameText != null) nameText.text = string.Empty;
            Hide();
        }
    }
}
