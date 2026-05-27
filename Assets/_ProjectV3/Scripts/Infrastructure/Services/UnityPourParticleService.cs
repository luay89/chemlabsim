// ChemLabSim v3 — Unity Pour Particle Service
// Infrastructure implementation of IPourParticleService (declared in the
// Presentation layer). Drives a single Unity ParticleSystem to spray a
// liquid stream whose start colour matches the chemical currently pouring.
//
// Design notes:
//   • Implemented as a MonoBehaviour so it can host / instantiate the
//     ParticleSystem and follow the spout transform every frame.
//   • All emit / colour calls are null-guarded — a missing ParticleSystem
//     degrades to Debug.LogWarning, never to NullReferenceException.
//   • Honours the existing IPourParticleService contract used by
//     VesselDragPresenter (StartPourStream / StopPourStream) AND exposes the
//     position-based PlayPourEffect / StopPourEffect helpers requested by the
//     infrastructure spec, so both call styles work.

using ChemLabSimV3.Presentation.Presenters;
using UnityEngine;

namespace ChemLabSimV3.Infrastructure.Services
{
    [DisallowMultipleComponent]
    public class UnityPourParticleService : MonoBehaviour, IPourParticleService
    {
        [Header("Particle System")]
        [Tooltip("Liquid-stream particle system. If null and no prefab is set, " +
                 "the first ParticleSystem found in children is used.")]
        [SerializeField] private ParticleSystem pourParticles;

        [Tooltip("Optional prefab spawned at Awake when no live ParticleSystem is provided.")]
        [SerializeField] private ParticleSystem pourParticlesPrefab;

        [Header("Behaviour")]
        [Tooltip("When true the particle system follows the active spout transform every frame.")]
        [SerializeField] private bool followSpout = true;

        [Tooltip("Local-space offset applied on top of the spout transform.")]
        [SerializeField] private Vector3 spoutLocalOffset = Vector3.zero;

        // Runtime state
        private Transform _activeSpout;
        private bool _isPlaying;

        private void Awake()
        {
            if (pourParticles == null && pourParticlesPrefab != null)
            {
                pourParticles = Instantiate(pourParticlesPrefab, transform);
                pourParticles.name = pourParticlesPrefab.name + " (Runtime)";
            }

            if (pourParticles == null)
                pourParticles = GetComponentInChildren<ParticleSystem>(true);

            if (pourParticles == null)
            {
                Debug.LogWarning("[UnityPourParticleService] No ParticleSystem assigned, prefab missing, " +
                                 "and none found in children. Liquid-stream FX will be a no-op.", this);
                return;
            }

            // Make sure we start clean — a prefab might be authored as "Play On Awake".
            if (pourParticles.isPlaying)
                pourParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            if (!followSpout || !_isPlaying || pourParticles == null || _activeSpout == null) return;
            // Track the vessel's spout each frame so the stream stays glued to the nozzle
            // even while the user drags the vessel.
            pourParticles.transform.position = _activeSpout.TransformPoint(spoutLocalOffset);
            pourParticles.transform.rotation = _activeSpout.rotation;
        }

        private void OnDisable()
        {
            // Defensive cleanup so a disabled service never leaves particles in flight.
            InternalStop();
        }

        // ── IPourParticleService (contract used by VesselDragPresenter) ──────

        public void StartPourStream(Transform spout, Color liquidColor)
        {
            if (pourParticles == null)
            {
                Debug.LogWarning("[UnityPourParticleService] StartPourStream called but no ParticleSystem is available.", this);
                return;
            }

            _activeSpout = spout;

            if (spout != null)
            {
                pourParticles.transform.position = spout.TransformPoint(spoutLocalOffset);
                pourParticles.transform.rotation = spout.rotation;
            }

            ApplyStartColor(liquidColor);

            if (!pourParticles.isPlaying)
                pourParticles.Play(true);

            _isPlaying = true;
        }

        public void StopPourStream(Transform spout)
        {
            // The presenter passes the spout that originated the stream so a
            // multi-vessel system can disambiguate; if it doesn't match the
            // active spout we still stop, since this service drives one stream.
            InternalStop();
        }

        // ── Spec-style helpers (Vector3 + parameterless stop) ────────────────

        /// <summary>
        /// Position-based variant requested by the infrastructure spec.
        /// Moves the particle system to <paramref name="position"/>, tints the
        /// stream with <paramref name="chemicalColor"/>, and starts emitting.
        /// </summary>
        public void PlayPourEffect(Vector3 position, Color chemicalColor)
        {
            if (pourParticles == null)
            {
                Debug.LogWarning("[UnityPourParticleService] PlayPourEffect called but no ParticleSystem is available.", this);
                return;
            }

            _activeSpout = null; // explicit position takes precedence over follow
            pourParticles.transform.position = position;
            ApplyStartColor(chemicalColor);

            if (!pourParticles.isPlaying)
                pourParticles.Play(true);

            _isPlaying = true;
        }

        /// <summary>Parameterless stop variant requested by the infrastructure spec.</summary>
        public void StopPourEffect() => InternalStop();

        // ── Internals ────────────────────────────────────────────────────────

        private void ApplyStartColor(Color color)
        {
            if (pourParticles == null) return;
            // Cache the main module locally — assigning back is not required
            // because MainModule is a struct that mutates the underlying system.
            var main = pourParticles.main;
            main.startColor = color;
        }

        private void InternalStop()
        {
            if (pourParticles != null && pourParticles.isPlaying)
                pourParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            _isPlaying = false;
            _activeSpout = null;
        }
    }
}
