// ChemLabSim v3 — Molecular Lab Display
// Integrated molecular visualization panel for the lab scene.
// Creates a dedicated camera/render texture and displays animated molecules
// during reactions. Connects to the reaction event system.

using UnityEngine;
using System.Collections.Generic;

namespace ChemLabSimV3.Views
{
    /// <summary>
    /// Connects the molecular simulator to the lab UI.
    /// Attach to a GameObject in the LabV3 scene. Creates a render texture
    /// and displays molecular animations during reactions.
    /// </summary>
    public class MolecularLabDisplay : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private Vector2 renderTextureSize = new Vector2(512, 512);
        [SerializeField] private int renderTextureDepth = 24;
        [SerializeField] private LayerMask renderLayer = 0;

        [Header("Camera Settings")]
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 0f, -6f);
        [SerializeField] private float orthographicSize = 4f;
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);

        [Header("Auto-rotate")]
        [SerializeField] private bool autoRotate = true;
        [SerializeField] private float rotateSpeed = 20f;

        // ── Runtime ──
        private MolecularRendererView _renderer;
        private GameObject _rendererGO;
        private Camera _molCamera;
        private RenderTexture _rt;
        private bool _initialized;

        /// <summary>Reference to the internal MolecularRendererView.</summary>
        public MolecularRendererView RendererView => _renderer;

        /// <summary>The render texture showing molecular simulation.</summary>
        public RenderTexture RenderTexture => _rt;

        /// <summary>Display static reagent molecules.</summary>
        public void DisplayReagents(List<string> reagentFormulas)
        {
            if (reagentFormulas == null || reagentFormulas.Count == 0) return;
            _renderer?.DisplayReagents(reagentFormulas);
            if (_rendererGO != null)
                _rendererGO.SetActive(true);
        }

        /// <summary>Play a reaction transformation animation.</summary>
        public void DisplayReaction(
            List<string> reactantFormulas,
            List<string> productFormulas,
            float temperatureC)
        {
            _renderer?.DisplayReaction(reactantFormulas, productFormulas, temperatureC);
            if (_rendererGO != null)
                _rendererGO.SetActive(true);
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Update()
        {
            if (_initialized && autoRotate && _rendererGO != null)
            {
                _rendererGO.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
            }
        }

        /// <summary>
        /// Initialize the molecular display system.
        /// Creates render texture, camera, and molecular renderer.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            // Create render texture
            _rt = new RenderTexture(
                (int)renderTextureSize.x,
                (int)renderTextureSize.y,
                renderTextureDepth,
                RenderTextureFormat.ARGB32)
            {
                name = "MolecularSimRT",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _rt.Create();

            // Create molecular renderer
            _rendererGO = new GameObject("MolecularRenderer_Internal");
            _rendererGO.transform.SetParent(transform, false);
            _renderer = _rendererGO.AddComponent<MolecularRendererView>();
            // renderCamera is set via SerializeField in the scene or left null for auto-creation

            // Create dedicated camera
            var camGO = new GameObject("MolSimCamera");
            camGO.transform.SetParent(_rendererGO.transform, false);
            camGO.transform.localPosition = cameraPosition;
            camGO.transform.LookAt(Vector3.zero);

            _molCamera = camGO.AddComponent<Camera>();
            _molCamera.clearFlags = CameraClearFlags.SolidColor;
            _molCamera.backgroundColor = backgroundColor;
            _molCamera.orthographic = true;
            _molCamera.orthographicSize = orthographicSize;
            _molCamera.nearClipPlane = 0.1f;
            _molCamera.farClipPlane = 20f;
            _molCamera.targetTexture = _rt;
            _molCamera.enabled = true;
            _molCamera.depth = 0;

            if (renderLayer != 0)
                _molCamera.cullingMask = renderLayer;

            _initialized = true;
            Debug.Log("[MolecularLabDisplay] Initialized with RT: " + _rt.name);
        }

        /// <summary>
        /// Display molecules for given reagent formulas.
        /// </summary>
        public void ShowReagents(List<string> reagentFormulas)
        {
            if (!_initialized) Initialize();
            if (_renderer == null || reagentFormulas == null) return;

            DisplayReagents(reagentFormulas);
        }

        /// <summary>
        /// Start a reaction animation between given reagent formulas to produce products.
        /// </summary>
        public void ShowReactionAnimation(
            List<string> reactantFormulas,
            List<string> productFormulas,
            float temperatureC)
        {
            if (!_initialized) Initialize();
            if (_renderer == null) return;

            DisplayReaction(reactantFormulas, productFormulas, temperatureC);
        }

        /// <summary>
        /// Hide the molecular display.
        /// </summary>
        public void Hide()
        {
            if (_rendererGO != null)
                _rendererGO.SetActive(false);
        }

        private void Cleanup()
        {
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }

            if (_molCamera != null)
            {
                Destroy(_molCamera.gameObject);
                _molCamera = null;
            }

            if (_rendererGO != null)
            {
                Destroy(_rendererGO);
                _rendererGO = null;
            }

            _initialized = false;
        }
    }
}
