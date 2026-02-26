using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WSJTX_Controller
{
    public static class DarkMode
    {
        public static bool Enabled { get; set; } = false;

        // Track which controls have border helpers attached
        private static Dictionary<Control, BorderNativeWindow> borderHelpers = new Dictionary<Control, BorderNativeWindow>();

        // Track which comboboxes have owner-draw handlers attached
        private static HashSet<ComboBox> ownerDrawCombos = new HashSet<ComboBox>();

        // Track which controls have persistent HandleCreated listeners for border reattachment
        private static HashSet<Control> persistentBorderControls = new HashSet<Control>();

        // Dark mode palette
        public static readonly Color DarkBackground = Color.FromArgb(30, 30, 30);
        public static readonly Color DarkControlBackground = Color.FromArgb(45, 45, 48);
        public static readonly Color DarkFieldBackground = Color.FromArgb(51, 51, 55);
        public static readonly Color DarkForeground = Color.FromArgb(220, 220, 220);
        public static readonly Color DarkBorder = Color.FromArgb(67, 67, 70);
        public static readonly Color DarkHelpLinkColor = Color.FromArgb(100, 180, 255);
        public static readonly Color DarkVersionLinkColor = Color.FromArgb(100, 180, 255);
        public static readonly Color DarkGrayText = Color.FromArgb(140, 140, 140);
        public static readonly Color DarkGroupBoxFore = Color.FromArgb(200, 200, 200);
        public static readonly Color DarkButtonBackground = Color.FromArgb(60, 60, 64);
        public static readonly Color DarkButtonForeground = Color.FromArgb(220, 220, 220);
        public static readonly Color DarkListBoxBackground = Color.FromArgb(37, 37, 38);
        public static readonly Color DarkControlBorder = Color.FromArgb(80, 80, 80);

        // Light mode palette (defaults)
        public static readonly Color LightBackground = SystemColors.Control;
        public static readonly Color LightControlBackground = SystemColors.Control;
        public static readonly Color LightFieldBackground = SystemColors.Window;
        public static readonly Color LightForeground = SystemColors.ControlText;
        public static readonly Color LightHelpLinkColor = Color.Blue;
        public static readonly Color LightVersionLinkColor = Color.Blue;
        public static readonly Color LightGrayText = Color.Gray;

        public static Color FormBackground => Enabled ? DarkBackground : LightBackground;
        public static Color ControlBackground => Enabled ? DarkControlBackground : LightControlBackground;
        public static Color FieldBackground => Enabled ? DarkFieldBackground : LightFieldBackground;
        public static Color Foreground => Enabled ? DarkForeground : LightForeground;
        public static Color HelpLinkColor => Enabled ? DarkHelpLinkColor : LightHelpLinkColor;
        public static Color GrayText => Enabled ? DarkGrayText : LightGrayText;
        public static Color ButtonBack => Enabled ? DarkButtonBackground : SystemColors.Control;
        public static Color ButtonFore => Enabled ? DarkButtonForeground : SystemColors.ControlText;
        public static Color ListBoxBack => Enabled ? DarkListBoxBackground : SystemColors.Window;
        public static Color GroupBoxFore => Enabled ? DarkGroupBoxFore : SystemColors.ControlText;

        /// <summary>
        /// The "normal" foreground for labels that get reset after debug highlight.
        /// In light mode this is Black; in dark mode use light text.
        /// </summary>
        public static Color NormalLabelFore => Enabled ? DarkForeground : Color.Black;

        /// <summary>
        /// Color for pending checkbox text (skip grid, use RR73).
        /// </summary>
        public static Color PendingColor => Enabled ? Color.LightGreen : Color.DarkGreen;

        /// <summary>
        /// Color used for confirmed (non-pending) checkbox text.
        /// </summary>
        public static Color ConfirmedCheckboxColor => NormalLabelFore;

        public static void ApplyToForm(Form form)
        {
            form.BackColor = FormBackground;
            form.ForeColor = Foreground;
            ApplyToControls(form.Controls);
        }

        public static void ApplyToControls(Control.ControlCollection controls)
        {
            foreach (Control ctrl in controls)
            {
                ApplyToControl(ctrl);
            }
        }

        private static bool NeedsBorder(Control ctrl)
        {
            return ctrl is TextBox || ctrl is ListBox || ctrl is NumericUpDown || ctrl is ComboBox;
        }

        /// <summary>
        /// Attach a NativeWindow border helper to a control, or refresh an existing one.
        /// For controls that recreate their handles (e.g. ComboBox when FlatStyle changes),
        /// set persistent=true to auto-reattach on each HandleCreated.
        /// </summary>
        private static void AttachBorderHelper(Control ctrl, bool persistent = false)
        {
            if (persistent && !persistentBorderControls.Contains(ctrl))
            {
                persistentBorderControls.Add(ctrl);
                ctrl.HandleCreated += OnPersistentHandleCreated;
                ctrl.HandleDestroyed += OnPersistentHandleDestroyed;
            }

            if (!ctrl.IsHandleCreated)
            {
                if (!persistent)
                {
                    ctrl.HandleCreated -= OnHandleCreated;
                    ctrl.HandleCreated += OnHandleCreated;
                }
                return;
            }

            if (borderHelpers.ContainsKey(ctrl))
            {
                // Already attached to this handle, just repaint
                RedrawBorder(ctrl);
                return;
            }

            var helper = new BorderNativeWindow(ctrl);
            borderHelpers[ctrl] = helper;

            if (!persistent)
            {
                ctrl.HandleDestroyed += (s, e) =>
                {
                    if (borderHelpers.ContainsKey(ctrl))
                    {
                        borderHelpers[ctrl].ReleaseHandle();
                        borderHelpers.Remove(ctrl);
                    }
                };
            }

            // Force a non-client repaint
            RedrawBorder(ctrl);
        }

        private static void OnHandleCreated(object sender, EventArgs e)
        {
            Control ctrl = (Control)sender;
            ctrl.HandleCreated -= OnHandleCreated;
            AttachBorderHelper(ctrl);
        }

        private static void OnPersistentHandleDestroyed(object sender, EventArgs e)
        {
            Control ctrl = (Control)sender;
            if (borderHelpers.ContainsKey(ctrl))
            {
                borderHelpers[ctrl].ReleaseHandle();
                borderHelpers.Remove(ctrl);
            }
        }

        private static void OnPersistentHandleCreated(object sender, EventArgs e)
        {
            Control ctrl = (Control)sender;
            if (ctrl.IsHandleCreated && !borderHelpers.ContainsKey(ctrl))
            {
                var helper = new BorderNativeWindow(ctrl);
                borderHelpers[ctrl] = helper;
                RedrawBorder(ctrl);
            }
        }

        private static void RedrawBorder(Control ctrl)
        {
            if (ctrl.IsHandleCreated)
            {
                // Send WM_NCPAINT to force the non-client area to repaint
                SendMessage(ctrl.Handle, WM_NCPAINT, (IntPtr)1, IntPtr.Zero);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private const int WM_NCPAINT = 0x0085;
        private const int WM_PAINT = 0x000F;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        /// <summary>
        /// NativeWindow subclass that intercepts WM_NCPAINT to draw custom borders.
        /// </summary>
        private class BorderNativeWindow : NativeWindow
        {
            private Control control;
            private bool isComboBox;

            public BorderNativeWindow(Control ctrl)
            {
                control = ctrl;
                isComboBox = ctrl is ComboBox;
                AssignHandle(ctrl.Handle);
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if ((m.Msg == WM_NCPAINT || m.Msg == WM_PAINT) && Enabled)
                {
                    if (isComboBox && m.Msg == WM_PAINT)
                    {
                        // ComboBox with FlatStyle.Flat paints entirely in client area.
                        // Defer border drawing so it happens after all child painting.
                        control.BeginInvoke(new Action(DrawCustomBorder));
                    }
                    else
                    {
                        DrawCustomBorder();
                    }
                }
            }

            private void DrawCustomBorder()
            {
                if (!control.IsHandleCreated) return;

                IntPtr hdc = GetWindowDC(control.Handle);
                if (hdc == IntPtr.Zero) return;

                try
                {
                    using (Graphics g = Graphics.FromHdc(hdc))
                    {
                        RECT windowRect;
                        GetWindowRect(control.Handle, out windowRect);
                        int w = windowRect.Right - windowRect.Left;
                        int h = windowRect.Bottom - windowRect.Top;

                        using (Pen pen = new Pen(DarkControlBorder))
                        {
                            g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
                            // ComboBox has a 2px 3D border; draw inner rect to cover it
                            if (isComboBox)
                            {
                                g.DrawRectangle(pen, 1, 1, w - 3, h - 3);
                            }
                        }
                    }
                }
                finally
                {
                    ReleaseDC(control.Handle, hdc);
                }
            }
        }

        private static void ComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            if (e.Index < 0) return;

            Color backColor, foreColor;
            if (Enabled)
            {
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                backColor = isSelected ? DarkBorder : FieldBackground;
                foreColor = DarkForeground;
            }
            else
            {
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                backColor = isSelected ? SystemColors.Highlight : SystemColors.Window;
                foreColor = isSelected ? SystemColors.HighlightText : SystemColors.WindowText;
            }

            using (SolidBrush bgBrush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }

            string text = cb.Items[e.Index].ToString();
            TextRenderer.DrawText(e.Graphics, text, cb.Font, e.Bounds, foreColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }


        public static void ApplyToControl(Control ctrl)
        {
            // Skip controls whose colors are set dynamically (status bar etc.)
            if (ctrl.Tag != null && ctrl.Tag.ToString() == "no-theme") return;

            if (ctrl is GroupBox gb)
            {
                gb.ForeColor = GroupBoxFore;
                gb.BackColor = FormBackground;
                ApplyToControls(gb.Controls);
            }
            else if (ctrl is Button btn)
            {
                // Skip buttons with active highlight colors (Guide form selected state)
                bool isHighlighted = btn.BackColor == Color.Green
                    || btn.BackColor == Color.LightGreen;

                if (!isHighlighted)
                {
                    btn.FlatStyle = Enabled ? FlatStyle.Flat : FlatStyle.Standard;
                    btn.BackColor = ButtonBack;
                    btn.ForeColor = ButtonFore;
                    if (Enabled)
                    {
                        btn.FlatAppearance.BorderColor = DarkBorder;
                        btn.FlatAppearance.MouseOverBackColor = ButtonBack;
                        btn.FlatAppearance.MouseDownBackColor = ButtonBack;
                    }
                }
                else if (Enabled)
                {
                    // Highlighted buttons still need flat style in dark mode
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = DarkBorder;
                }
                else
                {
                    btn.FlatStyle = FlatStyle.Standard;
                }
            }
            else if (ctrl is TextBox tb)
            {
                tb.BackColor = FieldBackground;
                // Preserve gray placeholder text
                if (tb.ForeColor == Color.Gray || tb.ForeColor == DarkGrayText)
                {
                    tb.ForeColor = GrayText;
                }
                else
                {
                    tb.ForeColor = Foreground;
                }
                tb.BorderStyle = Enabled ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                if (Enabled) AttachBorderHelper(tb);
                else RedrawBorder(tb);
            }
            else if (ctrl is ListBox lb)
            {
                // Borderless list boxes blend with form background; bordered ones use ListBoxBack
                lb.BackColor = lb.BorderStyle == BorderStyle.None ? FormBackground : ListBoxBack;
                // Preserve gray state for disabled list boxes
                if (lb.ForeColor == Color.Gray || lb.ForeColor == DarkGrayText)
                {
                    lb.ForeColor = GrayText;
                }
                else
                {
                    lb.ForeColor = Foreground;
                }
                // Preserve BorderStyle.None for borderless list boxes (e.g. callListBox, logListBox)
                if (lb.BorderStyle != BorderStyle.None)
                {
                    lb.BorderStyle = Enabled ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    if (Enabled) AttachBorderHelper(lb);
                    else RedrawBorder(lb);
                }
            }
            else if (ctrl is ComboBox cb)
            {
                if (!ownerDrawCombos.Contains(cb))
                {
                    cb.DrawMode = DrawMode.OwnerDrawFixed;
                    cb.DrawItem += ComboBox_DrawItem;
                    ownerDrawCombos.Add(cb);
                }
                cb.BackColor = Enabled ? FieldBackground : SystemColors.Window;
                cb.ForeColor = Enabled ? Foreground : SystemColors.WindowText;
                if (Enabled) AttachBorderHelper(cb, persistent: true);
                else RedrawBorder(cb);
                cb.Invalidate();
            }
            else if (ctrl is NumericUpDown nud)
            {
                nud.BackColor = FieldBackground;
                nud.ForeColor = Foreground;
                nud.BorderStyle = Enabled ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                if (Enabled) AttachBorderHelper(nud);
                else RedrawBorder(nud);
            }
            else if (ctrl is CheckBox chk)
            {
                // Preserve special forecolors (pending green, etc.)
                if (chk.ForeColor == Color.DarkGreen || chk.ForeColor == Color.LightGreen)
                {
                    chk.ForeColor = PendingColor;
                }
                else
                {
                    chk.ForeColor = Foreground;
                }
                chk.BackColor = FormBackground;
            }
            else if (ctrl is RadioButton rb)
            {
                rb.ForeColor = Foreground;
                rb.BackColor = FormBackground;
            }
            else if (ctrl is Label lbl)
            {
                // Preserve help label blue color
                if (lbl.ForeColor == Color.Blue || lbl.ForeColor == DarkHelpLinkColor)
                {
                    lbl.ForeColor = HelpLinkColor;
                }
                // Preserve gray placeholder forecolor
                else if (lbl.ForeColor == Color.Gray || lbl.ForeColor == DarkGrayText)
                {
                    lbl.ForeColor = GrayText;
                }
                // Preserve red forecolor (debug/error highlights)
                else if (lbl.ForeColor == Color.Red)
                {
                    // keep red
                }
                // Preserve pending green
                else if (lbl.ForeColor == Color.DarkGreen || lbl.ForeColor == Color.LightGreen)
                {
                    lbl.ForeColor = PendingColor;
                }
                else
                {
                    lbl.ForeColor = Foreground;
                }

                // Don't change BackColor on labels that have special backgrounds (status)
                if (lbl.BackColor != Color.Red
                    && lbl.BackColor != Color.Orange
                    && lbl.BackColor != Color.Yellow
                    && lbl.BackColor != Color.Green
                    && lbl.BackColor != Color.LightGray)
                {
                    lbl.BackColor = FormBackground;
                }
            }
            else if (ctrl is Panel pnl)
            {
                pnl.BackColor = FormBackground;
                ApplyToControls(pnl.Controls);
            }
            else
            {
                ctrl.BackColor = FormBackground;
                ctrl.ForeColor = Foreground;
                if (ctrl.HasChildren)
                {
                    ApplyToControls(ctrl.Controls);
                }
            }
        }
    }
}
