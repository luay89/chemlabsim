// ChemLabSim v3 — Vessel Container Contract
// Thin abstraction that lets the input/reaction layer drive any visual vessel
// (2D UI ReactionVesselView, 3D ConicalFlaskContainer, future presenters)
// without coupling controllers to a concrete implementation.

using UnityEngine;

namespace ChemLabSimV3.Views
{
    /// <summary>
    /// Coarse physical-state classification used by the idle-preview pipeline.
    /// Mirrors the strings stored in <c>materials.json</c> ("solid" / "liquid" / "gas").
    /// </summary>
    public enum PhysicalState
    {
        Unknown = 0,
        Solid   = 1,
        Liquid  = 2,
        Gas     = 3
    }

    /// <summary>
    /// Contract for any vessel view that wants to render the currently selected
    /// substance as an idle preview. Implementations decide whether to draw a
    /// settled powder mesh, a tinted fluid shader, a transparent gas, etc.
    /// </summary>
    public interface IVesselContainer
    {
        /// <summary>
        /// Show the substance currently sitting in the flask.
        /// </summary>
        /// <param name="physicalState">Solid / Liquid / Gas / Unknown.</param>
        /// <param name="substanceColor">RGBA tint pulled from materials.json
        /// (alpha 0 means "no colour info — use a sensible default").</param>
        void UpdateVesselVisuals(PhysicalState physicalState, Color substanceColor);
    }
}
