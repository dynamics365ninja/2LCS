using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LCS.Theming
{
    public enum InfoSeverity
    {
        Informational,
        Success,
        Warning,
        Error
    }

    public sealed class InfoBar : FluentControl
    {
        private readonly Timer _autoClose = new Timer();
        private readonly ToolTip _toolTip = new ToolTip();
        private RectangleF _closeBounds;
        private RectangleF _actionBounds;
        private int _hover;
        private Action _action;
        private Font _titleFont;

        public InfoBar()
        {
            Visible = false;
            _autoClose.Tick += (s, e) => Dismiss();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public InfoSeverity Severity { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Message { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ActionText { get; private set; }

        public void Show(InfoSeverity severity, string title, string message, TimeSpan? autoClose = null, string actionText = null, Action action = null)
        {
            Severity = severity;
            Title = title;
            Message = message;
            ActionText = action == null ? null : actionText;
            _action = action;
            _toolTip.SetToolTip(this, string.IsNullOrEmpty(message) ? title : $"{title}{Environment.NewLine}{message}");
            _autoClose.Stop();
            if (autoClose.HasValue)
            {
                _autoClose.Interval = (int)autoClose.Value.TotalMilliseconds;
                _autoClose.Start();
            }
            Visible = true;
            Invalidate();
        }

        public void Dismiss()
        {
            _autoClose.Stop();
            Visible = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = P;
            g.Clear(Parent?.BackColor ?? p.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var (glyph, color) = Severity switch
            {
                InfoSeverity.Success => (Glyph.CheckMark, p.Success),
                InfoSeverity.Warning => (Glyph.Warning, p.Caution),
                InfoSeverity.Error => ("", p.Critical),
                _ => (Glyph.Info, p.AccentText)
            };

            var bounds = new RectangleF(0.5f, 4 * S, Width - 1.5f, Height - 8 * S - 0.5f);
            var fill = Severity == InfoSeverity.Informational ? p.Surface : Theme.Blend(p.Surface, color, p.IsDark ? 0.16 : 0.10);
            Theme.FillRounded(g, fill, bounds, 4 * S);
            Theme.DrawRounded(g, p.Stroke, bounds, 4 * S);

            var x = bounds.X + 14 * S;
            FluentIcons.Draw(g, glyph, color, new RectangleF(x, bounds.Y, 18 * S, bounds.Height), 16 * S);
            x += 18 * S + 12 * S;

            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix
                | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            _titleFont ??= new Font("Segoe UI Semibold", 9f);

            _closeBounds = new RectangleF(bounds.Right - 36 * S, bounds.Y + (bounds.Height - 28 * S) / 2, 28 * S, 28 * S);
            if (_hover == 1) Theme.FillRounded(g, p.HoverOverlay, _closeBounds, 4 * S);
            FluentIcons.Draw(g, Glyph.Close, p.TextSecondary, _closeBounds, 10 * S);
            var right = _closeBounds.Left - 8 * S;

            _actionBounds = RectangleF.Empty;
            if (!string.IsNullOrEmpty(ActionText))
            {
                var actionSize = Theme.MeasureText(g, ActionText, F(Theme.UiFont), flags);
                _actionBounds = new RectangleF(right - actionSize.Width - 20 * S, bounds.Y + (bounds.Height - 28 * S) / 2, actionSize.Width + 20 * S, 28 * S);
                Theme.FillRounded(g, _hover == 2 ? p.ControlHover : p.Control, _actionBounds, 4 * S);
                Theme.DrawRounded(g, p.ControlStroke, _actionBounds, 4 * S);
                TextRenderer.DrawText(g, ActionText, F(Theme.UiFont), Rectangle.Round(_actionBounds), p.Text, flags | TextFormatFlags.HorizontalCenter);
                right = _actionBounds.Left - 12 * S;
            }

            var titleSize = Theme.MeasureText(g, Title ?? string.Empty, F(_titleFont), flags);
            var titleWidth = Math.Min(titleSize.Width + 2, right - x);
            TextRenderer.DrawText(g, Title, F(_titleFont), new Rectangle((int)x, (int)bounds.Y, (int)titleWidth, (int)bounds.Height), p.Text, flags);
            x += titleWidth + 10 * S;
            if (!string.IsNullOrEmpty(Message) && right - x > 40 * S)
            {
                TextRenderer.DrawText(g, Message, F(Theme.UiFont), new Rectangle((int)x, (int)bounds.Y, (int)(right - x), (int)bounds.Height), p.TextSecondary, flags);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hover = _closeBounds.Contains(e.Location) ? 1 : _actionBounds.Contains(e.Location) ? 2 : 0;
            Cursor = hover != 0 ? Cursors.Hand : Cursors.Default;
            if (hover == _hover) return;
            _hover = hover;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = 0;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (_closeBounds.Contains(e.Location))
            {
                Dismiss();
            }
            else if (_actionBounds.Contains(e.Location))
            {
                _action?.Invoke();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoClose.Dispose();
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
