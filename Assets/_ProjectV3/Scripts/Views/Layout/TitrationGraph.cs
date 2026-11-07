// ChemLabSim v3 — pH-versus-volume plot for the titration panel
// Draws, in its own rect: a frame with pH 7 and pH 0/14 guides, the model curve (muted), the
// equivalence volume (dashed), and the points the student has added (bright, joined).
// Display only: every point comes from TitrationModel.

using System.Collections.Generic;
using ChemLabSimV3.Engine.Chemistry;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Layout
{
    public class TitrationGraph : MaskableGraphic
    {
        private static readonly Color Frame = new Color(0.35f, 0.45f, 0.60f, 0.8f);
        private static readonly Color Guide = new Color(0.35f, 0.45f, 0.60f, 0.35f);
        private static readonly Color ModelLine = new Color(0.55f, 0.65f, 0.80f, 0.55f);
        private static readonly Color Taken = new Color(0.35f, 0.95f, 0.60f, 1f);
        private static readonly Color Equivalence = new Color(1.00f, 0.74f, 0.28f, 0.9f);

        private readonly List<TitrationPoint> model = new List<TitrationPoint>();
        private readonly List<TitrationPoint> taken = new List<TitrationPoint>();
        private double maxMl = 1, eqMl = double.NaN;

        public double MaxMl => maxMl;

        public void SetModel(List<TitrationPoint> curve, double rangeMl, double equivalenceMl)
        {
            model.Clear();
            if (curve != null) model.AddRange(curve);
            maxMl = rangeMl > 0 ? rangeMl : 1;
            eqMl = equivalenceMl;
            taken.Clear();
            SetVerticesDirty();
        }

        public void SetTaken(List<TitrationPoint> points)
        {
            taken.Clear();
            if (points != null) taken.AddRange(points);
            SetVerticesDirty();
        }

        public void Clear()
        {
            model.Clear();
            taken.Clear();
            eqMl = double.NaN;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;

            Line(vh, P(r, 0, 0), P(r, maxMl, 0), 1.5f, Frame);
            Line(vh, P(r, 0, 0), P(r, 0, 14), 1.5f, Frame);
            Line(vh, P(r, 0, 14), P(r, maxMl, 14), 1f, Guide);
            Line(vh, P(r, 0, 7), P(r, maxMl, 7), 1f, Guide);

            if (!double.IsNaN(eqMl) && eqMl <= maxMl)
                for (int i = 0; i < 14; i += 2)
                    Line(vh, P(r, eqMl, i), P(r, eqMl, i + 1), 1.5f, Equivalence);

            for (int i = 1; i < model.Count; i++)
                Line(vh, P(r, model[i - 1].AddedMl, model[i - 1].Ph), P(r, model[i].AddedMl, model[i].Ph), 2f, ModelLine);

            for (int i = 1; i < taken.Count; i++)
                Line(vh, P(r, taken[i - 1].AddedMl, taken[i - 1].Ph), P(r, taken[i].AddedMl, taken[i].Ph), 3f, Taken);
            foreach (var p in taken)
                Dot(vh, P(r, p.AddedMl, p.Ph), 4f, Taken);
        }

        private Vector2 P(Rect r, double ml, double ph)
        {
            float x = r.xMin + r.width * Mathf.Clamp01((float)(ml / maxMl));
            float y = r.yMin + r.height * Mathf.Clamp01((float)(ph / 14.0));
            return new Vector2(x, y);
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            Quad(vh, a - n, a + n, b + n, b - n, c);
        }

        private static void Dot(VertexHelper vh, Vector2 p, float size, Color c)
        {
            var h = new Vector2(size, 0); var v = new Vector2(0, size);
            Quad(vh, p - h, p + v, p + h, p - v, c);
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector2.zero);
            vh.AddVert(b, col, Vector2.zero);
            vh.AddVert(c, col, Vector2.zero);
            vh.AddVert(d, col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
