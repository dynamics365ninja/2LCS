using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace LCS.Theming
{
    public class ToggleSwitch : CheckBox
    {
        private static readonly PropertyInfo ShowFocusCuesProperty =
            typeof(Control).GetProperty("ShowFocusCues", BindingFlags.Instance | BindingFlags.NonPublic);

        private bool _hover;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            AutoSize = false;
            Size = new Size(96, 28);
            Cursor = Cursors.Hand;
        }

        [DefaultValue(false)]
        public override bool AutoSize
        {
            get => base.AutoSize;
            set => base.AutoSize = value;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            AccessibleName = Text;
        }

        protected override void OnMouseEnter(EventArgs eventargs)
        {
            base.OnMouseEnter(eventargs);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs eventargs)
        {
            base.OnMouseLeave(eventargs);
            _hover = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            var p = Theme.Current;
            var s = DeviceDpi / 96f;
            g.Clear(Parent?.BackColor.A == 255 ? Parent.BackColor : p.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var track = new RectangleF(Width - 42 * s, (Height - 20 * s) / 2, 40 * s, 20 * s);
            var knob = (_hover && Enabled ? 14 : 12) * s;
            Color stroke, fill, knobColor;
            if (!Enabled)
            {
                stroke = p.TextDisabled;
                fill = Checked ? p.TextDisabled : Color.Transparent;
                knobColor = Checked ? p.Surface : p.TextDisabled;
            }
            else if (Checked)
            {
                fill = _hover ? p.AccentHover : p.Accent;
                stroke = fill;
                knobColor = p.OnAccent;
            }
            else
            {
                fill = _hover ? p.HoverOverlay : Color.Transparent;
                stroke = p.TextSecondary;
                knobColor = p.TextSecondary;
            }

            if (fill.A > 0) Theme.FillRounded(g, fill, track, track.Height / 2);
            Theme.DrawRounded(g, stroke, track, track.Height / 2);
            var knobX = Checked ? track.Right - 4 * s - knob - (14 * s - knob) / 2 : track.X + 4 * s + (14 * s - knob) / 2;
            using (var brush = new SolidBrush(knobColor))
            {
                g.FillEllipse(brush, knobX, track.Y + (track.Height - knob) / 2, knob, knob);
            }

            if (Focused && ShowFocusCuesProperty?.GetValue(this) is bool cues && cues)
            {
                var focus = RectangleF.Inflate(track, 3 * s, 3 * s);
                Theme.DrawRounded(g, p.Text, focus, focus.Height / 2, 1.5f * s);
            }

            var state = Checked ? "On" : "Off";
            var textBounds = new Rectangle(0, 0, (int)(track.X - 10 * s), Height);
            TextRenderer.DrawText(g, state, Font, textBounds, Enabled ? p.Text : p.TextDisabled,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
