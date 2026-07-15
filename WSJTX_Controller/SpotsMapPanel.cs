using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WSJTX_Controller
{
    /// <summary>
    /// Custom double-buffered panel that renders an azimuthal equidistant spots map.
    /// </summary>
    public class SpotsMapPanel : Panel
    {
        // Data inputs (set by SpotsMapForm)
        public double CenterLat { get; set; }
        public double CenterLon { get; set; }
        public List<Spot> Spots { get; set; }
        public string TitleText { get; set; } = "";
        public bool ShowCallsignLabels { get; set; }
        public bool ShowDistanceRings { get; set; } = false;
        public bool MetricUnits { get; set; } = true;

        // Cached coastline data
        private List<List<PointF>> coastlines;

        // Layout constants
        private const int TitleHeight = 24;
        private const int LegendHeight = 40;
        private const int MapMargin = 18;

        // Colors
        private static readonly Color BgColor = Color.FromArgb(16, 16, 24);
        private static readonly Color CoastColor = Color.FromArgb(60, 80, 60);
        private static readonly Color GridColor = Color.FromArgb(70, 70, 90);
        private static readonly Color RingLabelColor = Color.FromArgb(140, 140, 160);
        private static readonly Color CenterColor = Color.FromArgb(230, 230, 240);
        private static readonly Color TitleColor = Color.FromArgb(220, 220, 235);
        private static readonly Color NoSpotsColor = Color.FromArgb(150, 150, 170);
        private static readonly Color SpotOutlineColor = Color.FromArgb(180, 0, 0, 0);

        // Tooltip
        private ToolTip toolTip;
        private string lastTooltipText = "";

        public SpotsMapPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            toolTip = new ToolTip();
            toolTip.InitialDelay = 0;
            toolTip.ReshowDelay = 0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            // Clear background
            g.Clear(BgColor);

            // Compute layout
            int mapTop = TitleHeight;
            int mapBottom = Height - LegendHeight;
            int mapHeight = mapBottom - mapTop;
            int mapWidth = Width - 2 * MapMargin;
            int mapSize = Math.Min(mapWidth, mapHeight);
            if (mapSize < 20) return;

            float cx = Width / 2f;
            float cy = mapTop + mapHeight / 2f;
            float radiusPixels = mapSize / 2f - 4;

            // Compute auto-scaling radius
            double radiusKm = ComputeRadius();

            // Draw in order
            DrawCoastlines(g, cx, cy, radiusPixels, radiusKm);
            DrawGrid(g, cx, cy, radiusPixels, radiusKm);
            DrawCenterMarker(g, cx, cy);
            DrawSpots(g, cx, cy, radiusPixels, radiusKm);
            DrawTitle(g);
            DrawSnrLegend(g);

            if (Spots == null || Spots.Count == 0)
            {
                DrawNoSpotsMessage(g, cx, cy);
            }
        }

        private double ComputeRadius()
        {
            double maxDist = 0;
            if (Spots != null)
            {
                foreach (var spot in Spots)
                {
                    if (spot.DistanceKm > maxDist) maxDist = spot.DistanceKm;
                }
            }

            if (maxDist < 500) maxDist = 500;

            // Round up to a clean number with ~5% headroom
            double padded = maxDist * 1.05;
            double step;
            if (padded <= 2000) step = 100;
            else if (padded <= 5000) step = 250;
            else if (padded <= 10000) step = 500;
            else step = 1000;

            return Math.Ceiling(padded / step) * step;
        }

        // --- Coastlines ---

        private void EnsureCoastlines()
        {
            if (coastlines == null)
                coastlines = CoastlineData.GetPolylines();
        }

        private void DrawCoastlines(Graphics g, float cx, float cy, float radiusPixels, double radiusKm)
        {
            EnsureCoastlines();
            double clipRadius = radiusKm * 1.15;

            using (Pen pen = new Pen(CoastColor, 1f))
            {
                foreach (var polyline in coastlines)
                {
                    for (int i = 0; i < polyline.Count - 1; i++)
                    {
                        double x1, y1, x2, y2;
                        GeoUtils.AzimuthalEquidistantProject(CenterLat, CenterLon, polyline[i].X, polyline[i].Y, out x1, out y1);
                        GeoUtils.AzimuthalEquidistantProject(CenterLat, CenterLon, polyline[i + 1].X, polyline[i + 1].Y, out x2, out y2);

                        double dist1 = Math.Sqrt(x1 * x1 + y1 * y1);
                        double dist2 = Math.Sqrt(x2 * x2 + y2 * y2);
                        if (dist1 > clipRadius && dist2 > clipRadius) continue;

                        float px1 = cx + (float)(x1 / radiusKm * radiusPixels);
                        float py1 = cy - (float)(y1 / radiusKm * radiusPixels);
                        float px2 = cx + (float)(x2 / radiusKm * radiusPixels);
                        float py2 = cy - (float)(y2 / radiusKm * radiusPixels);

                        g.DrawLine(pen, px1, py1, px2, py2);
                    }
                }
            }

            // Draw circular clip boundary
            using (Pen pen = new Pen(GridColor, 1.5f))
            {
                g.DrawEllipse(pen, cx - radiusPixels, cy - radiusPixels, radiusPixels * 2, radiusPixels * 2);
            }
        }

        // --- Azimuth Grid & Distance Rings ---

        private void DrawGrid(Graphics g, float cx, float cy, float radiusPixels, double radiusKm)
        {
            using (Pen pen = new Pen(GridColor, 0.5f))
            using (Font font = new Font("Microsoft Sans Serif", 8f))
            using (Brush brush = new SolidBrush(RingLabelColor))
            {
                // 30-degree azimuth ticks
                for (int deg = 0; deg < 360; deg += 30)
                {
                    double rad = deg * Math.PI / 180.0;
                    float tx = cx + (float)(Math.Sin(rad) * radiusPixels);
                    float ty = cy - (float)(Math.Cos(rad) * radiusPixels);
                    float ix = cx + (float)(Math.Sin(rad) * (radiusPixels - 6));
                    float iy = cy - (float)(Math.Cos(rad) * (radiusPixels - 6));
                    g.DrawLine(pen, ix, iy, tx, ty);
                }

                // Cardinal direction labels
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                float offset = radiusPixels + 12;
                g.DrawString("N", font, brush, cx, cy - offset, sf);
                g.DrawString("S", font, brush, cx, cy + offset, sf);
                g.DrawString("E", font, brush, cx + offset, cy, sf);
                g.DrawString("W", font, brush, cx - offset, cy, sf);

                // Distance rings
                if (ShowDistanceRings)
                {
                    pen.DashStyle = DashStyle.Dot;
                    for (int i = 1; i <= 4; i++)
                    {
                        float frac = i / 4f;
                        float r = radiusPixels * frac;
                        g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);

                        // Label
                        double distKm = radiusKm * frac;
                        string units = MetricUnits ? "km" : "mi";
                        int distLabel = MetricUnits ? (int)distKm : (int)(distKm * 0.6213 + 0.5);
                        string label = distLabel >= 1000 ? $"{distLabel / 1000}k {units}" : $"{distLabel} {units}";
                        g.DrawString(label, font, brush, cx + r + 2, cy, sf);
                    }
                }
            }
        }

        // --- Center Marker ---

        private void DrawCenterMarker(Graphics g, float cx, float cy)
        {
            using (Brush brush = new SolidBrush(CenterColor))
            {
                g.FillEllipse(brush, cx - 3, cy - 3, 6, 6);
            }
        }

        // --- Spots ---

        private void DrawSpots(Graphics g, float cx, float cy, float radiusPixels, double radiusKm)
        {
            if (Spots == null || Spots.Count == 0) return;

            // Sort oldest first so newest draws on top
            var sorted = new List<Spot>(Spots);
            sorted.Sort((a, b) => a.TimestampUtc.CompareTo(b.TimestampUtc));

            using (Font font = new Font("Microsoft Sans Serif", 7f))
            {
                foreach (var spot in sorted)
                {
                    double xKm, yKm;
                    GeoUtils.AzimuthalEquidistantProject(CenterLat, CenterLon, spot.LatDeg, spot.LonDeg, out xKm, out yKm);

                    double distFromCenter = Math.Sqrt(xKm * xKm + yKm * yKm);
                    if (distFromCenter > radiusKm * 1.05) continue;

                    float px = cx + (float)(xKm / radiusKm * radiusPixels);
                    float py = cy - (float)(yKm / radiusKm * radiusPixels);

                    // Age-based alpha: 255 at 0 min, 128 at 15 min
                    double ageMin = (DateTime.UtcNow - spot.TimestampUtc).TotalMinutes;
                    if (ageMin < 0) ageMin = 0;
                    int alpha = (int)(255 - (ageMin / SpotCache.MaxAgeMinutes) * 127);
                    if (alpha < 64) alpha = 64;
                    if (alpha > 255) alpha = 255;

                    // SNR-based color
                    Color spotColor = SnrToColor(spot.SnrDb, alpha);

                    // Draw spot dot
                    using (Brush brush = new SolidBrush(spotColor))
                    using (Pen outlinePen = new Pen(SpotOutlineColor, 0.5f))
                    {
                        g.FillEllipse(brush, px - 3, py - 3, 6, 6);
                        g.DrawEllipse(outlinePen, px - 3, py - 3, 6, 6);
                    }

                    // Callsign labels
                    if (ShowCallsignLabels)
                    {
                        SizeF textSize = g.MeasureString(spot.ReceiverCall, font);
                        float tx = px - textSize.Width / 2;
                        float ty = py - 3 - textSize.Height - 1;
                        using (Brush bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                        using (Brush textBrush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                        {
                            g.FillRectangle(bgBrush, tx - 1, ty, textSize.Width + 2, textSize.Height);
                            g.DrawString(spot.ReceiverCall, font, textBrush, tx, ty);
                        }
                    }
                }
            }
        }

        // --- SNR to Color ---

        private static Color SnrToColor(int snrDb, int alpha)
        {
            double hue = SnrToHue(snrDb);
            return HsvToRgb(hue, 1.0, 1.0, alpha);
        }

        private static double SnrToHue(int snrDb)
        {
            if (snrDb <= -25) return 240;
            if (snrDb >= 10) return 0;
            if (snrDb <= 0)
            {
                // -25 to 0 maps 240 to 60
                double t = (snrDb + 25.0) / 25.0;
                return 240 - t * 180;
            }
            else
            {
                // 0 to 10 maps 60 to 0
                double t = snrDb / 10.0;
                return 60 - t * 60;
            }
        }

        private static Color HsvToRgb(double hue, double sat, double val, int alpha)
        {
            hue = hue % 360;
            if (hue < 0) hue += 360;
            double c = val * sat;
            double x = c * (1 - Math.Abs((hue / 60.0) % 2 - 1));
            double m = val - c;

            double r, g, b;
            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromArgb(alpha,
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255));
        }

        // --- Title Strip ---

        private void DrawTitle(Graphics g)
        {
            using (Font font = new Font("Microsoft Sans Serif", 9f))
            using (Brush brush = new SolidBrush(TitleColor))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                RectangleF titleRect = new RectangleF(0, 0, Width, TitleHeight);
                g.DrawString(TitleText, font, brush, titleRect, sf);
            }
        }

        // --- No-Spots Message ---

        private void DrawNoSpotsMessage(Graphics g, float cx, float cy)
        {
            using (Font font = new Font("Microsoft Sans Serif", 10f))
            using (Brush brush = new SolidBrush(NoSpotsColor))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("No new spots yet", font, brush, cx, cy + 30, sf);
            }
        }

        // --- SNR Legend ---

        private void DrawSnrLegend(Graphics g)
        {
            float legendTop = Height - LegendHeight + 4;
            float legendLeft = MapMargin + 40;
            float legendRight = Width - MapMargin - 40;
            float legendWidth = legendRight - legendLeft;
            float barHeight = 12;
            float barTop = legendTop + 4;

            using (Font font = new Font("Microsoft Sans Serif", 7f))
            using (Brush textBrush = new SolidBrush(RingLabelColor))
            {
                // Draw gradient bar
                for (int px = 0; px < (int)legendWidth; px++)
                {
                    float t = px / legendWidth;
                    int snr = (int)(-25 + t * 35); // -25 to +10
                    Color c = SnrToColor(snr, 255);
                    using (Pen pen = new Pen(c))
                    {
                        g.DrawLine(pen, legendLeft + px, barTop, legendLeft + px, barTop + barHeight);
                    }
                }

                // Border
                using (Pen pen = new Pen(GridColor))
                {
                    g.DrawRectangle(pen, legendLeft, barTop, legendWidth, barHeight);
                }

                // Labels
                StringFormat sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
                StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                StringFormat sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };

                float labelY = barTop + barHeight + 2;
                g.DrawString("-25 dB", font, textBrush, legendLeft, labelY, sfLeft);
                g.DrawString("SNR (dB)", font, textBrush, legendLeft + legendWidth / 2, labelY, sfCenter);
                g.DrawString("+10 dB", font, textBrush, legendRight, labelY, sfRight);
            }
        }

        // --- Hover Tooltips ---

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (Spots == null || Spots.Count == 0)
            {
                ClearTooltip();
                return;
            }

            int mapTop = TitleHeight;
            int mapBottom = Height - LegendHeight;
            int mapHeight = mapBottom - mapTop;
            int mapWidth = Width - 2 * MapMargin;
            int mapSize = Math.Min(mapWidth, mapHeight);
            if (mapSize < 20) { ClearTooltip(); return; }

            float cx = Width / 2f;
            float cy = mapTop + mapHeight / 2f;
            float radiusPixels = mapSize / 2f - 4;
            double radiusKm = ComputeRadius();

            float closestDist = 8f; // max pixel distance
            Spot closestSpot = null;

            foreach (var spot in Spots)
            {
                double xKm, yKm;
                GeoUtils.AzimuthalEquidistantProject(CenterLat, CenterLon, spot.LatDeg, spot.LonDeg, out xKm, out yKm);

                float px = cx + (float)(xKm / radiusKm * radiusPixels);
                float py = cy - (float)(yKm / radiusKm * radiusPixels);

                float dx = e.X - px;
                float dy = e.Y - py;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestSpot = spot;
                }
            }

            if (closestSpot != null)
            {
                double ageMin = (DateTime.UtcNow - closestSpot.TimestampUtc).TotalMinutes;
                string units = MetricUnits ? "km" : "mi";
                double distVal = MetricUnits ? closestSpot.DistanceKm : closestSpot.DistanceKm * 0.6213;
                string distStr = $"{(int)(distVal + 0.5)} {units}";
                string tipText = $"{closestSpot.ReceiverCall} ({closestSpot.ReceiverGrid})\n{closestSpot.SnrDb} dB \u00B7 {distStr} \u00B7 {ageMin:F0} min ago";

                if (tipText != lastTooltipText)
                {
                    toolTip.SetToolTip(this, tipText);
                    lastTooltipText = tipText;
                }
            }
            else
            {
                ClearTooltip();
            }
        }

        private void ClearTooltip()
        {
            if (lastTooltipText != "")
            {
                toolTip.SetToolTip(this, "");
                lastTooltipText = "";
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            ClearTooltip();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
