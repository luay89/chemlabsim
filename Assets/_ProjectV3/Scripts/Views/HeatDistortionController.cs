// ChemLabSim v3 — Heat Distortion Controller
// Manages the fullscreen heat distortion post-process effect.
// Subscribes to ChemFxTriggeredEvent and controls distortion material properties
// based on enthalpy / temperature data from the chemistry engine.
//
// Requires: HeatDistortion.shader material assigned in scene,
//           or auto-creates from shader at runtime.

using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    [RequireComponent(typeof(Camera))]
    public class HeatDistortionController : MonoBehaviour
    {
        private Material _distortionMat;
        private float _targetStrength;
        private float _currentStrength;
        private float _targetSpeed = 1f;
        private Vector2 _heatCenter = new Vector2(0.5f, 0.4f);
        private float _heatRadius = 0.25f;

        private const float LerpSpeed = 3f;
        private const float MaxStrength = 0.05f;
        private const float FadeSpeed = 2f;

        private static readonly int PropStrength = Shader.PropertyToID("_DistortionStrength");
        private static readonly int PropCenter   = Shader.PropertyToID("_HeatCenter");
        private static readonly int PropRadius   = Shader.PropertyToID("_HeatRadius");
        private static readonly int PropSpeed    = Shader.PropertyToID("_AnimSpeed");

        private void OnEnable()
        {
            EventBus.Subscribe<ChemFxTriggeredEvent>(OnChemFx);
            InitMaterial();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChemFxTriggeredEvent>(OnChemFx);
            if (_distortionMat != null)
                Destroy(_distortionMat);
        }

        private void InitMaterial()
        {
            var shader = Shader.Find("ChemLabSim/HeatDistortion");
            if (shader == null)
            {
                Debug.LogWarning("[HeatDistortion] Shader not found. Effect disabled.");
                return;
            }
            _distortionMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void OnChemFx(ChemFxTriggeredEvent evt)
        {
            var s = evt.State;

            if (!s.Found || s.IsFailure || !s.HasHeat)
            {
                _targetStrength = 0f;
                return;
            }

            // Map enthalpy magnitude to distortion strength
            float absH = Mathf.Abs(s.EnthalpyKJ);
            float normalized = Mathf.Clamp01(absH / 200f); // 200 kJ/mol = max distortion
            _targetStrength = normalized * MaxStrength;

            // Map reaction rate to animation speed
            _targetSpeed = Mathf.Lerp(0.5f, 3f, s.ReactionRate);
        }

        private void Update()
        {
            if (_distortionMat == null) return;

            // Smooth lerp toward target
            _currentStrength = Mathf.Lerp(_currentStrength, _targetStrength,
                Time.deltaTime * (_targetStrength > _currentStrength ? LerpSpeed : FadeSpeed));

            // Kill tiny residual
            if (_currentStrength < 0.0005f && _targetStrength <= 0f)
                _currentStrength = 0f;
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_distortionMat == null || _currentStrength < 0.0001f)
            {
                Graphics.Blit(src, dst);
                return;
            }

            _distortionMat.SetFloat(PropStrength, _currentStrength);
            _distortionMat.SetVector(PropCenter, new Vector4(_heatCenter.x, _heatCenter.y, 0, 0));
            _distortionMat.SetFloat(PropRadius, _heatRadius);
            _distortionMat.SetFloat(PropSpeed, _targetSpeed);

            Graphics.Blit(src, dst, _distortionMat);
        }

        /// <summary>Set the screen-space position of the heat source (0-1 UV).</summary>
        public void SetHeatCenter(Vector2 centerUV)
        {
            _heatCenter = centerUV;
        }

        /// <summary>Set the distortion radius in UV space.</summary>
        public void SetHeatRadius(float radius)
        {
            _heatRadius = Mathf.Clamp(radius, 0.05f, 0.8f);
        }
    }
}
