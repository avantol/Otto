using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WSJTX_Controller
{
    public partial class SpotsMapForm : Form
    {
        private Controller ctrl;
        private PskReporterClient mqttClient;
        private SpotCache spotCache;
        private SpotsMapPanel mapPanel;
        private System.Windows.Forms.Timer repaintTimer;
        private System.Windows.Forms.Timer pruneTimer;
        private string currentBand = "";
        private string lastMode = "";
        private bool forceClose = false;

        public SpotsMapForm(Controller ctrl, PskReporterClient mqttClient, SpotCache spotCache)
        {
            this.ctrl = ctrl;
            this.mqttClient = mqttClient;
            this.spotCache = spotCache;

            InitializeFormComponents();

            // Wire up events
            mqttClient.SpotReceived += OnSpotReceived;
            mqttClient.ConnectionStateChanged += OnConnectionStateChanged;

            // Repaint timer (1 second)
            repaintTimer = new System.Windows.Forms.Timer();
            repaintTimer.Interval = 1000;
            repaintTimer.Tick += (s, e) => UpdateMap();
            repaintTimer.Start();

            // Prune timer (30 seconds)
            pruneTimer = new System.Windows.Forms.Timer();
            pruneTimer.Interval = 30000;
            pruneTimer.Tick += (s, e) => spotCache.PruneExpired();
            pruneTimer.Start();
        }

        private void InitializeFormComponents()
        {
            this.SuspendLayout();

            this.Text = "Spots Map";
            this.MinimumSize = new Size(400, 400);
            this.Size = new Size(600, 600);
            this.ShowInTaskbar = true;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(16, 16, 24);

            // Use same icon as main form
            try { this.Icon = ctrl.Icon; } catch { }

            // Map panel fills the form
            mapPanel = new SpotsMapPanel();
            mapPanel.Dock = DockStyle.Fill;
            this.Controls.Add(mapPanel);

            // Right-click context menu
            ContextMenuStrip menu = new ContextMenuStrip();
            var ringsItem = new ToolStripMenuItem("Show Distance Rings");
            ringsItem.CheckOnClick = true;
            ringsItem.Checked = false;
            ringsItem.CheckedChanged += (s, e) =>
            {
                mapPanel.ShowDistanceRings = ringsItem.Checked;
                mapPanel.Invalidate();
            };

            var callsItem = new ToolStripMenuItem("Show Callsign Labels");
            callsItem.CheckOnClick = true;
            callsItem.Checked = false;
            callsItem.CheckedChanged += (s, e) =>
            {
                mapPanel.ShowCallsignLabels = callsItem.Checked;
                mapPanel.Invalidate();
            };

            menu.Items.Add(ringsItem);
            menu.Items.Add(callsItem);
            mapPanel.ContextMenuStrip = menu;

            this.ResumeLayout(false);
        }

        public void OnBandChanged(string newBand)
        {
            currentBand = newBand;
            UpdateMap();
        }

        public void SetStation(string call, string grid)
        {
            double lat, lon;
            if (GeoUtils.GridToLatLon(grid, out lat, out lon))
            {
                mapPanel.CenterLat = lat;
                mapPanel.CenterLon = lon;
            }
        }

        public void SetBand(string band)
        {
            currentBand = band;
        }

        public bool ShowRings
        {
            get { return mapPanel.ShowDistanceRings; }
            set
            {
                mapPanel.ShowDistanceRings = value;
                // Also update context menu
                if (mapPanel.ContextMenuStrip != null && mapPanel.ContextMenuStrip.Items.Count > 0)
                {
                    var item = mapPanel.ContextMenuStrip.Items[0] as ToolStripMenuItem;
                    if (item != null) item.Checked = value;
                }
            }
        }

        public bool ShowCalls
        {
            get { return mapPanel.ShowCallsignLabels; }
            set
            {
                mapPanel.ShowCallsignLabels = value;
                if (mapPanel.ContextMenuStrip != null && mapPanel.ContextMenuStrip.Items.Count > 1)
                {
                    var item = mapPanel.ContextMenuStrip.Items[1] as ToolStripMenuItem;
                    if (item != null) item.Checked = value;
                }
            }
        }

        private void OnSpotReceived(Spot spot, string band)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => OnSpotReceived(spot, band))); } catch { }
                return;
            }

            // Compute distance and azimuth from center
            spot.DistanceKm = GeoUtils.HaversineDistanceKm(mapPanel.CenterLat, mapPanel.CenterLon, spot.LatDeg, spot.LonDeg);
            spot.AzimuthDeg = GeoUtils.InitialBearing(mapPanel.CenterLat, mapPanel.CenterLon, spot.LatDeg, spot.LonDeg);

            spotCache.AddSpot(spot, band);
        }

        private void OnConnectionStateChanged(string state)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => OnConnectionStateChanged(state))); } catch { }
                return;
            }
            UpdateMap();
        }

        private void UpdateMap()
        {
            if (mapPanel == null) return;

            string call = ctrl.wsjtxClient?.myCall ?? "";
            string grid = ctrl.wsjtxClient?.myGrid ?? "";

            // Update center if grid available
            double lat, lon;
            if (GeoUtils.GridToLatLon(grid, out lat, out lon))
            {
                mapPanel.CenterLat = lat;
                mapPanel.CenterLon = lon;
            }

            // Keep band, mode, and units in sync with controller
            string currentMode = "";
            if (ctrl.wsjtxClient != null)
            {
                string band = ctrl.wsjtxClient.CurrentBand;
                if (!string.IsNullOrEmpty(band)) currentBand = band;

                currentMode = ctrl.wsjtxClient.CurrentMode ?? "";
                if (!string.IsNullOrEmpty(currentMode) && currentMode != lastMode)
                {
                    if (!string.IsNullOrEmpty(lastMode))
                    {
                        spotCache.Clear();
                    }
                    lastMode = currentMode;
                }
                mqttClient.UpdateMode(currentMode);

                mapPanel.MetricUnits = ctrl.wsjtxClient.MetricUnits;
            }

            // Get spots for current band
            List<Spot> spots = spotCache.GetSpots(currentBand);
            mapPanel.Spots = spots;

            // Build title
            int count = spots.Count;
            string connState = mqttClient.ConnectionState;
            string bandStr = string.IsNullOrEmpty(currentBand) ? "?" : currentBand;
            string modeStr = string.IsNullOrEmpty(currentMode) ? "" : $" {currentMode}";
            mapPanel.TitleText = $"{call} @ {grid} \u2014 {bandStr}{modeStr} \u2014 {count} spotter{(count != 1 ? "s" : "")} / last 15 min \u2014 {connState}";

            mapPanel.Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!forceClose && e.CloseReason == CloseReason.UserClosing)
            {
                // Hide instead of close — MQTT keeps running
                e.Cancel = true;
                Hide();
                ctrl.SpotsMapHidden();
                return;
            }

            repaintTimer?.Stop();
            repaintTimer?.Dispose();
            pruneTimer?.Stop();
            pruneTimer?.Dispose();

            mqttClient.SpotReceived -= OnSpotReceived;
            mqttClient.ConnectionStateChanged -= OnConnectionStateChanged;

            base.OnFormClosing(e);
        }

        public void ForceClose()
        {
            forceClose = true;
            Close();
        }
    }
}
