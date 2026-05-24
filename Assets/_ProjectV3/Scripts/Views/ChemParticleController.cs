// ChemLabSim v3 — Chem Particle Controller
// Configurable particle system factory and runtime controller.
// Creates and manages particle systems based on chemistry data:
//   - Gas bubbles: density ∝ gasMoles, size ∝ 1/pressure, speed ∝ reactionRate
//   - Smoke: opacity ∝ completion, rise speed ∝ rate
//   - Heat glow: color = exo/endo, intensity ∝ |ΔH|
//   - Precipitate: count ∝ solid product moles, settling speed ∝ gravity
//
// Drives 3D ParticleSystems (not UI). For UI-based effects, see ChemVesselView.

using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    public class ChemParticleController : MonoBehaviour
    {
        // ── Systems ────────────────────────────────────────────
        private ParticleSystem _gasBubbles;
        private ParticleSystem _smoke;
        private ParticleSystem _heatGlow;
        private ParticleSystem _precipitate;
        private ParticleSystem _sparks;

        // ── Cached modules ─────────────────────────────────────
        private ParticleSystem.EmissionModule _gasEmission;
        private ParticleSystem.MainModule _gasMain;
        private ParticleSystem.EmissionModule _smokeEmission;
        private ParticleSystem.MainModule _smokeMain;
        private ParticleSystem.EmissionModule _heatEmission;
        private ParticleSystem.MainModule _heatMain;
        private ParticleSystem.EmissionModule _precipEmission;
        private ParticleSystem.MainModule _precipMain;

        // ── State ──────────────────────────────────────────────
        private ChemFxState _state;
        private bool _active;
        private float _fadeTimer;
        private const float FadeDuration = 1.5f;

        // ── Material ───────────────────────────────────────────
        private Material _particleMat;

        private void Awake()
        {
            _particleMat = CreateParticleMaterial();
            CreateSystems();
        }

        private void OnEnable()  => EventBus.Subscribe<ChemFxTriggeredEvent>(OnChemFx);
        private void OnDisable() => EventBus.Unsubscribe<ChemFxTriggeredEvent>(OnChemFx);

        private void OnChemFx(ChemFxTriggeredEvent evt)
        {
            _state = evt.State;
            _active = _state.Found && !_state.IsFailure;
            _fadeTimer = 0f;
            ApplyState();
        }

        private void Update()
        {
            if (!_active) return;

            _fadeTimer += Time.deltaTime;

            // Auto-fade after reaction settles
            float effectDuration = Mathf.Lerp(2f, 6f, _state.ReactionRate);
            if (_fadeTimer > effectDuration)
            {
                float fade = 1f - Mathf.Clamp01((_fadeTimer - effectDuration) / FadeDuration);
                if (fade <= 0.01f)
                {
                    StopAll();
                    _active = false;
                    return;
                }

                // Reduce emission rates during fade
                if (_gasBubbles.isPlaying)
                {
                    _gasEmission.rateOverTimeMultiplier = CalculateGasRate() * fade;
                }
            }
        }

        // ════════════════════════════════════════════════════════
        //  APPLY CHEMISTRY STATE TO PARTICLES
        // ════════════════════════════════════════════════════════

        private void ApplyState()
        {
            StopAll();

            if (!_active) return;

            float rate = Mathf.Max(0.1f, _state.ReactionRate);

            // Gas bubbles
            if (_state.HasGas && _state.GasMolesProduced > 0.01f)
            {
                float gasRate = CalculateGasRate();
                _gasEmission.rateOverTime = gasRate;

                // Bubble size inversely proportional to pressure
                float sizeScale = 1f / Mathf.Max(0.3f, _state.PressureAtm);
                _gasMain.startSize = 0.08f * sizeScale;

                // Speed driven by reaction rate
                _gasMain.startSpeed = Mathf.Lerp(0.3f, 1.2f, rate);
                _gasMain.simulationSpeed = Mathf.Lerp(0.5f, 2f, rate);

                _gasBubbles.Play();
            }

            // Smoke
            if (_state.HasSmoke)
            {
                float smokeOpacity = Mathf.Lerp(0.15f, 0.55f, _state.CompletionPercent / 100f);
                var col = _smokeMain.startColor;
                col.color = new Color(0.48f, 0.48f, 0.50f, smokeOpacity);
                _smokeMain.startColor = col;
                _smokeMain.simulationSpeed = Mathf.Lerp(0.4f, 1.5f, rate);
                _smokeEmission.rateOverTime = Mathf.Lerp(5f, 20f, rate);
                _smoke.Play();
            }

            // Heat glow
            if (_state.HasHeat || _state.HasGlow)
            {
                float intensity = Mathf.Clamp01(Mathf.Abs(_state.EnthalpyKJ) / 150f);
                Color glowColor = _state.IsExothermic
                    ? new Color(1f, 0.5f, 0.12f, intensity)
                    : new Color(0.3f, 0.65f, 1f, intensity);

                var col = _heatMain.startColor;
                col.color = glowColor;
                _heatMain.startColor = col;
                _heatMain.startSize = Mathf.Lerp(0.3f, 0.8f, intensity);
                _heatMain.simulationSpeed = Mathf.Lerp(0.5f, 2f, rate);
                _heatEmission.rateOverTime = Mathf.Lerp(8f, 25f, intensity);
                _heatGlow.Play();
            }

            // Precipitate
            if (_state.HasPrecipitate)
            {
                float solidMoles = CalcSolidProductMoles();
                float precipRate = Mathf.Lerp(5f, 30f, Mathf.Clamp01(solidMoles));
                _precipEmission.rateOverTime = precipRate;
                _precipMain.simulationSpeed = Mathf.Lerp(0.6f, 1.2f, rate);
                _precipitate.Play();
            }

            // Sparks
            if (_state.HasSparks)
            {
                _sparks.Play();
            }
        }

        private float CalculateGasRate()
        {
            float molesNorm = Mathf.Clamp01(_state.GasMolesProduced / 2f);
            return Mathf.Lerp(8f, 50f, molesNorm) * Mathf.Max(0.2f, _state.ReactionRate);
        }

        private float CalcSolidProductMoles()
        {
            float total = 0f;
            if (_state.Substances == null) return 0f;
            for (int i = 0; i < _state.Substances.Count; i++)
            {
                var s = _state.Substances[i];
                if (s.IsProduct && s.Phase == Engine.Chemistry.Phase.Solid)
                    total += s.MolesFinal;
            }
            return total;
        }

        private void StopAll()
        {
            if (_gasBubbles.isPlaying)  _gasBubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_smoke.isPlaying)       _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_heatGlow.isPlaying)    _heatGlow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_precipitate.isPlaying) _precipitate.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_sparks.isPlaying)      _sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ════════════════════════════════════════════════════════
        //  PARTICLE SYSTEM FACTORY
        // ════════════════════════════════════════════════════════

        private void CreateSystems()
        {
            _gasBubbles  = CreatePS("_ChemGas",        localPos: new Vector3(0, -0.5f, 0));
            _smoke       = CreatePS("_ChemSmoke",      localPos: new Vector3(0, 0.2f, 0));
            _heatGlow    = CreatePS("_ChemHeatGlow",   localPos: new Vector3(0, -0.3f, 0));
            _precipitate = CreatePS("_ChemPrecipitate",localPos: new Vector3(0, -0.2f, 0));
            _sparks      = CreatePS("_ChemSparks",     localPos: new Vector3(0, 0f, 0));

            // Cache modules
            _gasEmission   = _gasBubbles.emission;
            _gasMain       = _gasBubbles.main;
            _smokeEmission = _smoke.emission;
            _smokeMain     = _smoke.main;
            _heatEmission  = _heatGlow.emission;
            _heatMain      = _heatGlow.main;
            _precipEmission = _precipitate.emission;
            _precipMain     = _precipitate.main;

            // Configure gas
            ConfigureGas(_gasBubbles);
            // Configure smoke
            ConfigureSmoke(_smoke);
            // Configure heat glow
            ConfigureHeatGlow(_heatGlow);
            // Configure precipitate
            ConfigurePrecipitate(_precipitate);
            // Configure sparks
            ConfigureSparks(_sparks);
        }

        private ParticleSystem CreatePS(string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = _particleMat;
            renderer.sortingOrder = 10;

            return ps;
        }

        private static void ConfigureGas(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = 2.5f;
            main.startSpeed = 0.5f;
            main.startSize = 0.08f;
            main.startColor = new Color(0.7f, 0.9f, 1f, 0.6f);
            main.gravityModifier = -0.15f;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.rateOverTime = 20f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.3f, 0.05f, 0.3f);
        }

        private static void ConfigureSmoke(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = 3.5f;
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startColor = new Color(0.48f, 0.48f, 0.50f, 0.35f);
            main.gravityModifier = -0.08f;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = 10f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.25f, 0.02f, 0.25f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.5f),
                    new Keyframe(0.5f, 1f),
                    new Keyframe(1f, 1.5f)));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.gray, 0f), new GradientColorKey(Color.gray, 1f) },
                new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0.15f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
        }

        private static void ConfigureHeatGlow(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = 2f;
            main.startSpeed = 0.1f;
            main.startSize = 0.4f;
            main.startColor = new Color(1f, 0.5f, 0.12f, 0.4f);
            main.gravityModifier = 0f;
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.rateOverTime = 15f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.6f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
        }

        private static void ConfigurePrecipitate(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = 3f;
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new Color(0.92f, 0.90f, 0.85f, 0.85f);
            main.gravityModifier = 0.12f;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 15f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.25f, 0.15f, 0.25f);
        }

        private static void ConfigureSparks(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new Color(1f, 0.85f, 0.3f, 0.95f);
            main.gravityModifier = 0.3f;
            main.maxParticles = 50;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var burst = new ParticleSystem.Burst(0f, 30, 50);
            emission.SetBursts(new[] { burst });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
        }

        private static Material CreateParticleMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            // Transparent additive
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
            if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 1f);   // Additive
            if (mat.HasProperty("_Cull"))    mat.SetFloat("_Cull", 0f);    // Off

            // Set white base
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

            return mat;
        }
    }
}
