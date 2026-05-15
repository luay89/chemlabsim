// ChemLabSim v3 — Reaction Vessel View (UI-based VFX)
// Creates an animated beaker visualization inside the Canvas overlay.
// Shows liquid color changes, bubbles, precipitate, heat glow, sparks,
// smoke, foam, frost, and flash effects when reactions occur.
//
// The existing ReactionFxView uses 3D ParticleSystems which are invisible
// behind a ScreenSpace-Overlay Canvas. This view renders effects IN the UI.

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    public class ReactionVesselView : MonoBehaviour
    {
        // ── Pool sizes ──────────────────────────────────────────
        private const int BUBBLES      = 25;
        private const int PRECIPITATE  = 15;
        private const int SPARKS       = 12;
        private const int SMOKE        = 10;
        private const int FOAM_DOTS    = 18;

        // ── Vessel dimensions (design coords at 1920×1080) ──────
        private const float VW = 260f;   // vessel width
        private const float VH = 360f;   // vessel height
        private const float LIQUID_RATIO = 0.62f;

        // ── UI references ───────────────────────────────────────
        private CanvasGroup vesselGroup;
        private RectTransform vesselRect;
        private Image liquidImage;
        private Image glowImage;
        private Image flashImage;
        private Image frostImage;
        private Image foamStrip;

        // ── Particle pools ──────────────────────────────────────
        private Image[] bubbles;
        private Image[] precipitates;
        private Image[] sparks;
        private Image[] smokes;
        private Image[] foamDots;

        // ── Generated sprites ───────────────────────────────────
        private Sprite circleSprite;
        private Sprite softSprite;

        // ── Colors ──────────────────────────────────────────────
        private static readonly Color BeakerBg    = new Color(0.10f, 0.16f, 0.25f, 0.94f);
        private static readonly Color BorderColor = new Color(0.38f, 0.55f, 0.70f, 0.70f);
        private static readonly Color LiquidIdle  = new Color(0.32f, 0.52f, 0.72f, 0.55f);
        private static readonly Color Transparent = new Color(1, 1, 1, 0);

        // ── Lifecycle ───────────────────────────────────────────

        private void Awake()
        {
            circleSprite = MakeCircle(32);
            softSprite   = MakeCircle(64);
            BuildUI();
            vesselGroup.alpha = 0f;
        }

        private void OnEnable()  => EventBus.Subscribe<FxTriggeredEvent>(OnFx);
        private void OnDisable() => EventBus.Unsubscribe<FxTriggeredEvent>(OnFx);
        private void OnFx(FxTriggeredEvent e) => Play(e.State);

        // ════════════════════════════════════════════════════════
        //  UI CONSTRUCTION
        // ════════════════════════════════════════════════════════

        private void BuildUI()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // Full-screen root (hosts flash + centered vessel)
            var root = MakeRT("_VesselRoot", canvas.transform);
            Stretch(root);
            vesselGroup = root.gameObject.AddComponent<CanvasGroup>();
            vesselGroup.interactable = false;
            vesselGroup.blocksRaycasts = false;

            // Flash overlay (full screen)
            flashImage = Img("_Flash", root, Transparent);
            Stretch(flashImage.rectTransform);

            // Vessel container (centered)
            vesselRect = MakeRT("_Vessel", root);
            vesselRect.anchorMin = vesselRect.anchorMax = new Vector2(0.5f, 0.5f);
            vesselRect.sizeDelta = new Vector2(VW, VH);

            // Glow halo (larger than beaker)
            glowImage = Img("_Glow", vesselRect, Transparent, softSprite);
            var gr = glowImage.rectTransform;
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = new Vector2(-35, -35);
            gr.offsetMax = new Vector2(35, 35);

            // Beaker body (with mask to clip bubbles/precipitate)
            var beaker = Img("_Beaker", vesselRect, BeakerBg);
            var br = beaker.rectTransform;
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = br.offsetMax = Vector2.zero;
            beaker.gameObject.AddComponent<RectMask2D>();

            // Liquid fill (bottom portion of beaker)
            liquidImage = Img("_Liquid", br, LiquidIdle);
            var lr = liquidImage.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = new Vector2(1f, LIQUID_RATIO);
            lr.offsetMin = new Vector2(4, 4);
            lr.offsetMax = new Vector2(-4, 0);

            // Foam strip at liquid surface
            foamStrip = Img("_FoamStrip", br, Transparent);
            var fs = foamStrip.rectTransform;
            fs.anchorMin = new Vector2(0f, LIQUID_RATIO - 0.02f);
            fs.anchorMax = new Vector2(1f, LIQUID_RATIO + 0.06f);
            fs.offsetMin = new Vector2(4, 0);
            fs.offsetMax = new Vector2(-4, 0);

            // Frost overlay
            frostImage = Img("_Frost", br, new Color(0.7f, 0.9f, 1f, 0f));
            Stretch(frostImage.rectTransform);

            // Particle pools inside beaker (clipped by mask)
            bubbles      = Pool("_Bub",  br, BUBBLES,     circleSprite, new Color(1,1,1,0.7f),         8);
            precipitates = Pool("_Prec", br, PRECIPITATE,  circleSprite, new Color(.92f,.90f,.85f,.8f), 6);
            foamDots     = Pool("_Foam", br, FOAM_DOTS,    circleSprite, Color.white,                  10);

            // Pools outside mask (smoke rises above, sparks scatter)
            smokes = Pool("_Smk", vesselRect, SMOKE,  softSprite,   new Color(.5f,.5f,.52f,.5f), 40);
            sparks = Pool("_Spk", vesselRect, SPARKS, circleSprite, new Color(1,.85f,.3f,.95f),   5);

            // Rim and base decoration
            Bar("_Rim",  vesselRect, new Vector2(0,1), new Vector2(1,1), BorderColor, 4f, 6f);
            Bar("_Base", vesselRect, new Vector2(0,0), new Vector2(1,0), new Color(BorderColor.r, BorderColor.g, BorderColor.b, 0.5f), 5f, 3f);
        }

        // ════════════════════════════════════════════════════════
        //  REACTION ORCHESTRATOR
        // ════════════════════════════════════════════════════════

        private void Play(FxState s)
        {
            StopAllCoroutines();
            ResetAll();
            StartCoroutine(Sequence(s));
        }

        private IEnumerator Sequence(FxState s)
        {
            liquidImage.color = LiquidIdle;
            glowImage.color = Transparent;
            frostImage.color = new Color(0.7f, 0.9f, 1f, 0f);
            foamStrip.color = Transparent;

            // Fade in
            yield return Fade(0f, 1f, 0.3f);

            float dur = 3.5f;

            if (s.PlayFail)
            {
                StartCoroutine(Flash(new Color(1, 0.2f, 0.15f, 0.4f), 0.5f));
                StartCoroutine(Shake(10f, 0.45f));
                StartCoroutine(LiquidColor(new Color(0.35f, 0.22f, 0.22f, 0.5f), 0.4f));
                dur = 2f;
            }
            else
            {
                if (s.PlaySuccess)
                    StartCoroutine(Flash(new Color(0.3f, 1f, 0.5f, 0.2f), 0.4f));

                if (s.PlayColorChange && !string.IsNullOrEmpty(s.ColorChangeHex)
                    && ColorUtility.TryParseHtmlString(s.ColorChangeHex, out Color c))
                {
                    c.a = 0.78f;
                    StartCoroutine(LiquidColor(c, 1f));
                }

                if (s.PlayGas)    { StartCoroutine(Bubbles(3.5f));  dur = Mathf.Max(dur, 4f); }
                if (s.PlayHeat)   { StartCoroutine(HeatGlow(s.TemperatureDelta, 3.5f)); dur = Mathf.Max(dur, 4f); }
                if (s.PlayPrecipitate) { StartCoroutine(Precipitate(3f)); dur = Mathf.Max(dur, 3.5f); }
                if (s.PlaySparks) { StartCoroutine(SparkBurst(1.5f)); StartCoroutine(Flash(new Color(1,0.95f,0.7f,0.45f), 0.2f)); }
                if (s.PlaySmoke)  { StartCoroutine(Smoke(3.5f));  dur = Mathf.Max(dur, 4f); }
                if (s.PlayFoam)   { StartCoroutine(Foam(3f));     dur = Mathf.Max(dur, 3.5f); }
                if (s.PlayFrost)  { StartCoroutine(Frost(3f));    dur = Mathf.Max(dur, 3.5f); }
                if (s.PlayGlow)   { StartCoroutine(HeatGlow(15f, 3f)); dur = Mathf.Max(dur, 3.5f); }
                if (s.PlayCatalyst) StartCoroutine(Flash(new Color(0.4f, 0.9f, 1f, 0.3f), 0.3f));
            }

            yield return new WaitForSeconds(dur);
            yield return Fade(1f, 0f, 1.2f);
            ResetAll();
        }

        // ════════════════════════════════════════════════════════
        //  INDIVIDUAL EFFECTS
        // ════════════════════════════════════════════════════════

        // ── Screen flash ────────────────────────────────────────
        private IEnumerator Flash(Color c, float dur)
        {
            flashImage.color = c;
            float t = 0;
            while (t < dur) { t += Time.deltaTime; flashImage.color = new Color(c.r, c.g, c.b, c.a * (1f - t/dur)); yield return null; }
            flashImage.color = Transparent;
        }

        // ── Vessel shake ────────────────────────────────────────
        private IEnumerator Shake(float mag, float dur)
        {
            Vector2 origin = vesselRect.anchoredPosition;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                float d = 1f - t / dur;
                vesselRect.anchoredPosition = origin + new Vector2(
                    Random.Range(-mag, mag) * d,
                    Random.Range(-mag, mag) * d);
                yield return null;
            }
            vesselRect.anchoredPosition = origin;
        }

        // ── Liquid color transition ─────────────────────────────
        private IEnumerator LiquidColor(Color target, float dur)
        {
            Color from = liquidImage.color;
            float t = 0;
            while (t < dur) { t += Time.deltaTime; liquidImage.color = Color.Lerp(from, target, t/dur); yield return null; }
            liquidImage.color = target;
        }

        // ── Pulsing glow (heat / ambient) ───────────────────────
        private IEnumerator HeatGlow(float tempDelta, float dur)
        {
            Color c = tempDelta >= 0
                ? new Color(1f, 0.50f, 0.12f, 0.55f)   // exothermic orange
                : new Color(0.30f, 0.65f, 1f, 0.55f);   // endothermic blue
            float intensity = Mathf.Clamp(Mathf.Abs(tempDelta) / 30f, 0.3f, 1f);
            c.a *= intensity;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4.5f);
                float fadeOut = 1f - Mathf.Clamp01((t - dur * 0.7f) / (dur * 0.3f));
                glowImage.color = new Color(c.r, c.g, c.b, c.a * pulse * fadeOut);
                yield return null;
            }
            glowImage.color = Transparent;
        }

        // ── Bubbles (gas evolution) ─────────────────────────────
        private IEnumerator Bubbles(float dur)
        {
            float liquidH = VH * LIQUID_RATIO;
            int waves = 3, perWave = BUBBLES / waves;
            for (int w = 0; w < waves; w++)
            {
                for (int i = 0; i < perWave && w * perWave + i < BUBBLES; i++)
                {
                    StartCoroutine(AnimBubble(bubbles[w * perWave + i], liquidH));
                    yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
                }
                yield return new WaitForSeconds(dur / waves * 0.4f);
            }
        }

        private IEnumerator AnimBubble(Image img, float liquidH)
        {
            img.enabled = true;
            float sz = Random.Range(6f, 14f);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(sz, sz);

            float x = Random.Range(12f, VW - 12f);
            float yStart = Random.Range(8f, liquidH * 0.25f);
            float yEnd   = liquidH * Random.Range(0.88f, 1f);
            float wobF = Random.Range(2.5f, 5f);
            float wobA = Random.Range(6f, 16f);
            float life = Random.Range(1.2f, 2.2f);

            float t = 0;
            while (t < life)
            {
                t += Time.deltaTime;
                float p = t / life;
                float y = Mathf.Lerp(yStart, yEnd, p);
                float wx = x + Mathf.Sin(t * wobF) * wobA;
                rt.anchoredPosition = new Vector2(wx, y);
                img.color = new Color(1, 1, 1, 0.72f * (1f - p * 0.4f));
                float s = sz * (1f + p * 0.35f);
                rt.sizeDelta = new Vector2(s, s);
                yield return null;
            }
            img.enabled = false;
        }

        // ── Precipitate (settling solids) ───────────────────────
        private IEnumerator Precipitate(float dur)
        {
            float liquidH = VH * LIQUID_RATIO;
            for (int i = 0; i < PRECIPITATE; i++)
            {
                StartCoroutine(AnimPrecip(precipitates[i], liquidH));
                yield return new WaitForSeconds(Random.Range(0.06f, 0.18f));
            }
        }

        private IEnumerator AnimPrecip(Image img, float liquidH)
        {
            img.enabled = true;
            float sz = Random.Range(4f, 9f);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(sz, sz);

            float x = Random.Range(16f, VW - 16f);
            float yStart = liquidH * Random.Range(0.55f, 0.92f);
            float yEnd   = Random.Range(6f, 22f);
            float speed  = Random.Range(1.8f, 3.2f);
            float wobble = Random.Range(1f, 3f);

            float t = 0;
            while (t < speed)
            {
                t += Time.deltaTime;
                float p = t / speed;
                float y = Mathf.Lerp(yStart, yEnd, p * p);          // accelerate
                float wx = x + Mathf.Sin(t * wobble) * 3.5f;
                rt.anchoredPosition = new Vector2(wx, y);
                img.color = new Color(.92f, .90f, .85f, .88f * (1f - p * 0.25f));
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);                   // settle
            img.enabled = false;
        }

        // ── Sparks (energetic burst) ────────────────────────────
        private IEnumerator SparkBurst(float dur)
        {
            for (int i = 0; i < SPARKS; i++)
            {
                StartCoroutine(AnimSpark(sparks[i]));
                yield return new WaitForSeconds(Random.Range(0.02f, 0.07f));
            }
        }

        private IEnumerator AnimSpark(Image img)
        {
            img.enabled = true;
            float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float spd = Random.Range(220f, 520f);
            float life = Random.Range(0.3f, 0.65f);
            Vector2 vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
            Vector2 pos = new Vector2(VW * 0.5f, VH * 0.45f);     // beaker center
            float sz = Random.Range(3f, 7f);
            img.rectTransform.sizeDelta = new Vector2(sz, sz);

            float t = 0;
            while (t < life)
            {
                t += Time.deltaTime;
                vel.y -= 350f * Time.deltaTime;                     // gravity
                pos += vel * Time.deltaTime;
                img.rectTransform.anchoredPosition = pos;
                img.color = new Color(1, .85f + Random.Range(-.08f,.08f), .3f, 1f - t/life);
                yield return null;
            }
            img.enabled = false;
        }

        // ── Smoke (rising wisps) ────────────────────────────────
        private IEnumerator Smoke(float dur)
        {
            for (int i = 0; i < SMOKE; i++)
            {
                StartCoroutine(AnimSmoke(smokes[i]));
                yield return new WaitForSeconds(Random.Range(0.15f, 0.40f));
            }
        }

        private IEnumerator AnimSmoke(Image img)
        {
            img.enabled = true;
            float startX = VW * 0.5f + Random.Range(-VW * 0.28f, VW * 0.28f);
            float startY = VH + 8f;
            float endY   = startY + Random.Range(90f, 200f);
            float sz     = Random.Range(32f, 58f);
            float life   = Random.Range(2.2f, 3.8f);
            float drift  = Random.Range(-22f, 22f);

            float t = 0;
            while (t < life)
            {
                t += Time.deltaTime;
                float p = t / life;
                float y = Mathf.Lerp(startY, endY, p);
                float x = startX + drift * p + Mathf.Sin(t * 1.3f) * 10f;
                float s = sz * (1f + p * 0.9f);
                img.rectTransform.anchoredPosition = new Vector2(x, y);
                img.rectTransform.sizeDelta = new Vector2(s, s);
                img.color = new Color(.48f, .48f, .50f, .42f * (1f - p));
                yield return null;
            }
            img.enabled = false;
        }

        // ── Foam (surface accumulation) ─────────────────────────
        private IEnumerator Foam(float dur)
        {
            float foamY = VH * LIQUID_RATIO;

            // Fade strip in
            float t = 0;
            while (t < 0.4f) { t += Time.deltaTime; foamStrip.color = new Color(1,1,1, .55f * t/.4f); yield return null; }

            // Place dots
            for (int i = 0; i < FOAM_DOTS; i++)
            {
                var fd = foamDots[i];
                fd.enabled = true;
                float x = Random.Range(8f, VW - 8f);
                float y = foamY + Random.Range(-8f, 16f);
                float sz = Random.Range(7f, 13f);
                fd.rectTransform.anchoredPosition = new Vector2(x, y);
                fd.rectTransform.sizeDelta = new Vector2(sz, sz);
                fd.color = new Color(1, 1, 1, Random.Range(.55f, .88f));
            }

            // Gentle wobble
            float elapsed = 0;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < FOAM_DOTS; i++)
                {
                    if (!foamDots[i].enabled) continue;
                    var pos = foamDots[i].rectTransform.anchoredPosition;
                    pos.x += Mathf.Sin(elapsed * 2.2f + i) * 0.35f;
                    pos.y += Mathf.Sin(elapsed * 1.6f + i * 0.7f) * 0.22f;
                    foamDots[i].rectTransform.anchoredPosition = pos;
                }
                yield return null;
            }

            // Fade out
            t = 0;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                float a = 1f - t / .7f;
                foamStrip.color = new Color(1, 1, 1, .55f * a);
                for (int i = 0; i < FOAM_DOTS; i++)
                    foamDots[i].color = new Color(1, 1, 1, foamDots[i].color.a * (1f - Time.deltaTime * 2f));
                yield return null;
            }
            PoolOff(foamDots);
        }

        // ── Frost (icy overlay) ─────────────────────────────────
        private IEnumerator Frost(float dur)
        {
            float fadeIn = 0.7f, hold = dur - fadeIn - 0.5f;
            float t = 0;
            while (t < fadeIn) { t += Time.deltaTime; frostImage.color = new Color(.7f,.9f,1f, .32f * t/fadeIn); yield return null; }
            t = 0;
            while (t < hold) { t += Time.deltaTime; frostImage.color = new Color(.7f,.9f,1f, .28f + .04f * Mathf.Sin(t*3f)); yield return null; }
            t = 0;
            Color fc = frostImage.color;
            while (t < 0.5f) { t += Time.deltaTime; frostImage.color = new Color(.7f,.9f,1f, fc.a * (1f-t/.5f)); yield return null; }
            frostImage.color = new Color(.7f, .9f, 1f, 0f);
        }

        // ── Fade vessel in/out ──────────────────────────────────
        private IEnumerator Fade(float from, float to, float dur)
        {
            float t = 0;
            while (t < dur) { t += Time.deltaTime; vesselGroup.alpha = Mathf.Lerp(from, to, t/dur); yield return null; }
            vesselGroup.alpha = to;
        }

        // ════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════

        private void ResetAll()
        {
            PoolOff(bubbles); PoolOff(precipitates); PoolOff(sparks);
            PoolOff(smokes); PoolOff(foamDots);
        }

        private static void PoolOff(Image[] pool)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++) if (pool[i]) pool[i].enabled = false;
        }

        // ── UI Factory ──────────────────────────────────────────

        private static RectTransform MakeRT(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private Image Img(string n, RectTransform parent, Color c, Sprite spr = null)
        {
            var rt = MakeRT(n, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            if (spr) img.sprite = spr;
            return img;
        }

        private Image[] Pool(string pfx, RectTransform parent, int count, Sprite spr, Color c, float sz)
        {
            var arr = new Image[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = Img($"{pfx}{i}", parent, c, spr);
                var rt = arr[i].rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;   // anchor at bottom-left
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(sz, sz);
                arr[i].enabled = false;
            }
            return arr;
        }

        private void Bar(string n, RectTransform parent, Vector2 ancMin, Vector2 ancMax, Color c, float thickness, float overshoot)
        {
            var img = Img(n, parent, c);
            var rt = img.rectTransform;
            rt.anchorMin = ancMin; rt.anchorMax = ancMax;
            bool horiz = Mathf.Abs(ancMax.x - ancMin.x) > 0.01f;
            if (horiz)
            {
                rt.offsetMin = new Vector2(-overshoot, -thickness * 0.5f);
                rt.offsetMax = new Vector2(overshoot, thickness * 0.5f);
            }
            else
            {
                rt.offsetMin = new Vector2(-thickness * 0.5f, -overshoot);
                rt.offsetMax = new Vector2(thickness * 0.5f, overshoot);
            }
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float c = size * 0.5f, r = c - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((r - d) * 1.5f)));
            }
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
