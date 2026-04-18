// ChemLabSim v3 — V3ViewBase
// Abstract base for all v3 UI views. Views are passive display components:
// they receive data from UIController and render it. No logic, no events, no state.

using UnityEngine;

namespace ChemLabSimV3.Views
{
    /// <summary>
    /// Base class for v3 view components.
    /// Views update their UI elements when <see cref="Refresh"/> is called by UIController.
    /// </summary>
    public abstract class V3ViewBase : MonoBehaviour
    {
        /// <summary>Show the view's root GameObject.</summary>
        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>Hide the view's root GameObject.</summary>
        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>True if the view's root is active.</summary>
        public bool IsVisible => gameObject.activeSelf;
    }
}
