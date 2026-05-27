// ChemLabSim v3 — Vessel Drag Presenter
// Presentation-layer MonoBehaviour that turns pointer drag input into a smooth
// world-space vessel movement, then progressively tilts and pours the vessel's
// contents into the conical flask when it enters a registered TriggerZone.
//
// Architecture notes:
//   • Pure presentation concern. Business logic stays in the Application layer.
//   • The presenter resolves its dependencies through ServiceLocator at runtime
//     and degrades gracefully (Debug.LogWarning) when they are missing, so the
//     scene remains playable even before the pour use case / particle service
//     are registered by ProductionBootstrapper.
//   • The two minimal interfaces below are the seams that the Application and
//     Infrastructure layers will implement. They live in this file so the
//     presenter compiles with 0 errors today and can be moved into dedicated
//     contract files once the surrounding systems land.

using ChemLabSimV3.Core;
using ChemLabSimV3.Presentation.Adapters;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChemLabSimV3.Presentation.Presenters
{
    // ────────────────────────────────────────────────────────────────────────
    // Local presentation-layer contracts (seams for DI).
    // Replace / move these once the Application + Infrastructure layers expose
    // the real types. The presenter only ever talks to these abstractions.
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Application-layer contract: pours <paramref name="deltaSeconds"/> worth
    /// of a vessel's contents into a target container during a single frame.
    /// </summary>
    public interface ITimeBasedPourUseCase
    {
        /// <summary>Returns true while the source vessel still has fluid to pour.</summary>
        bool ExecutePour(string sourceVesselId, string targetContainerId, float deltaSeconds);
    }

    /// <summary>
    /// Infrastructure-layer contract: plays a directional liquid-stream VFX
    /// tinted to the given colour. Implementations typically wrap a pooled
    /// <see cref="ParticleSystem"/>.
    /// </summary>
    public interface IPourParticleService
    {
        void StartPourStream(Transform spout, Color liquidColor);
        void StopPourStream(Transform spout);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Presenter
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Drag → tilt → pour mechanic for a draggable 3D chemical vessel.
    /// Attach to a GameObject that has a Collider and a (non-kinematic-safe)
    /// physics-free Transform; pointer input is mapped to world space via the
    /// configured camera.
    /// </summary>
    [DisallowMultipleComponent]
    public class VesselDragPresenter : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public enum DragState
        {
            Idle = 0,
            Dragging = 1,
            Pouring = 2,
            Returning = 3
        }

        [Header("Identity")]
        [Tooltip("Stable id used by the Application layer to look up this vessel's contents.")]
        [SerializeField] private string vesselId = "vessel_unknown";

        [Tooltip("Hex colour (#RRGGBB or #RRGGBBAA) of the liquid this vessel pours. " +
                 "Overridden at runtime via SetLiquidColor / SetLiquidColorHex.")]
        [SerializeField] private string liquidColorHex = "#3DA9FCFF";

        [Header("Camera & Movement")]
        [Tooltip("Camera used to convert pointer screen position into world space. " +
                 "Falls back to Camera.main when null.")]
        [SerializeField] private Camera dragCamera;

        [Tooltip("How quickly the vessel chases the pointer (higher = snappier).")]
        [SerializeField, Min(0.01f)] private float followLerpSpeed = 18f;

        [Tooltip("How quickly the vessel returns to its original pose on invalid drop.")]
        [SerializeField, Min(0.01f)] private float returnLerpSpeed = 6f;

        [Tooltip("Squared distance considered close enough to snap back to the original pose.")]
        [SerializeField, Min(0.0001f)] private float returnSnapEpsilonSqr = 0.0004f;

        [Header("Pouring")]
        [Tooltip("Target tilt around the local X axis while pouring, in degrees (90–120 is typical).")]
        [SerializeField, Range(0f, 180f)] private float pourTiltDegrees = 105f;

        [Tooltip("Tilt easing speed (higher = faster tilt-in / tilt-out).")]
        [SerializeField, Min(0.01f)] private float tiltLerpSpeed = 8f;

        [Tooltip("Optional spout transform used as the VFX emission origin. " +
                 "Falls back to this transform when null.")]
        [SerializeField] private Transform pourSpout;

        // ── Runtime state ────────────────────────────────────────────────────
        private DragState _state = DragState.Idle;
        private Vector3 _originalLocalPosition;
        private Quaternion _originalLocalRotation;
        private Vector3 _pointerWorldOffset;
        private float _cameraDepth;
        private Color _liquidColor = Color.white;

        private TriggerZoneAdapter _activeZone;        // zone currently overlapped
        private bool _particleStreamActive;

        // Cached service references — resolved lazily so the presenter does not
        // care whether ServiceLocator was populated before or after Awake().
        private ITimeBasedPourUseCase _pourUseCase;
        private IPourParticleService _particleService;
        private bool _useCaseResolutionAttempted;
        private bool _particleResolutionAttempted;

        // ── Public surface (called by TriggerZoneAdapter) ────────────────────

        public string VesselId => vesselId;
        public DragState State => _state;
        public Color LiquidColor => _liquidColor;

        /// <summary>
        /// Override the serialized vessel id at runtime (used by scene
        /// bootstrappers that seed the use-case registry from a data list).
        /// Null / empty values are rejected to keep the id usable as a key.
        /// </summary>
        public void SetVesselId(string newVesselId)
        {
            if (string.IsNullOrWhiteSpace(newVesselId))
            {
                Debug.LogWarning($"[VesselDragPresenter:{vesselId}] Ignored attempt to assign an empty VesselId.", this);
                return;
            }
            vesselId = newVesselId;
        }

        /// <summary>Set the liquid colour directly (used by data binding).</summary>
        public void SetLiquidColor(Color color) => _liquidColor = color;

        /// <summary>Set the liquid colour from a hex string (#RRGGBB or #RRGGBBAA).</summary>
        public void SetLiquidColorHex(string hex)
        {
            if (TryParseHex(hex, out Color parsed))
                _liquidColor = parsed;
            else
                Debug.LogWarning($"[VesselDragPresenter:{vesselId}] Invalid liquid hex '{hex}'. Keeping previous colour.", this);
        }

        /// <summary>Invoked by <see cref="TriggerZoneAdapter"/> when this vessel enters the pour zone.</summary>
        internal void NotifyEnteredPourZone(TriggerZoneAdapter zone)
        {
            if (zone == null) return;
            _activeZone = zone;
            // Only flip to Pouring while the user is actively dragging — entering
            // the zone with an idle vessel must not start a phantom pour.
            if (_state == DragState.Dragging)
                _state = DragState.Pouring;
        }

        /// <summary>Invoked by <see cref="TriggerZoneAdapter"/> when this vessel leaves the pour zone.</summary>
        internal void NotifyExitedPourZone(TriggerZoneAdapter zone)
        {
            if (zone == null || _activeZone != zone) return;
            _activeZone = null;
            StopPourStreamIfNeeded();
            if (_state == DragState.Pouring)
                _state = DragState.Dragging;
        }

        // ── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _originalLocalPosition = transform.localPosition;
            _originalLocalRotation = transform.localRotation;

            if (dragCamera == null) dragCamera = Camera.main;
            if (dragCamera == null)
                Debug.LogWarning($"[VesselDragPresenter:{vesselId}] No drag camera assigned and Camera.main is null. Drag will be skipped.", this);

            if (pourSpout == null) pourSpout = transform;

            if (TryParseHex(liquidColorHex, out Color parsed))
                _liquidColor = parsed;
        }

        private void Update()
        {
            // Application-layer side effects only run while we are actively pouring.
            if (_state == DragState.Pouring && _activeZone != null)
            {
                EnsurePourStreamPlaying();
                TickPourUseCase(Time.deltaTime);
                ApplyTiltTowards(pourTiltDegrees);
            }
            else
            {
                ApplyTiltTowards(0f);
            }

            if (_state == DragState.Returning)
                TickReturnAnimation();
        }

        private void OnDisable()
        {
            // Defensive: never leave a particle stream playing if the presenter
            // is disabled mid-pour (scene unload, gameplay pause, etc.).
            StopPourStreamIfNeeded();
        }

        // ── EventSystem drag handlers ────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || dragCamera == null) return;

            _state = DragState.Dragging;
            _cameraDepth = Vector3.Distance(dragCamera.transform.position, transform.position);

            Vector3 pointerWorld = ScreenToWorld(eventData.position);
            _pointerWorldOffset = transform.position - pointerWorld;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null || dragCamera == null) return;
            if (_state != DragState.Dragging && _state != DragState.Pouring) return;

            Vector3 targetWorld = ScreenToWorld(eventData.position) + _pointerWorldOffset;
            transform.position = Vector3.Lerp(
                transform.position,
                targetWorld,
                1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            StopPourStreamIfNeeded();
            // Always begin returning — if the vessel is dropped on the flask the
            // pour use case has already moved the fluid; the visual vessel
            // itself snaps back to its rack so the user can grab it again.
            _state = DragState.Returning;
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        private void TickReturnAnimation()
        {
            float t = 1f - Mathf.Exp(-returnLerpSpeed * Time.deltaTime);
            transform.localPosition = Vector3.Lerp(transform.localPosition, _originalLocalPosition, t);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, _originalLocalRotation, t);

            if ((transform.localPosition - _originalLocalPosition).sqrMagnitude <= returnSnapEpsilonSqr)
            {
                transform.localPosition = _originalLocalPosition;
                transform.localRotation = _originalLocalRotation;
                _state = DragState.Idle;
            }
        }

        private void ApplyTiltTowards(float targetDegrees)
        {
            // Tilt is layered on top of the original rotation so designers can
            // freely orient the rest pose in the prefab.
            Quaternion target = _originalLocalRotation * Quaternion.Euler(targetDegrees, 0f, 0f);
            float t = 1f - Mathf.Exp(-tiltLerpSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, t);
        }

        private void TickPourUseCase(float deltaSeconds)
        {
            var useCase = ResolvePourUseCase();
            if (useCase == null) return; // warning already logged once

            string targetId = _activeZone != null ? _activeZone.ContainerId : null;
            if (string.IsNullOrEmpty(targetId))
            {
                Debug.LogWarning($"[VesselDragPresenter:{vesselId}] Pour zone has no ContainerId; skipping pour tick.", this);
                return;
            }

            bool stillHasFluid = useCase.ExecutePour(vesselId, targetId, deltaSeconds);
            if (!stillHasFluid)
            {
                // Vessel ran dry — stop pouring but stay attached to the cursor.
                _state = DragState.Dragging;
                StopPourStreamIfNeeded();
            }
        }

        private void EnsurePourStreamPlaying()
        {
            if (_particleStreamActive) return;
            var service = ResolveParticleService();
            if (service == null) return;
            service.StartPourStream(pourSpout != null ? pourSpout : transform, _liquidColor);
            _particleStreamActive = true;
        }

        private void StopPourStreamIfNeeded()
        {
            if (!_particleStreamActive) return;
            var service = ResolveParticleService();
            // Service might have been torn down before we got the chance to stop;
            // just clear the flag in that case.
            if (service != null)
                service.StopPourStream(pourSpout != null ? pourSpout : transform);
            _particleStreamActive = false;
        }

        private ITimeBasedPourUseCase ResolvePourUseCase()
        {
            if (_pourUseCase != null) return _pourUseCase;
            if (_useCaseResolutionAttempted) return null;
            _useCaseResolutionAttempted = true;

            _pourUseCase = ServiceLocator.Get<ITimeBasedPourUseCase>();
            if (_pourUseCase == null)
                Debug.LogWarning($"[VesselDragPresenter:{vesselId}] ITimeBasedPourUseCase not registered in ServiceLocator. Pour will be a visual no-op.", this);
            return _pourUseCase;
        }

        private IPourParticleService ResolveParticleService()
        {
            if (_particleService != null) return _particleService;
            if (_particleResolutionAttempted) return null;
            _particleResolutionAttempted = true;

            _particleService = ServiceLocator.Get<IPourParticleService>();
            if (_particleService == null)
                Debug.LogWarning($"[VesselDragPresenter:{vesselId}] IPourParticleService not registered in ServiceLocator. Liquid stream FX disabled.", this);
            return _particleService;
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            // Preserve depth so the vessel slides on the original camera-facing plane
            // instead of being shoved into / pulled out of the screen each frame.
            return dragCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, _cameraDepth));
        }

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            string normalized = hex.Trim().StartsWith("#") ? hex.Trim() : "#" + hex.Trim();
            return ColorUtility.TryParseHtmlString(normalized, out color);
        }
    }
}
