// ChemLabSim v3 — MixButtonView
// Thin wrapper around Unity Button for the "Mix" action.
// No logic — just forwards click via OnMixClicked event.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class MixButtonView : V3ViewBase
    {
        [Header("UI")]
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI buttonLabel;

        /// <summary>Fires when the user clicks the Mix button.</summary>
        public event Action OnMixClicked;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(HandleClick);

            if (buttonLabel != null && string.IsNullOrEmpty(buttonLabel.text))
                buttonLabel.text = V3Labels.Get("mix");
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        /// <summary>Update the button label text (used on language change).</summary>
        public void SetLabel(string text)
        {
            if (buttonLabel != null)
                buttonLabel.text = text;
        }

        /// <summary>Enable or disable the mix button.</summary>
        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        private void HandleClick()
        {
            OnMixClicked?.Invoke();
        }
    }
}
