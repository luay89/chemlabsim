using ChemLabSimV3.Views;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Engine.SimulationV1
{
    /// <summary>
    /// V1 single-reaction simulation loop.
    /// - Runs every frame via Update()
    /// - Uses temperature + stirring + surface area (from grinding) to evolve progress
    /// - Binds progression to color, gas bubbles, and foam
    ///
    /// Attach this to a scene object and wire references in the Inspector.
    /// </summary>
    public class ReactionSimulationEngine : MonoBehaviour
    {
        public enum ReactionType
        {
            AcidCarbonate
        }

        [Header("Reaction Selection")]
        [SerializeField] private ReactionType defaultReactionType = ReactionType.AcidCarbonate;

        [Header("Input Sources")]
        [SerializeField] private TemperatureSliderView temperatureSlider;
        [SerializeField] private StirringSliderView stirringSlider;
        [SerializeField] private GrindingSliderView grindingSlider;
        [SerializeField] private Slider stirringSliderRaw;
        [SerializeField] private Slider grindingSliderRaw;

        [Header("Visual Binding")]
        [SerializeField] private Renderer liquidRenderer;
        [SerializeField] private string liquidColorProperty = "_BaseColor";
        [SerializeField] private Image liquidImage;
        [SerializeField] private ParticleSystem gasBubbleParticles;
        [SerializeField] private GameObject foamObject;
        [SerializeField] private ParticleSystem foamParticles;

        [Header("Reaction Profile")]
        [SerializeField] private bool autoStart = true;
        [SerializeField, Min(0.001f)] private float reactionConstantK = 2.0f;
        [SerializeField, Min(0.05f)] private float reactionDuration = 4.2f;
        [SerializeField, Min(0.05f)] private float initialConcentration = 1.0f;
        [SerializeField, Min(0.1f)] private float concentrationDecayPower = 1.3f;
        [SerializeField, Min(0f)] private float stirringMixBoost = 0.45f;
        [SerializeField, Min(0f)] private float surfaceAreaGain = 1.5f;
        [SerializeField, Min(0f)] private float exothermicHeatBoost = 30f;
        [SerializeField, Min(0.05f)] private float maxGasVisual = 2.2f;
        [SerializeField, Min(0.01f)] private float foamThreshold = 0.55f;

        [Header("Realism Curve")]
        [Tooltip("Shape should start low, rise to a peak, then fall (sigmoid/exponential-like kinetics).")]
        [SerializeField] private AnimationCurve progressionCurve = new AnimationCurve(
            new Keyframe(0.00f, 0.10f),
            new Keyframe(0.20f, 0.80f),
            new Keyframe(0.40f, 1.00f),
            new Keyframe(0.70f, 0.30f),
            new Keyframe(1.00f, 0.00f)
        );

        [Header("Color Evolution")]
        [SerializeField] private Color startColor = new Color(0.28f, 0.55f, 0.92f, 0.86f);
        [SerializeField] private Color endColor = new Color(0.96f, 0.70f, 0.24f, 0.90f);

        public ReactionState State { get; private set; } = new ReactionState();
        public bool IsRunning { get; private set; }
        public float FoamLevel => Mathf.Clamp01(
            Mathf.InverseLerp(foamThreshold, foamThreshold + maxGasVisual, State.gasAmount));
        public bool FoamActive => State.gasAmount >= foamThreshold;

        private Material _liquidMaterialInstance;
        private float _targetTemperature = 25f;
        private float _stirring;
        private float _grinding;
        private float _reactionTime;

        public ReactionType ActiveReactionType => defaultReactionType;

        private void Awake()
        {
            ApplyReactionPreset(defaultReactionType);
            CacheRendererMaterial();
            ResetReaction();
            ApplyVisualBinding();
        }

        private void OnEnable()
        {
            BindInputs();
            if (autoStart)
                StartReaction();
        }

        private void OnDisable()
        {
            UnbindInputs();
        }

        private void Update()
        {
            if (!IsRunning)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            // Keep values live even if events are not wired.
            PullInputValues();

            // Surface area comes from grinding control (requirement #4).
            float surfaceAreaFactor = 1f + (_grinding * surfaceAreaGain);

            // Effective concentration decreases as reactants are consumed; stirring improves mixing.
            float depletion = Mathf.Pow(1f - State.progress, concentrationDecayPower);
            float concentration = initialConcentration * depletion * (1f + _stirring * stirringMixBoost);

            // f(time): burst profile for acid-carbonate behavior.
            _reactionTime += dt;
            float normalizedTime = Mathf.Clamp01(_reactionTime / reactionDuration);
            float timeCurveFactor = Mathf.Max(0f, progressionCurve.Evaluate(normalizedTime));

            // reactionRate = k * surfaceArea * concentration * f(time)
            State.reactionRate = reactionConstantK * surfaceAreaFactor * concentration * timeCurveFactor;
            State.progress = Mathf.Clamp01(State.progress + (State.reactionRate * dt));

            // Mild thermal response to keep the reaction visually alive.
            float progressPulse = Mathf.Sin(normalizedTime * Mathf.PI);
            float dynamicHeat = exothermicHeatBoost * progressPulse * (0.7f + 0.3f * _stirring);
            float thermalTarget = _targetTemperature + dynamicHeat;
            State.temperature = Mathf.Lerp(State.temperature, thermalTarget, dt * 2.6f);

            // Gas model (requested): gasAmount += reactionRate * deltaTime
            State.gasAmount += State.reactionRate * dt;
            State.gasAmount = Mathf.Max(0f, State.gasAmount);

            // Progress drives color fade.
            State.colorShift = Mathf.SmoothStep(0f, 1f, State.progress);

            ApplyVisualBinding();

            if (State.progress >= 0.999f || (_reactionTime >= reactionDuration && State.reactionRate <= 0.001f))
            {
                State.progress = 1f;
                State.reactionRate = 0f;
                IsRunning = false;
            }
        }

        public void StartReaction()
        {
            IsRunning = true;
        }

        public void StopReaction()
        {
            IsRunning = false;
            State.reactionRate = 0f;
            ApplyVisualBinding();
        }

        public void ResetReaction()
        {
            PullInputValues();

            State.progress = 0f;
            State.reactionRate = 0f;
            State.gasAmount = 0f;
            State.colorShift = 0f;
            State.temperature = _targetTemperature;
            _reactionTime = 0f;
        }

        private void OnTemperatureChanged(float value) => _targetTemperature = value;
        private void OnStirringChanged(float value) => _stirring = Mathf.Clamp01(value);
        private void OnGrindingChanged(float value) => _grinding = Mathf.Clamp01(value);

        private void OnStirringRawChanged(float value) => _stirring = Mathf.Clamp01(value);
        private void OnGrindingRawChanged(float value) => _grinding = Mathf.Clamp01(value);

        private void PullInputValues()
        {
            _targetTemperature = temperatureSlider != null ? temperatureSlider.GetValue() : _targetTemperature;
            _stirring = stirringSlider != null
                ? Mathf.Clamp01(stirringSlider.GetValue())
                : (stirringSliderRaw != null ? Mathf.Clamp01(stirringSliderRaw.value) : _stirring);
            _grinding = grindingSlider != null
                ? Mathf.Clamp01(grindingSlider.GetValue())
                : (grindingSliderRaw != null ? Mathf.Clamp01(grindingSliderRaw.value) : _grinding);
        }

        private void ApplyReactionPreset(ReactionType reactionType)
        {
            switch (reactionType)
            {
                case ReactionType.AcidCarbonate:
                default:
                    reactionConstantK = 2.0f;
                    initialConcentration = 1.0f;
                    break;
            }
        }

        private void ApplyVisualBinding()
        {
            Color currentLiquidColor = Color.Lerp(startColor, endColor, State.colorShift);

            if (_liquidMaterialInstance != null && _liquidMaterialInstance.HasProperty(liquidColorProperty))
            {
                _liquidMaterialInstance.SetColor(liquidColorProperty, currentLiquidColor);
            }

            if (liquidImage != null)
            {
                liquidImage.color = currentLiquidColor;
            }

            float normalizedGas = Mathf.Clamp01(State.gasAmount / Mathf.Max(0.05f, maxGasVisual));

            if (gasBubbleParticles != null)
            {
                var emission = gasBubbleParticles.emission;
                float bubbleRate = Mathf.Lerp(0f, 120f, normalizedGas);
                float noise = Random.Range(0.9f, 1.1f);
                bubbleRate *= noise;
                emission.rateOverTime = Mathf.Max(0f, bubbleRate);

                if (normalizedGas > 0.01f)
                {
                    if (!gasBubbleParticles.isPlaying)
                        gasBubbleParticles.Play();
                }
                else if (gasBubbleParticles.isPlaying)
                {
                    gasBubbleParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            bool foamActive = State.gasAmount >= foamThreshold;

            if (foamObject != null)
                foamObject.SetActive(foamActive);

            if (foamParticles != null)
            {
                var foamEmission = foamParticles.emission;
                foamEmission.rateOverTime = foamActive
                    ? Mathf.Lerp(4f, 65f, Mathf.InverseLerp(foamThreshold, foamThreshold + maxGasVisual, State.gasAmount))
                    : 0f;

                if (foamActive)
                {
                    if (!foamParticles.isPlaying)
                        foamParticles.Play();
                }
                else if (foamParticles.isPlaying)
                {
                    foamParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void CacheRendererMaterial()
        {
            if (liquidRenderer == null)
                return;

            _liquidMaterialInstance = liquidRenderer.material;
        }

        private void BindInputs()
        {
            if (temperatureSlider != null)
                temperatureSlider.OnValueChanged += OnTemperatureChanged;
            if (stirringSlider != null)
                stirringSlider.OnValueChanged += OnStirringChanged;
            if (grindingSlider != null)
                grindingSlider.OnValueChanged += OnGrindingChanged;

            if (stirringSliderRaw != null)
                stirringSliderRaw.onValueChanged.AddListener(OnStirringRawChanged);
            if (grindingSliderRaw != null)
                grindingSliderRaw.onValueChanged.AddListener(OnGrindingRawChanged);
        }

        private void UnbindInputs()
        {
            if (temperatureSlider != null)
                temperatureSlider.OnValueChanged -= OnTemperatureChanged;
            if (stirringSlider != null)
                stirringSlider.OnValueChanged -= OnStirringChanged;
            if (grindingSlider != null)
                grindingSlider.OnValueChanged -= OnGrindingChanged;

            if (stirringSliderRaw != null)
                stirringSliderRaw.onValueChanged.RemoveListener(OnStirringRawChanged);
            if (grindingSliderRaw != null)
                grindingSliderRaw.onValueChanged.RemoveListener(OnGrindingRawChanged);
        }
    }
}
