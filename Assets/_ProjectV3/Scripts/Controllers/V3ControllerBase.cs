// ChemLabSim v3 — Base Controller
// Optional base class for v3 controllers. Provides common lifecycle hooks.

using UnityEngine;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    /// <summary>
    /// Base MonoBehaviour for all v3 controllers.
    /// Subclasses override <see cref="OnInitialize"/> / <see cref="OnTeardown"/>
    /// and call <c>Subscribe/Unsubscribe</c> in those hooks.
    /// </summary>
    public abstract class V3ControllerBase : MonoBehaviour
    {
        /// <summary>Called by V3Bootstrap after all services are ready.</summary>
        public void Init()
        {
            OnInitialize();
        }

        /// <summary>Override to subscribe to EventBus and set up state.</summary>
        protected virtual void OnInitialize() { }

        /// <summary>Override to unsubscribe from EventBus and clean up.</summary>
        protected virtual void OnTeardown() { }

        protected virtual void OnDestroy()
        {
            OnTeardown();
        }
    }
}
