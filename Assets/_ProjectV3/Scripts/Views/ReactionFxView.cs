// ChemLabSim v3 — Reaction FX View
// View-layer bridge: subscribes to FxTriggeredEvent, owns ParticleSystems.
// Creates 6 particle systems programmatically on Awake (matching v2 specs).
// No decision logic — just play/stop what FXController tells it to.

using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    public class ReactionFxView : MonoBehaviour
    {
        // -- Particle Systems (created at runtime) -------------
        private ParticleSystem gasFx;
        private ParticleSystem successFx;
        private ParticleSystem failFx;
        private ParticleSystem catalystFx;
        private ParticleSystem heatFx;
        private ParticleSystem precipitateFx;
        private ParticleSystem colorChangeFx;
        private ParticleSystem glowFx;
        private ParticleSystem sparksFx;
        private ParticleSystem smokeFx;
        private ParticleSystem foamFx;
        private ParticleSystem frostFx;

        private Material particleMaterial;

        // -- Unity Lifecycle -----------------------------------

        private void Awake()
        {
            EnsureFxSetup();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<FxTriggeredEvent>(OnFxTriggered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<FxTriggeredEvent>(OnFxTriggered);
        }

        // -- Event Handler -------------------------------------

        private void OnFxTriggered(FxTriggeredEvent evt)
        {
            Render(evt.State);
        }

        public void Render(FxState state)
        {
            if (state.StopAll)
                StopAllFx();

            if (state.PlaySuccess && successFx != null) successFx.Play();
            if (state.PlayFail && failFx != null) failFx.Play();
            if (state.PlayGas && gasFx != null) gasFx.Play();
            if (state.PlayCatalyst && catalystFx != null) catalystFx.Play();

            // -- Heat with temperature-delta-aware intensity & color --
            if (state.PlayHeat && heatFx != null)
            {
                float absDelta = Mathf.Abs(state.TemperatureDelta);
                // Scale burst count and speed by magnitude (clamp 1x–3x)
                float intensityMul = Mathf.Clamp(absDelta / 20f, 1f, 3f);

                var main = heatFx.main;
                main.startSpeed = 0.65f * intensityMul;
                main.startSize = 0.16f * Mathf.Clamp(intensityMul, 1f, 2f);

                var emission = heatFx.emission;
                emission.SetBursts(new[] {
                    new ParticleSystem.Burst(0f, (short)Mathf.Clamp(20 * intensityMul, 20, 60))
                });

                if (state.TemperatureDelta < 0f)
                {
                    // Endothermic: blue/cyan gradient
                    main.startColor = new Color(0.45f, 0.75f, 1f, 0.7f);
                    SetGradient(heatFx,
                        new Color(0.45f, 0.75f, 1f), new Color(0.70f, 0.92f, 1f));
                }
                else
                {
                    // Exothermic: orange/red gradient (stronger orange at higher delta)
                    float redShift = Mathf.Clamp01(absDelta / 80f);
                    Color startC = Color.Lerp(
                        new Color(1f, 0.65f, 0.22f), new Color(1f, 0.45f, 0.10f), redShift);
                    Color endC = Color.Lerp(
                        new Color(1f, 0.18f, 0.08f), new Color(1f, 0.08f, 0.02f), redShift);
                    main.startColor = new Color(startC.r, startC.g, startC.b, 0.7f);
                    SetGradient(heatFx, startC, endC);
                }
                heatFx.Play();
            }

            if (state.PlayPrecipitate && precipitateFx != null) precipitateFx.Play();

            if (state.PlayColorChange && colorChangeFx != null)
            {
                if (!string.IsNullOrEmpty(state.ColorChangeHex) &&
                    ColorUtility.TryParseHtmlString(state.ColorChangeHex, out Color parsed))
                {
                    var main = colorChangeFx.main;
                    main.startColor = new Color(parsed.r, parsed.g, parsed.b, 0.85f);
                }
                colorChangeFx.Play();
            }

            // -- Extended effects (glow, sparks, smoke, foam, frost) --
            if (state.PlayGlow && glowFx != null) glowFx.Play();
            if (state.PlaySparks && sparksFx != null) sparksFx.Play();
            if (state.PlaySmoke && smokeFx != null) smokeFx.Play();
            if (state.PlayFoam && foamFx != null) foamFx.Play();
            if (state.PlayFrost && frostFx != null) frostFx.Play();
        }

        // -- Gradient Helper -----------------------------------

        private static void SetGradient(ParticleSystem ps, Color start, Color end)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[] {
                    new GradientAlphaKey(0.7f, 0f),
                    new GradientAlphaKey(0.35f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        // -- Stop ----------------------------------------------

        private void StopAllFx()
        {
            StopIfPlaying(gasFx);
            StopIfPlaying(successFx);
            StopIfPlaying(failFx);
            StopIfPlaying(catalystFx);
            StopIfPlaying(heatFx);
            StopIfPlaying(precipitateFx);
            StopIfPlaying(colorChangeFx);
            StopIfPlaying(glowFx);
            StopIfPlaying(sparksFx);
            StopIfPlaying(smokeFx);
            StopIfPlaying(foamFx);
            StopIfPlaying(frostFx);
        }

        private static void StopIfPlaying(ParticleSystem ps)
        {
            if (ps != null && ps.isPlaying) ps.Stop();
        }

        // -- Setup (from v2 EnsureReactionFxSetup + CreateParticleFx) --

        private void EnsureFxSetup()
        {
            Transform fxParent = Camera.main != null ? Camera.main.transform : transform;

            gasFx ??= CreateParticleFx(fxParent, "_V3_GasFx",
                new Vector3(0f, -0.55f, 4f), false,
                new Color(0.85f, 0.95f, 1f, 0.65f), new Color(0.80f, 0.92f, 1f, 0f),
                2.8f, 0.55f, 0.18f, -0.02f, 0f, new Vector3(0.35f, 0.12f, 0.2f), 24, 0.22f);

            successFx ??= CreateParticleFx(fxParent, "_V3_SuccessFx",
                new Vector3(0f, -0.2f, 4f), false,
                new Color(0.30f, 1f, 0.66f, 0.95f), new Color(0.95f, 1f, 0.95f, 0f),
                1.1f, 1.35f, 0.12f, 0f, 0f, new Vector3(0.22f, 0.08f, 0.18f), 20, 0.12f);

            failFx ??= CreateParticleFx(fxParent, "_V3_FailFx",
                new Vector3(0f, -0.25f, 4f), false,
                new Color(1f, 0.35f, 0.35f, 0.7f), new Color(0.42f, 0.08f, 0.08f, 0f),
                1.7f, 0.85f, 0.15f, 0f, 0f, new Vector3(0.28f, 0.08f, 0.18f), 16, 0.18f);

            catalystFx ??= CreateParticleFx(fxParent, "_V3_CatalystFx",
                new Vector3(0f, -0.4f, 4f), false,
                new Color(0.45f, 0.95f, 1f, 0.9f), new Color(0.35f, 0.70f, 1f, 0f),
                1.45f, 0.95f, 0.10f, 0f, 0f, new Vector3(0.18f, 0.18f, 0.18f), 18, 0.3f);

            heatFx ??= CreateParticleFx(fxParent, "_V3_HeatFx",
                new Vector3(0f, -0.45f, 4f), false,
                new Color(1f, 0.65f, 0.22f, 0.7f), new Color(1f, 0.18f, 0.08f, 0f),
                1.6f, 0.65f, 0.16f, -0.03f, 0f, new Vector3(0.30f, 0.10f, 0.20f), 20, 0.2f);

            precipitateFx ??= CreateParticleFx(fxParent, "_V3_PrecipitateFx",
                new Vector3(0f, -0.62f, 4f), false,
                new Color(0.96f, 0.96f, 0.90f, 0.92f), new Color(0.82f, 0.82f, 0.76f, 0.12f),
                2.4f, 0.14f, 0.12f, 0.06f, 0f, new Vector3(0.42f, 0.08f, 0.18f), 28, 0.08f);

            colorChangeFx ??= CreateParticleFx(fxParent, "_V3_ColorChangeFx",
                new Vector3(0f, -0.35f, 4f), false,
                new Color(0.5f, 0.8f, 1f, 0.75f), new Color(0.5f, 0.8f, 1f, 0f),
                1.8f, 0.40f, 0.20f, 0f, 0f, new Vector3(0.50f, 0.30f, 0.20f), 30, 0.15f);

            // -- Extended particle systems (glow, sparks, smoke, foam, frost) --

            // Glow: soft warm emission, slow, large, low alpha — ambient highlight
            glowFx ??= CreateParticleFx(fxParent, "_V3_GlowFx",
                new Vector3(0f, -0.35f, 4f), false,
                new Color(1f, 0.85f, 0.5f, 0.45f), new Color(1f, 0.90f, 0.6f, 0f),
                2.2f, 0.12f, 0.45f, 0f, 0f, new Vector3(0.55f, 0.35f, 0.25f), 16, 0.05f);

            // Sparks: short energetic burst, fast, small, high gravity
            sparksFx ??= CreateParticleFx(fxParent, "_V3_SparksFx",
                new Vector3(0f, -0.30f, 4f), false,
                new Color(1f, 0.90f, 0.35f, 0.95f), new Color(1f, 0.45f, 0.10f, 0f),
                0.6f, 2.8f, 0.06f, 0.15f, 0f, new Vector3(0.20f, 0.06f, 0.15f), 40, 0.35f);

            // Smoke: soft upward dark/gray, slow, large, rising
            smokeFx ??= CreateParticleFx(fxParent, "_V3_SmokeFx",
                new Vector3(0f, -0.45f, 4f), false,
                new Color(0.30f, 0.30f, 0.32f, 0.55f), new Color(0.45f, 0.45f, 0.48f, 0f),
                3.2f, 0.25f, 0.30f, -0.04f, 0f, new Vector3(0.40f, 0.15f, 0.25f), 20, 0.18f);

            // Foam: dense white bubbly, medium speed, clustered
            foamFx ??= CreateParticleFx(fxParent, "_V3_FoamFx",
                new Vector3(0f, -0.50f, 4f), false,
                new Color(0.98f, 0.98f, 1f, 0.90f), new Color(0.92f, 0.95f, 1f, 0.10f),
                1.8f, 0.55f, 0.10f, -0.01f, 0f, new Vector3(0.30f, 0.20f, 0.20f), 45, 0.25f);

            // Frost: blue/white cold mist, slow expanding, crystalline feel
            frostFx ??= CreateParticleFx(fxParent, "_V3_FrostFx",
                new Vector3(0f, -0.40f, 4f), false,
                new Color(0.65f, 0.85f, 1f, 0.70f), new Color(0.85f, 0.95f, 1f, 0f),
                2.6f, 0.18f, 0.25f, 0.01f, 0f, new Vector3(0.48f, 0.25f, 0.22f), 22, 0.10f);
        }

        private ParticleSystem CreateParticleFx(
            Transform parent, string fxName, Vector3 localPos, bool loop,
            Color startColor, Color endColor,
            float lifetime, float speed, float size, float gravity,
            float emissionRate, Vector3 shapeScale, short burstCount, float noiseStrength)
        {
            var go = new GameObject(fxName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = loop;
            main.duration = Mathf.Max(1.2f, lifetime);
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = startColor;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = loop ? 256 : 64;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = emissionRate;
            if (burstCount > 0)
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = shapeScale;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(endColor, 1f)
                },
                new[] {
                    new GradientAlphaKey(startColor.a, 0f),
                    new GradientAlphaKey(Mathf.Max(startColor.a * 0.5f, endColor.a), 0.55f),
                    new GradientAlphaKey(endColor.a, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 1.35f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var noise = ps.noise;
            noise.enabled = noiseStrength > 0f;
            noise.strength = noiseStrength;
            noise.frequency = 0.45f;
            noise.scrollSpeed = 0.12f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;

            Material mat = GetOrCreateParticleMaterial();
            if (mat != null)
                renderer.sharedMaterial = mat;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private Material GetOrCreateParticleMaterial()
        {
            if (particleMaterial != null) return particleMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Transparent");

            if (shader == null)
            {
                Debug.LogWarning("[ReactionFxView] No compatible particle shader found.");
                return null;
            }

            particleMaterial = new Material(shader) { name = "_V3_ParticleFxMaterial" };
            particleMaterial.enableInstancing = true;

            if (particleMaterial.HasProperty("_BaseMap"))
                particleMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (particleMaterial.HasProperty("_MainTex"))
                particleMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
            if (particleMaterial.HasProperty("_BaseColor"))
                particleMaterial.SetColor("_BaseColor", Color.white);
            if (particleMaterial.HasProperty("_Color"))
                particleMaterial.SetColor("_Color", Color.white);
            if (particleMaterial.HasProperty("_Surface"))
                particleMaterial.SetFloat("_Surface", 1f);
            if (particleMaterial.HasProperty("_Blend"))
                particleMaterial.SetFloat("_Blend", 0f);
            if (particleMaterial.HasProperty("_Cull"))
                particleMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            if (particleMaterial.HasProperty("_ZWrite"))
                particleMaterial.SetFloat("_ZWrite", 0f);

            return particleMaterial;
        }
    }
}
