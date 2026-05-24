// ChemLabSim v3 — Color Interpolator
// Smooth, time-based color transition system for chemistry visuals.
// Supports:
//   - Linear / ease-in-out / ease-out interpolation
//   - Queue-based multi-step transitions (reactant → intermediate → product)
//   - HSV-aware blending for more natural chemical color changes
//   - Independent alpha channel control

using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Views
{
    public enum BlendMode { Linear, EaseOut, EaseInOut, HSV }

    /// <summary>A single color transition step.</summary>
    public struct ColorStep
    {
        public Color Target;
        public float Duration;
        public BlendMode Mode;

        public ColorStep(Color target, float duration, BlendMode mode = BlendMode.EaseInOut)
        {
            Target   = target;
            Duration = Mathf.Max(0.01f, duration);
            Mode     = mode;
        }
    }

    /// <summary>
    /// Drives smooth color transitions for liquid, glow, and UI elements.
    /// Call Update() every frame. Read CurrentColor for the interpolated result.
    /// </summary>
    public class ColorInterpolator
    {
        private readonly Queue<ColorStep> _queue = new Queue<ColorStep>();
        private Color _current;
        private Color _from;
        private Color _to;
        private float _elapsed;
        private float _duration;
        private BlendMode _mode;
        private bool _transitioning;

        public Color CurrentColor => _current;
        public bool IsTransitioning => _transitioning || _queue.Count > 0;

        public ColorInterpolator(Color initial)
        {
            _current = initial;
            _from = initial;
            _to = initial;
        }

        /// <summary>Immediately set color without transition.</summary>
        public void SetImmediate(Color color)
        {
            _queue.Clear();
            _current = color;
            _from = color;
            _to = color;
            _transitioning = false;
        }

        /// <summary>Transition to a target color over duration.</summary>
        public void TransitionTo(Color target, float duration, BlendMode mode = BlendMode.EaseInOut)
        {
            _queue.Enqueue(new ColorStep(target, duration, mode));
            if (!_transitioning)
                StartNext();
        }

        /// <summary>Queue a multi-step color sequence (e.g., reaction stages).</summary>
        public void QueueSequence(IList<ColorStep> steps)
        {
            for (int i = 0; i < steps.Count; i++)
                _queue.Enqueue(steps[i]);
            if (!_transitioning)
                StartNext();
        }

        /// <summary>Call every frame. Returns current interpolated color.</summary>
        public Color Update(float deltaTime)
        {
            if (!_transitioning)
                return _current;

            _elapsed += deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float eased = ApplyEasing(t, _mode);

            _current = _mode == BlendMode.HSV
                ? LerpHSV(_from, _to, eased)
                : Color.Lerp(_from, _to, eased);

            if (t >= 1f)
            {
                _current = _to;
                _transitioning = false;

                if (_queue.Count > 0)
                    StartNext();
            }

            return _current;
        }

        private void StartNext()
        {
            if (_queue.Count == 0) return;

            var step = _queue.Dequeue();
            _from = _current;
            _to = step.Target;
            _duration = step.Duration;
            _mode = step.Mode;
            _elapsed = 0f;
            _transitioning = true;
        }

        private static float ApplyEasing(float t, BlendMode mode)
        {
            switch (mode)
            {
                case BlendMode.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case BlendMode.EaseInOut:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default: // Linear, HSV
                    return t;
            }
        }

        /// <summary>Interpolate through HSV space for more natural color mixing.</summary>
        private static Color LerpHSV(Color a, Color b, float t)
        {
            float aH, aS, aV, bH, bS, bV;
            Color.RGBToHSV(a, out aH, out aS, out aV);
            Color.RGBToHSV(b, out bH, out bS, out bV);

            // Shortest path on hue circle
            float dH = bH - aH;
            if (dH > 0.5f)  dH -= 1f;
            if (dH < -0.5f) dH += 1f;

            float h = aH + dH * t;
            if (h < 0f) h += 1f;
            if (h > 1f) h -= 1f;

            float s = Mathf.Lerp(aS, bS, t);
            float v = Mathf.Lerp(aV, bV, t);
            float alpha = Mathf.Lerp(a.a, b.a, t);

            var result = Color.HSVToRGB(h, s, v);
            result.a = alpha;
            return result;
        }
    }
}
