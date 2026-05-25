// ChemLabSim v3 — Conical Flask Container (placeholder)
// Stub MonoBehaviour for the upcoming 3D conical-flask visual. Wire this into
// LabInputController's "Vessel Integration" slot so the idle material preview
// can drive the flask's contents (powder mesh / fluid shader) at runtime.
//
// NOTE: Final rendering hooks land alongside the 3D vessel prefab and the
// ChemLiquid shader (see ContainerFillController). This stub deliberately
// keeps zero behavioural coupling so it can be dropped into any scene.

using UnityEngine;

namespace ChemLabSimV3.Views
{
    [DisallowMultipleComponent]
    public class ConicalFlaskContainer : MonoBehaviour, IVesselContainer
    {
        [Header("Idle preview targets (assign once the 3D flask prefab exists)")]
        [Tooltip("Renderer that draws the fluid mesh + ChemLiquid shader.")]
        [SerializeField] private Renderer liquidRenderer;
        [Tooltip("Renderer for the settled compound / powder pile at the bottom.")]
        [SerializeField] private Renderer powderRenderer;

        [Header("Placeholder fallback")]
        [Tooltip("When true and a renderer slot is empty, a primitive Cylinder/Cube is spawned at runtime so the colour-swap pipeline can be verified without the final 3D prefab.")]
        [SerializeField] private bool autoSpawnPlaceholders = true;

        private const string PlaceholderLiquidName = "[Placeholder] Liquid";
        private const string PlaceholderPowderName = "[Placeholder] Powder";

        // Shader property IDs — cached once at type load so the runtime path is allocation-free.
        private static readonly int PropLiquidColor = Shader.PropertyToID("_LiquidColor");
        private static readonly int PropSolidColor  = Shader.PropertyToID("_SolidColor");
        private static readonly int PropColor       = Shader.PropertyToID("_Color");
        private static readonly int PropBaseColor   = Shader.PropertyToID("_BaseColor"); // URP/HDRP Lit fallback

        // Cached per-instance MaterialPropertyBlock — using a block (not material.color)
        // means we never instantiate a runtime material copy, so there is nothing to leak.
        private MaterialPropertyBlock _mpb;

        public PhysicalState CurrentState { get; private set; } = PhysicalState.Unknown;
        public Color CurrentColor { get; private set; } = Color.clear;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            // Runtime safety net: if the scene was saved with empty slots,
            // spawn primitives so UpdateVesselVisuals can never hit a null ref.
            if (autoSpawnPlaceholders) EnsurePlaceholderRenderers();
        }

        /// <summary>
        /// Called by Unity when the component is added in the editor or
        /// when "Reset" is chosen from the component context menu. Seeds
        /// the two placeholder primitives so designers see something
        /// immediately without hand-building the hierarchy.
        /// </summary>
        private void Reset()
        {
            autoSpawnPlaceholders = true;
            EnsurePlaceholderRenderers();
        }

        /// <inheritdoc />
        public void UpdateVesselVisuals(PhysicalState physicalState, Color substanceColor)
        {
            CurrentState = physicalState;
            CurrentColor = substanceColor;

            // Defensive: Awake() may not have run yet if UpdateVesselVisuals is
            // called from another component's Awake on the same frame.
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            switch (physicalState)
            {
                case PhysicalState.Liquid:
                    SetRendererActive(liquidRenderer, true);
                    SetRendererActive(powderRenderer, false);
                    if (substanceColor.a > 0f)
                        ApplyTint(liquidRenderer, PropLiquidColor, substanceColor);
                    break;

                case PhysicalState.Solid:
                    SetRendererActive(powderRenderer, true);
                    SetRendererActive(liquidRenderer, false);
                    if (substanceColor.a > 0f)
                        ApplyTint(powderRenderer, PropSolidColor, substanceColor);
                    break;

                case PhysicalState.Gas:
                case PhysicalState.Unknown:
                default:
                    // Gas has no in-flask body; the FX layer handles steam/vapour.
                    SetRendererActive(liquidRenderer, false);
                    SetRendererActive(powderRenderer, false);
                    break;
            }
        }

        private static void SetRendererActive(Renderer r, bool active)
        {
            if (r == null) return;
            var go = r.gameObject;
            if (go.activeSelf != active) go.SetActive(active);
        }

        private void ApplyTint(Renderer r, int primaryPropId, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(_mpb);
            // Write the primary chem-shader slot AND the standard fallbacks so
            // the same component works whether the prefab uses ChemLiquid,
            // URP Lit, or Built-in Standard.
            _mpb.SetColor(primaryPropId, c);
            _mpb.SetColor(PropColor, c);
            _mpb.SetColor(PropBaseColor, c);
            r.SetPropertyBlock(_mpb);
        }

        // ── Placeholder hierarchy bootstrap ────────────────────────────────────

        /// <summary>
        /// Resolves the renderer slots, reusing existing placeholder children
        /// if they're present and creating fresh Cylinder/Cube primitives
        /// otherwise. Safe to call from both Reset() (editor) and Awake()
        /// (runtime) — destroy semantics adapt to the play state.
        /// </summary>
        private void EnsurePlaceholderRenderers()
        {
            if (liquidRenderer == null)
                liquidRenderer = ResolvePlaceholder(
                    PlaceholderLiquidName,
                    PrimitiveType.Cylinder,
                    localPos:   new Vector3(0f, 0.25f, 0f),
                    localScale: new Vector3(0.45f, 0.50f, 0.45f));

            if (powderRenderer == null)
                powderRenderer = ResolvePlaceholder(
                    PlaceholderPowderName,
                    PrimitiveType.Cube,
                    localPos:   new Vector3(0f, 0.10f, 0f),
                    localScale: new Vector3(0.55f, 0.18f, 0.55f));
        }

        private Renderer ResolvePlaceholder(string childName, PrimitiveType primitive, Vector3 localPos, Vector3 localScale)
        {
            // Reuse: if a previous run/edit already spawned this child, just grab its renderer.
            var existing = transform.Find(childName);
            if (existing != null)
            {
                var existingRenderer = existing.GetComponent<Renderer>();
                if (existingRenderer != null) return existingRenderer;
            }

            var go = GameObject.CreatePrimitive(primitive);
            go.name = childName;
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = localScale;

            // Primitives ship with a Collider — strip it; this is a visual preview only.
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            return go.GetComponent<Renderer>();
        }
    }
}
