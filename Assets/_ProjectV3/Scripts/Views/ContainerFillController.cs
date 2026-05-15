// ChemLabSim v3 — Container Fill Controller
// Drives the liquid fill level and color on a 3D vessel using the ChemLiquid shader.
// Also supports the UI-based ReactionVesselView liquid ratio.
//
// Subscribes to ChemFxTriggeredEvent for continuous fill animation.
// Uses ColorInterpolator for smooth liquid color transitions.

using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    public class ContainerFillController : MonoBehaviour
    {
        [Header("Shader-driven vessel (optional)")]
        [Tooltip("Assign a MeshRenderer with ChemLiquid shader material.")]
        [SerializeField] private Renderer _liquidRenderer;

        // ── Shader property IDs ────────────────────────────────
        private static readonly int PropSolidHeight = Shader.PropertyToID("_SolidHeight");
        private static readonly int PropFill      = Shader.PropertyToID("_FillAmount");
        private static readonly int PropLiquidHeight = Shader.PropertyToID("_LiquidHeight");
        private static readonly int PropFoamHeight = Shader.PropertyToID("_FoamHeight");
        private static readonly int PropSolidColor = Shader.PropertyToID("_SolidColor");
        private static readonly int PropColor     = Shader.PropertyToID("_LiquidColor");
        private static readonly int PropFoamColor = Shader.PropertyToID("_FoamColor");
        private static readonly int PropSolidAnim = Shader.PropertyToID("_SolidAnimSpeed");
        private static readonly int PropLiquidAnim = Shader.PropertyToID("_LiquidAnimSpeed");
        private static readonly int PropFoamAnim = Shader.PropertyToID("_FoamAnimSpeed");
        private static readonly int PropContainerBottomY = Shader.PropertyToID("_ContainerBottomY");
        private static readonly int PropContainerHeight = Shader.PropertyToID("_ContainerHeight");
        private static readonly int PropGlow      = Shader.PropertyToID("_GlowIntensity");
        private static readonly int PropGlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int PropBubbles   = Shader.PropertyToID("_BubbleIntensity");
        private static readonly int PropSpeed     = Shader.PropertyToID("_AnimSpeed");
        private static readonly int PropWobbleX   = Shader.PropertyToID("_WobbleX");
        private static readonly int PropWobbleZ   = Shader.PropertyToID("_WobbleZ");
        private static readonly int PropFoamWidth = Shader.PropertyToID("_FoamWidth");
        private static readonly int PropTopColor  = Shader.PropertyToID("_TopColor");

        // ── Runtime state ──────────────────────────────────────
        private MaterialPropertyBlock _mpb;
        private ColorInterpolator _liquidColor;
        private ColorInterpolator _solidColor;
        private ColorInterpolator _foamColor;
        private ColorInterpolator _glowColor;
        private float _currentLiquidHeight;
        private float _targetLiquidHeight;
        private float _currentSolidHeight;
        private float _targetSolidHeight;
        private float _currentFoamHeight;
        private float _targetFoamHeight;
        private float _currentFill;
        private float _currentGlow;
        private float _targetGlow;
        private float _currentBubbles;
        private float _targetBubbles;
        private float _currentSolidAnim;
        private float _targetSolidAnim;
        private float _currentLiquidAnim;
        private float _targetLiquidAnim;
        private float _currentFoamAnim;
        private float _targetFoamAnim;
        private float _wobbleX;
        private float _wobbleZ;
        private float _wobbleDecay = 8f;
        /// <summary>Minimum wobble floor kept alive by active stirring.</summary>
        private float _stirWobbleX;
        private float _stirWobbleZ;
        private ChemFxState _lastState;
        private bool _hasLastState;
        private ReactionState _liveReactionState;
        private bool _hasLiveReactionState;
        private float _lastGasMoles;
        private float _lastElapsedTime;
        private float _latestGasProductionRate;
        private float _smoothedGasProductionRate;

        private const float FillLerpSpeed  = 2f;
        private const float LiquidLerpSpeed = 3.0f;
        private const float SolidLerpSlowSpeed = 1.1f;
        private const float FoamLerpFastSpeed = 6.0f;
        private const float LayerLerpSpeed = 3.0f;
        private const float GasRateSmoothSpeed = 8.0f;
        private const float MaxGasRateMolPerSec = 5.0f;
        private const float MaxFoamHeight = 0.25f;
        private const float GlowLerpSpeed  = 3f;
        private const float BubbleLerpSpeed = 4f;
        private const float DefaultFill    = 0f;
        private const float GasRateEpsilon = 1e-5f;
        private const float HeightEpsilon = 1e-5f;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _liquidColor = new ColorInterpolator(new Color(0.2f, 0.6f, 0.9f, 0.85f));
            _solidColor  = new ColorInterpolator(new Color(0.6f, 0.55f, 0.5f, 0.9f));
            _foamColor   = new ColorInterpolator(new Color(1f, 1f, 1f, 0.35f));
            _glowColor   = new ColorInterpolator(Color.clear);
            _currentFill = DefaultFill;
            _currentLiquidHeight = DefaultFill;
            _targetLiquidHeight = DefaultFill;
            _currentSolidHeight = 0f;
            _targetSolidHeight = 0f;
            _currentFoamHeight = 0f;
            _targetFoamHeight = 0f;
            _currentSolidAnim = 0.2f;
            _targetSolidAnim = 0.2f;
            _currentLiquidAnim = 1f;
            _targetLiquidAnim = 1f;
            _currentFoamAnim = 1f;
            _targetFoamAnim = 1f;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChemFxTriggeredEvent>(OnChemFx);
            EventBus.Subscribe<SimulationStartedEvent>(OnSimulationStarted);
            EventBus.Subscribe<SimulationTickEvent>(OnSimulationTick);
            EventBus.Subscribe<SimulationCompletedEvent>(OnSimulationCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChemFxTriggeredEvent>(OnChemFx);
            EventBus.Unsubscribe<SimulationStartedEvent>(OnSimulationStarted);
            EventBus.Unsubscribe<SimulationTickEvent>(OnSimulationTick);
            EventBus.Unsubscribe<SimulationCompletedEvent>(OnSimulationCompleted);
        }

        private void OnSimulationStarted(SimulationStartedEvent evt)
        {
            _liveReactionState = evt.State;
            _hasLiveReactionState = evt.State != null;
            _lastGasMoles = evt.State != null ? evt.State.TotalGasMoles : 0f;
            _lastElapsedTime = evt.State != null ? evt.State.ElapsedTime : 0f;
            _latestGasProductionRate = 0f;
            _smoothedGasProductionRate = 0f;

            if (_hasLiveReactionState)
            {
                var layers = ComputeLayers(_liveReactionState);
                ApplyLayerTargets(layers);
            }
        }

        private void OnSimulationTick(SimulationTickEvent evt)
        {
            _liveReactionState = evt.State;
            _hasLiveReactionState = evt.State != null;

            if (!_hasLiveReactionState)
            {
                _targetLiquidHeight = 0f;
                _targetSolidHeight = 0f;
                _targetFoamHeight = 0f;
                _latestGasProductionRate = 0f;
                _smoothedGasProductionRate = 0f;
                return;
            }

            _latestGasProductionRate = ComputeGasProductionRate(_liveReactionState);
            _smoothedGasProductionRate = Mathf.Lerp(
                _smoothedGasProductionRate,
                _latestGasProductionRate,
                Mathf.Clamp01(Time.deltaTime * GasRateSmoothSpeed));

            LayerHeights layers = ComputeLayers(_liveReactionState);
            ApplyLayerTargets(layers);

            // Foam ↔ gas production rate.
            float foamNorm = Mathf.Clamp01(_targetFoamHeight / 0.2f);
            _targetBubbles = _smoothedGasProductionRate > GasRateEpsilon
                ? Mathf.Lerp(0.1f, 1f, foamNorm)
                : 0f;
        }

        private void OnSimulationCompleted(SimulationCompletedEvent evt)
        {
            _liveReactionState = evt.State;
            _hasLiveReactionState = evt.State != null;
            _latestGasProductionRate = 0f;
            _smoothedGasProductionRate = 0f;
            _targetFoamHeight = 0f;
            _targetBubbles = 0f;
        }

        private void OnChemFx(ChemFxTriggeredEvent evt)
        {
            var s = evt.State;
            _lastState = s;
            _hasLastState = true;

            // Keep stirring visuals data-flow unidirectional: Tool -> SimulationBridge -> ChemFxState -> View
            _stirWobbleX = Mathf.Max(0f, s.StirWobbleX);
            _stirWobbleZ = Mathf.Max(0f, s.StirWobbleZ);

            if (!s.Found || s.IsFailure)
            {
                _targetSolidHeight = 0f;
                _targetLiquidHeight = 0f;
                _targetFoamHeight = 0f;
                _targetSolidAnim = 0.1f;
                _targetLiquidAnim = 0.7f;
                _targetFoamAnim = 0.2f;
                _targetGlow = 0f;
                _targetBubbles = 0f;
                _liquidColor.TransitionTo(new Color(0.2f, 0.6f, 0.9f, 0.85f), 0.8f, BlendMode.EaseOut);
                _solidColor.TransitionTo(new Color(0.6f, 0.55f, 0.5f, 0.9f), 0.8f, BlendMode.EaseOut);
                _foamColor.TransitionTo(new Color(1f, 1f, 1f, 0.2f), 0.5f, BlendMode.EaseOut);
                _glowColor.TransitionTo(Color.clear, 0.5f);
                return;
            }

            LayerHeights fxLayers = new LayerHeights
            {
                solidHeight = Safe01(s.SolidFillFraction),
                liquidHeight = Safe01(s.LiquidFillFraction),
                foamHeight = Mathf.Clamp(Safe01(s.FoamFillFraction), 0f, MaxFoamHeight)
            };
            ApplyLayerTargets(fxLayers);
            _targetSolidAnim = Mathf.Max(0.05f, s.SolidAnimSpeed);
            _targetLiquidAnim = Mathf.Max(0.05f, s.LiquidAnimSpeed);
            _targetFoamAnim = Mathf.Max(0.05f, s.FoamAnimSpeed);

            // Wobble on new reaction
            _wobbleX = 0.04f;
            _wobbleZ = 0.03f;

            // Liquid color
            Color targetLiquid;
            if (s.HasColorChange && !string.IsNullOrEmpty(s.TargetColorHex))
            {
                if (ColorUtility.TryParseHtmlString(s.TargetColorHex, out Color parsed))
                {
                    parsed.a = 0.85f;
                    targetLiquid = parsed;
                }
                else
                {
                    targetLiquid = _liquidColor.CurrentColor;
                }
            }
            else
            {
                targetLiquid = new Color(0.3f, 0.6f, 0.85f, 0.85f);
            }

            float colorDuration = Mathf.Lerp(1.5f, 0.5f, s.ReactionRate);
            _liquidColor.TransitionTo(targetLiquid, colorDuration, BlendMode.HSV);
            _solidColor.TransitionTo(s.SolidLayerColor, Mathf.Lerp(1.2f, 0.4f, s.ReactionRate), BlendMode.HSV);
            _foamColor.TransitionTo(s.FoamLayerColor, Mathf.Lerp(0.8f, 0.3f, s.ReactionRate), BlendMode.EaseInOut);

            // Glow — blends reaction enthalpy glow with external burner glow
            if (s.HasGlow || s.HasHeat || s.HeatGlowIntensity > 0.01f)
            {
                float enthalpyGlow  = Mathf.Clamp01(Mathf.Abs(s.EnthalpyKJ) / 120f);
                _targetGlow = Mathf.Clamp01(Mathf.Max(enthalpyGlow, s.HeatGlowIntensity));

                Color gc;
                if (s.HeatGlowIntensity > 0.1f)
                {
                    // Burner flame: orange-red that deepens toward red as intensity grows
                    gc = Color.Lerp(new Color(1f, 0.50f, 0.12f), new Color(1f, 0.20f, 0.05f),
                                    s.HeatGlowIntensity);
                }
                else if (ColorUtility.TryParseHtmlString(s.GlowColorHex, out Color glowParsed))
                {
                    gc = glowParsed;
                }
                else
                {
                    gc = s.IsExothermic ? new Color(1f, 0.5f, 0.12f) : new Color(0.3f, 0.65f, 1f);
                }
                _glowColor.TransitionTo(gc, 0.6f, BlendMode.EaseInOut);
            }
            else
            {
                _targetGlow = 0f;
                _glowColor.TransitionTo(Color.clear, 0.8f);
            }

            // Bubbles — take the max of gas-rate bubbles (set in OnSimulationTick)
            // and boiling-driven bubble intensity from the heating system
            if (s.BubbleIntensity > 0f)
                _targetBubbles = Mathf.Max(_targetBubbles, s.BubbleIntensity);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_hasLastState)
                UpdateFill(_lastState);

            // Smoothly lerp continuous values
            _currentLiquidHeight = StableLerp01(_currentLiquidHeight, _targetLiquidHeight, dt, LiquidLerpSpeed);
            _currentSolidHeight = StableLerp01(_currentSolidHeight, _targetSolidHeight, dt, SolidLerpSlowSpeed);
            _currentFoamHeight = StableLerp01(_currentFoamHeight, _targetFoamHeight, dt, FoamLerpFastSpeed);

            _currentLiquidHeight = Safe01(_currentLiquidHeight);
            _currentSolidHeight = Safe01(_currentSolidHeight);
            _currentFoamHeight = Mathf.Clamp(Safe01(_currentFoamHeight), 0f, MaxFoamHeight);

            // Visual consistency: enforce stacking constraints every frame.
            EnforceLayerOrdering(ref _currentSolidHeight, ref _currentLiquidHeight, ref _currentFoamHeight);

            _currentSolidAnim = StableLerp01(_currentSolidAnim, Safe01(_targetSolidAnim), dt, LayerLerpSpeed);
            _currentLiquidAnim = StableLerp01(_currentLiquidAnim, Safe01(_targetLiquidAnim), dt, LayerLerpSpeed);
            _currentFoamAnim = StableLerp01(_currentFoamAnim, Safe01(_targetFoamAnim), dt, LayerLerpSpeed);
            _currentGlow = StableLerp01(_currentGlow, Safe01(_targetGlow), dt, GlowLerpSpeed);
            _currentBubbles = StableLerp01(_currentBubbles, Safe01(_targetBubbles), dt, BubbleLerpSpeed);

            // Wobble decay
            _wobbleX = StableLerp01(_wobbleX, 0f, dt, _wobbleDecay);
            _wobbleZ = StableLerp01(_wobbleZ, 0f, dt, _wobbleDecay);
            // Stirring keeps wobble alive: clamp floor to stir-driven minimum
            if (_wobbleX < _stirWobbleX) _wobbleX = _stirWobbleX;
            if (_wobbleZ < _stirWobbleZ) _wobbleZ = _stirWobbleZ;

            // Update colors
            Color sc = _solidColor.Update(dt);
            Color lc = _liquidColor.Update(dt);
            Color fc = _foamColor.Update(dt);
            Color gc = _glowColor.Update(dt);

            // Apply to shader via MaterialPropertyBlock
            if (_liquidRenderer != null)
            {
                _liquidRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(PropSolidHeight, _currentSolidHeight);
                _mpb.SetFloat(PropLiquidHeight, _currentLiquidHeight);
                _mpb.SetFloat(PropFoamHeight, _currentFoamHeight);
                _mpb.SetColor(PropSolidColor, sc);
                _mpb.SetColor(PropColor, lc);
                _mpb.SetColor(PropFoamColor, fc);
                _mpb.SetFloat(PropSolidAnim, _currentSolidAnim);
                _mpb.SetFloat(PropLiquidAnim, _currentLiquidAnim);
                _mpb.SetFloat(PropFoamAnim, _currentFoamAnim);

                Bounds b = _liquidRenderer.bounds;
                _mpb.SetFloat(PropContainerBottomY, b.min.y);
                _mpb.SetFloat(PropContainerHeight, Mathf.Max(0.001f, b.size.y));

                _mpb.SetFloat(PropGlow, _currentGlow);
                _mpb.SetColor(PropGlowColor, gc);
                _mpb.SetFloat(PropBubbles, _currentBubbles);
                _mpb.SetFloat(PropSpeed, 1f);
                _mpb.SetFloat(PropWobbleX, _wobbleX);
                _mpb.SetFloat(PropWobbleZ, _wobbleZ);

                // Foam visible during active bubbling
                _mpb.SetFloat(PropFoamWidth, _currentBubbles > 0.05f ? 0.02f : 0f);

                // Compat aggregate fill + top color tint
                _mpb.SetFloat(PropFill, Safe01(_currentSolidHeight + _currentLiquidHeight));

                // Top color slightly lighter
                Color topCol = lc;
                topCol.r = Mathf.Min(1f, topCol.r + 0.1f);
                topCol.g = Mathf.Min(1f, topCol.g + 0.1f);
                topCol.b = Mathf.Min(1f, topCol.b + 0.1f);
                _mpb.SetColor(PropTopColor, topCol);

                _liquidRenderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// Updates liquid fill based on scientific liquid-volume fill logic.
        /// Pattern mirrors: target → lerp current → bind _FillAmount.
        /// </summary>
        private void UpdateFill(ChemFxState state)
        {
            float targetFill = (!state.Found || state.IsFailure)
                ? 0f
                : Safe01(state.SolidFillFraction + state.LiquidFillFraction);

            _currentFill = StableLerp01(_currentFill, targetFill, Time.deltaTime, FillLerpSpeed);

            if (_liquidRenderer != null)
            {
                _liquidRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(PropFill, _currentFill);
                _liquidRenderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>Get the current fill fraction (0–1) for UI sync.</summary>
        public float GetCurrentFill() => _currentFill;

        /// <summary>Get the current liquid color for UI sync.</summary>
        public Color GetCurrentLiquidColor() => _liquidColor.CurrentColor;

        /// <summary>
        /// Compute layered heights from live ReactionState.
        /// liquid ↔ dissolved species only, solid ↔ precipitate-only contribution, foam ↔ gas rate.
        /// </summary>
        private LayerHeights ComputeLayers(ReactionState state)
        {
            if (state == null)
                return default;

            float containerVolume = Mathf.Max(1e-6f, state.VolumeLiters);
            LayerHeights layers = PhaseInteractionModel.ComputeLayerHeights(
                state,
                containerVolume,
                _smoothedGasProductionRate);

            // solid ↔ precipitate only: remove baseline solid mass and keep precipitate inventory contribution.
            float precipitatedMoles = 0f;
            if (state.Reactants != null)
                for (int i = 0; i < state.Reactants.Length; i++)
                    precipitatedMoles += Mathf.Max(0f, state.Reactants[i]?.PrecipitatedMoles ?? 0f);
            if (state.Products != null)
                for (int i = 0; i < state.Products.Length; i++)
                    precipitatedMoles += Mathf.Max(0f, state.Products[i]?.PrecipitatedMoles ?? 0f);

            float precipMolarVolume = 0.018f;
            if (state.phases != null)
            {
                foreach (var kv in state.phases)
                {
                    var p = kv.Value;
                    if (p != null && p.MolarVolumeLPerMol > 0f)
                    {
                        precipMolarVolume = p.MolarVolumeLPerMol;
                        break;
                    }
                }
            }

            float precipSolidHeight = (precipitatedMoles * precipMolarVolume) / containerVolume;
            layers.solidHeight = Safe01(precipSolidHeight);
            layers.liquidHeight = Safe01(layers.liquidHeight); // liquid remains dissolved-only from phase.liquidMoles
            layers.foamHeight = _latestGasProductionRate > GasRateEpsilon
                ? Mathf.Clamp(Safe01(layers.foamHeight), 0f, MaxFoamHeight)
                : 0f;

            // epsilon cleanup
            if (layers.solidHeight < HeightEpsilon) layers.solidHeight = 0f;
            if (layers.liquidHeight < HeightEpsilon) layers.liquidHeight = 0f;
            if (layers.foamHeight < HeightEpsilon) layers.foamHeight = 0f;

            EnforceLayerOrdering(ref layers.solidHeight, ref layers.liquidHeight, ref layers.foamHeight);

            return layers;
        }

        private float ComputeGasProductionRate(ReactionState state)
        {
            if (state == null) return 0f;

            float dt = Mathf.Max(1e-4f, state.ElapsedTime - _lastElapsedTime);
            float gas = Mathf.Max(0f, state.TotalGasMoles);
            float rate = Mathf.Max(0f, (gas - _lastGasMoles) / dt);
            rate = Mathf.Min(rate, MaxGasRateMolPerSec);
            _lastGasMoles = gas;
            _lastElapsedTime = state.ElapsedTime;

            return SafeNonNegativeFinite(rate);
        }

        private static float Safe01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Mathf.Clamp01(value);
        }

        private static float SafeNonNegativeFinite(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Mathf.Max(0f, value);
        }

        private static float StableLerp01(float current, float target, float dt, float speed)
        {
            float t = Mathf.Clamp01(Mathf.Max(0f, dt) * Mathf.Max(0f, speed));
            return Mathf.Lerp(Safe01(current), Safe01(target), t);
        }

        private static void EnforceLayerOrdering(ref float solid, ref float liquid, ref float foam)
        {
            solid = Safe01(solid);
            liquid = Safe01(liquid);
            foam = Mathf.Clamp(Safe01(foam), 0f, MaxFoamHeight);

            // solid always bottom, liquid above solid, foam above liquid
            float maxLiquid = Mathf.Clamp01(1f - solid);
            liquid = Mathf.Min(liquid, maxLiquid);

            float maxFoam = Mathf.Clamp01(1f - solid - liquid);
            foam = Mathf.Min(foam, maxFoam);
        }

        private void ApplyLayerTargets(LayerHeights layers)
        {
            _targetSolidHeight = Safe01(layers.solidHeight);
            _targetLiquidHeight = Safe01(layers.liquidHeight);
            _targetFoamHeight = Mathf.Clamp(Safe01(layers.foamHeight), 0f, MaxFoamHeight);

            if (_targetSolidHeight < HeightEpsilon) _targetSolidHeight = 0f;
            if (_targetLiquidHeight < HeightEpsilon) _targetLiquidHeight = 0f;
            if (_targetFoamHeight < HeightEpsilon) _targetFoamHeight = 0f;

            EnforceLayerOrdering(ref _targetSolidHeight, ref _targetLiquidHeight, ref _targetFoamHeight);
        }
    }
}
