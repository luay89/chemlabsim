// ChemLabSim v3 — small line plot for the Tests tab
// Titration: pH (0–14) versus volume — a frame with pH 7 and pH 0/14 guides, the model curve
// (muted), the equivalence volume (dashed), and the points the student has added (bright).
// Heating lab: temperature versus time (SetLine), with dashed guides at the transition
// temperatures. Display only: every point comes from the engine's models.

using System;
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
        private double yMin = 0, yMax = 14;
        private double[] yGuides = { 7 };
        private readonly List<Vector2> line = new List<Vector2>();

        public double MaxMl => maxMl;

        /// <summary>A plain line (e.g. temperature against time) with horizontal guides; replaces any titration plot.</summary>
        public void SetLine(List<Vector2> points, double xMax, double yMinValue, double yMaxValue, double[] guides)
        {
            model.Clear();
            taken.Clear();
            eqMl = double.NaN;
            line.Clear();
            if (points != null) line.AddRange(points);
            maxMl = xMax > 0 ? xMax : 1;
            yMin = yMinValue;
            yMax = yMaxValue > yMinValue ? yMaxValue : yMinValue + 1;
            yGuides = guides ?? new double[0];
            SetVerticesDirty();
        }

        public void SetModel(List<TitrationPoint> curve, double rangeMl, double equivalenceMl)
        {
            line.Clear();
            yMin = 0; yMax = 14; yGuides = new double[] { 7 };
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
            line.Clear();
            yMin = 0; yMax = 14; yGuides = new double[] { 7 };
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

            Line(vh, P(r, 0, yMin), P(r, maxMl, yMin), 1.5f, Frame);
            Line(vh, P(r, 0, yMin), P(r, 0, yMax), 1.5f, Frame);
            Line(vh, P(r, 0, yMax), P(r, maxMl, yMax), 1f, Guide);
            foreach (var g in yGuides)
            {
                if (line.Count == 0) { Line(vh, P(r, 0, g), P(r, maxMl, g), 1f, Guide); continue; }
                for (double x = 0; x < maxMl; x += maxMl / 20)   // dashed: a transition temperature
                    Line(vh, P(r, x, g), P(r, Math.Min(maxMl, x + maxMl / 40), g), 1.5f, Equivalence);
            }

            if (!double.IsNaN(eqMl) && eqMl <= maxMl)
                for (int i = 0; i < 14; i += 2)
                    Line(vh, P(r, eqMl, i), P(r, eqMl, i + 1), 1.5f, Equivalence);

            for (int i = 1; i < line.Count; i++)
                Line(vh, P(r, line[i - 1].x, line[i - 1].y), P(r, line[i].x, line[i].y), 3f, Taken);

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
            float y = r.yMin + r.height * Mathf.Clamp01((float)((ph - yMin) / (yMax - yMin)));
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
