// ChemLabSim v3 — Trigger Zone Adapter
// Bridges Unity's physics trigger callbacks to the Presentation layer.
// Place on a 3D trigger collider parented to the conical flask's mouth.
// When a draggable vessel enters the zone, the matching VesselDragPresenter
// is notified so it can transition into the "pouring" state.

using ChemLabSimV3.Presentation.Presenters;
using UnityEngine;

namespace ChemLabSimV3.Presentation.Adapters
{
    /// <summary>
    /// Lightweight trigger adapter that detects draggable vessels entering /
    /// leaving the pour zone at the mouth of a container (e.g. a conical flask).
    /// Vessels are validated either by tag or by carrying a
    /// <see cref="VesselDragPresenter"/> component anywhere in their hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class TriggerZoneAdapter : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Stable id of the container this zone fronts (passed to the pour use case).")]
        [SerializeField] private string containerId = "container_conical_flask";

        [Header("Validation")]
        [Tooltip("Optional tag filter. Leave empty to accept any object whose hierarchy " +
                 "carries a VesselDragPresenter component.")]
        [SerializeField] private string requiredTag = "DraggableVessel";

        [Tooltip("When true, also search parent transforms for a VesselDragPresenter. " +
                 "Useful when the collider sits on a child mesh of the vessel prefab.")]
        [SerializeField] private bool searchHierarchy = true;

        /// <summary>Container identifier consumed by the pour use case.</summary>
        public string ContainerId => containerId;

        private void Reset()
        {
            // Ensure the attached collider is in trigger mode the first time
            // the component is added — saves a common setup foot-gun.
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger) col.isTrigger = true;
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                Debug.LogWarning($"[TriggerZoneAdapter:{containerId}] No Collider found on this GameObject.", this);
                return;
            }
            if (!col.isTrigger)
                Debug.LogWarning($"[TriggerZoneAdapter:{containerId}] Collider is not set to 'isTrigger'. " +
                                 "Pour detection will not fire.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!UnityEngine.Application.isPlaying) return; // ignore editor-time prefab edits
            var presenter = ResolvePresenter(other);
            if (presenter == null) return;
            presenter.NotifyEnteredPourZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!UnityEngine.Application.isPlaying) return;
            var presenter = ResolvePresenter(other);
            if (presenter == null) return;
            presenter.NotifyExitedPourZone(this);
        }

        /// <summary>
        /// Returns the <see cref="VesselDragPresenter"/> that owns
        /// <paramref name="other"/>, or null if the collider does not belong
        /// to a registered draggable vessel.
        /// </summary>
        private VesselDragPresenter ResolvePresenter(Collider other)
        {
            if (other == null) return null;

            // Cheap tag rejection first, before the component lookup.
            if (!string.IsNullOrEmpty(requiredTag))
            {
                // CompareTag throws if the tag is undeclared in the TagManager —
                // wrap defensively so a missing tag never breaks gameplay.
                try
                {
                    if (!other.CompareTag(requiredTag)) return null;
                }
                catch (UnityException)
                {
                    Debug.LogWarning($"[TriggerZoneAdapter:{containerId}] Required tag '{requiredTag}' " +
                                     "is not defined in the TagManager. Skipping tag check.", this);
                }
            }

            return searchHierarchy
                ? other.GetComponentInParent<VesselDragPresenter>()
                : other.GetComponent<VesselDragPresenter>();
        }
    }
}
