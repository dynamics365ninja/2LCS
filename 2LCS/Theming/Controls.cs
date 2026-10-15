using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LCS.Theming
{
    // Hides the native tab strip; PivotStrip shows the tabs.
    public class ModernTabControl : TabControl
    {
        protected override void WndProc(ref Message m)
        {
            const int TCM_ADJUSTRECT = 0x1328;
            if (m.Msg == TCM_ADJUSTRECT && !DesignMode)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
    }

    [ToolboxItem(false)]
    public abstract class FluentControl : Control
    {
        protected FluentControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            // Mouse-only surfaces; the keyboard goes to the real controls (grid, filter text box).
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        protected float S => DeviceDpi / 96f;

        protected Font F(Font font) => Theme.ForDpi(font, DeviceDpi);

        protected int Px(float value) => (int)Math.Round(value * S);

        private protected Palette P => Theme.Current;

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            PerformLayout();
            Invalidate();
        }
    }

    public sealed class SearchBox : FluentControl
    {
        private readonly TextBox _textBox = new TextBox { BorderStyle = BorderStyle.None };

        public SearchBox()
        {
            Cursor = Cursors.IBeam;
            _textBox.PlaceholderText = "Filter";
            _textBox.TextChanged += (s, e) => QueryChanged?.Invoke(this, EventArgs.Empty);
            _textBox.GotFocus += (s, e) => Invalidate();
            _textBox.LostFocus += (s, e) => Invalidate();
            _textBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Escape) return;
                _textBox.Clear();
                e.SuppressKeyPress = true;
            };
            Controls.Add(_textBox);
        }

        public event EventHandler QueryChanged;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Query => _textBox.Text.Trim();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Placeholder
        {
            get => _textBox.PlaceholderText;
            set => _textBox.PlaceholderText = value;
        }

        public void FocusSearch()
        {
            _textBox.Focus();
            _textBox.SelectAll();
        }

        public void SetQuery(string text)
        {
            _textBox.Text = text ?? string.Empty;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _textBox.Focus();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            _textBox.Font = Font;
            var left = Px(34);
            _textBox.SetBounds(left, (Height - _textBox.PreferredHeight) / 2 + 1, Math.Max(10, Width - left - Px(10)), _textBox.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = P;
            g.Clear(Parent?.BackColor ?? p.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var focused = _textBox.Focused;
            var fill = focused ? p.Surface : p.Control;
            _textBox.BackColor = fill;
            _textBox.ForeColor = p.Text;

            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            var radius = 4 * S;
            Theme.FillRounded(g, fill, bounds, radius);
            Theme.DrawRounded(g, p.ControlStroke, bounds, radius);
            if (focused)
            {
                // Windows 11 text box focus: accent underline.
                using var clip = Theme.RoundedRect(bounds, radius);
                g.SetClip(clip);
                using (var brush = new SolidBrush(p.Accent))
                {
                    g.FillRectangle(brush, 0, Height - 2 * S - 0.5f, Width, 2 * S);
                }
                g.ResetClip();
            }
            FluentIcons.Draw(g, Glyph.Search, p.TextSecondary, new RectangleF(Px(10), 0, Px(16), Height), 14 * S);
        }
    }

    public sealed class PivotStrip : FluentControl
    {
        private readonly Dictionary<TabPage, string> _badges = new Dictionary<TabPage, string>();
        private readonly List<RectangleF> _items = new List<RectangleF>();
        private TabControl _tabs;
        private int _hover = -1;
        private Font _itemFont;
        private Font _badgeFont;

        public PivotStrip()
        {
            Search = new SearchBox();
            Controls.Add(Search);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SearchBox Search { get; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TabControl Tabs
        {
            get => _tabs;
            set
            {
                _tabs = value;
                _tabs.SelectedIndexChanged += (s, e) => Invalidate();
                Invalidate();
            }
        }

        public void SetBadge(TabPage page, string text)
        {
            if (_badges.TryGetValue(page, out var current) && current == text) return;
            _badges[page] = text;
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var width = Math.Min(Px(280), Math.Max(Px(140), Width / 3));
            var height = Px(32);
            Search.Font = F(Theme.UiFont);
            Search.SetBounds(Width - width, (Height - height) / 2, width, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = P;
            g.Clear(BackColor);
            _items.Clear();
            if (_tabs == null) return;

            _itemFont ??= new Font("Segoe UI", 10.5f);
            _badgeFont ??= new Font("Segoe UI Semibold", 8f);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;

            var x = 0f;
            for (var i = 0; i < _tabs.TabPages.Count; i++)
            {
                var page = _tabs.TabPages[i];
                var selected = i == _tabs.SelectedIndex;
                var textSize = Theme.MeasureText(g, page.Text, F(_itemFont), flags);
                _badges.TryGetValue(page, out var badge);
                var badgeSize = string.IsNullOrEmpty(badge) ? Size.Empty : Theme.MeasureText(g, badge, F(_badgeFont), flags);
                var badgeWidth = badgeSize.IsEmpty ? 0 : badgeSize.Width + 12 * S;
                var itemWidth = 12 * S + textSize.Width + (badgeWidth > 0 ? 8 * S + badgeWidth : 0) + 12 * S;
                var item = new RectangleF(x, Height / 2f - 18 * S, itemWidth, 36 * S);
                _items.Add(item);

                if (i == _hover && !selected)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Theme.FillRounded(g, p.HoverOverlay, item, 4 * S);
                }

                var textColor = selected || i == _hover ? p.Text : p.TextSecondary;
                var textBounds = new Rectangle((int)(item.X + 12 * S), (int)item.Y, textSize.Width + 2, (int)item.Height);
                TextRenderer.DrawText(g, page.Text, F(_itemFont), textBounds, textColor, flags);

                if (badgeWidth > 0)
                {
                    var badgeRect = new RectangleF(textBounds.Right + 8 * S, item.Y + (item.Height - 20 * S) / 2, badgeWidth, 20 * S);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Theme.FillRounded(g, selected ? Theme.WithAlpha(p.Accent, p.IsDark ? 60 : 36) : p.SurfaceAlt, badgeRect, 10 * S);
                    TextRenderer.DrawText(g, badge, F(_badgeFont), Rectangle.Round(badgeRect), selected ? p.Text : p.TextSecondary,
                        flags | TextFormatFlags.HorizontalCenter);
                }

                if (selected)
                {
                    var indicatorWidth = Math.Max(16 * S, textSize.Width * 0.45f);
                    var indicator = new RectangleF(item.X + 12 * S + (textSize.Width - indicatorWidth) / 2, item.Bottom - 3 * S, indicatorWidth, 3 * S);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Theme.FillRounded(g, p.Accent, indicator, 1.5f * S);
                }
                x += itemWidth + 4 * S;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hit = _items.FindIndex(r => r.Contains(e.Location));
            Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
            if (hit == _hover) return;
            _hover = hit;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var hit = _items.FindIndex(r => r.Contains(e.Location));
            if (hit >= 0 && _tabs != null) _tabs.SelectedIndex = hit;
        }
    }

    public sealed class HeaderBar : FluentControl
    {
        private readonly ToolTip _toolTip = new ToolTip();
        private RectangleF _projectBounds;
        private RectangleF _sessionBounds;
        private int _hover;
        private Font _subtitleFont;
        private string _activeTooltip;

        public HeaderBar()
        {
            Text = "2LCS";
        }

        public event EventHandler ProjectClick;
        public event EventHandler SessionClick;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ProjectName { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ProjectDetail { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ProjectEnabled { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SessionText { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SessionTooltip { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color SessionColor { get; private set; }

        public void SetProject(string name, string detail, bool enabled)
        {
            ProjectName = name;
            ProjectDetail = detail;
            ProjectEnabled = enabled;
            Invalidate();
        }

        public void SetSession(string text, Color color, string tooltip)
        {
            SessionText = text;
            SessionColor = color;
            SessionTooltip = tooltip;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = P;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix
                | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

            var x = 16 * S;
            var tile = new RectangleF(x, (Height - 30 * S) / 2, 30 * S, 30 * S);
            AppLogo.Draw(g, tile);
            x = tile.Right + 10 * S;

            var titleSize = Theme.MeasureText(g, Text, F(Theme.TitleFont), flags);
            TextRenderer.DrawText(g, Text, F(Theme.TitleFont), new Rectangle((int)x, 0, titleSize.Width + 2, Height), p.Text, flags);
            x += titleSize.Width + 4 * S;
            _subtitleFont ??= new Font("Segoe UI", 8.5f);
            const string subtitle = "Lifecycle Services companion";
            var subtitleSize = Theme.MeasureText(g, subtitle, F(_subtitleFont), flags);
            var showSubtitle = Width > 900 * S;
            if (showSubtitle)
            {
                TextRenderer.DrawText(g, subtitle, F(_subtitleFont), new Rectangle((int)(x + 4 * S), (int)(2 * S), subtitleSize.Width + 2, Height), p.TextSecondary, flags);
                x += subtitleSize.Width + 8 * S;
            }
            x += 16 * S;

            _sessionBounds = RectangleF.Empty;
            if (!string.IsNullOrEmpty(SessionText))
            {
                var textSize = Theme.MeasureText(g, SessionText, F(Theme.UiFont), flags);
                var width = 12 * S + 8 * S + 8 * S + textSize.Width + 14 * S;
                _sessionBounds = new RectangleF(Width - 16 * S - width, (Height - 30 * S) / 2, width, 30 * S);
                Theme.FillRounded(g, Theme.WithAlpha(SessionColor, _hover == 2 ? (p.IsDark ? 70 : 52) : (p.IsDark ? 44 : 30)), _sessionBounds, 15 * S);
                using (var brush = new SolidBrush(SessionColor))
                {
                    g.FillEllipse(brush, _sessionBounds.X + 12 * S, _sessionBounds.Y + (_sessionBounds.Height - 8 * S) / 2, 8 * S, 8 * S);
                }
                TextRenderer.DrawText(g, SessionText, F(Theme.UiFont),
                    new Rectangle((int)(_sessionBounds.X + 28 * S), (int)_sessionBounds.Y, textSize.Width + 2, (int)_sessionBounds.Height), p.Text, flags);
            }

            var name = string.IsNullOrEmpty(ProjectName) ? "Choose a project" : ProjectName;
            var detail = string.IsNullOrEmpty(ProjectName) ? null : ProjectDetail;
            var nameSize = Theme.MeasureText(g, name, Theme.UiSemibold, flags);
            var detailSize = string.IsNullOrEmpty(detail) ? Size.Empty : Theme.MeasureText(g, detail, F(Theme.UiFont), flags);
            var limit = (_sessionBounds.IsEmpty ? Width - 16 * S : _sessionBounds.Left - 16 * S) - x;
            var chipWidth = Math.Min(limit, 12 * S + 16 * S + 8 * S + nameSize.Width + 2 + (detailSize.IsEmpty ? 0 : 8 * S + detailSize.Width + 2) + 40 * S);
            _projectBounds = chipWidth > 80 * S ? new RectangleF(x, (Height - 34 * S) / 2, chipWidth, 34 * S) : RectangleF.Empty;
            if (!_projectBounds.IsEmpty)
            {
                var enabled = ProjectEnabled;
                var fill = !enabled ? p.Window : _hover == 1 ? p.ControlHover : p.Control;
                Theme.FillRounded(g, fill, _projectBounds, 4 * S);
                Theme.DrawRounded(g, enabled ? p.ControlStroke : p.Stroke, _projectBounds, 4 * S);
                var cx = _projectBounds.X + 12 * S;
                FluentIcons.Draw(g, Glyph.Folder, enabled ? p.AccentText : p.TextDisabled, new RectangleF(cx, _projectBounds.Y, 16 * S, _projectBounds.Height), 15 * S);
                cx += 16 * S + 8 * S;
                var textRight = _projectBounds.Right - 32 * S;
                var nameWidth = Math.Min(nameSize.Width + 2, textRight - cx);
                TextRenderer.DrawText(g, name, Theme.UiSemibold, new Rectangle((int)cx, (int)_projectBounds.Y, (int)nameWidth, (int)_projectBounds.Height),
                    enabled ? (string.IsNullOrEmpty(ProjectName) ? p.TextSecondary : p.Text) : p.TextDisabled, flags);
                cx += nameWidth + 8 * S;
                if (!detailSize.IsEmpty && textRight - cx > 30 * S)
                {
                    TextRenderer.DrawText(g, detail, F(Theme.UiFont), new Rectangle((int)cx, (int)_projectBounds.Y, (int)(textRight - cx), (int)_projectBounds.Height),
                        enabled ? p.TextSecondary : p.TextDisabled, flags);
                }
                FluentIcons.Draw(g, Glyph.ChevronDown, enabled ? p.TextSecondary : p.TextDisabled,
                    new RectangleF(_projectBounds.Right - 26 * S, _projectBounds.Y, 14 * S, _projectBounds.Height), 10 * S);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hover = ProjectEnabled && _projectBounds.Contains(e.Location) ? 1 : _sessionBounds.Contains(e.Location) ? 2 : 0;
            Cursor = hover != 0 ? Cursors.Hand : Cursors.Default;
            var tooltip = hover == 2 ? SessionTooltip : hover == 1 ? "Change project" : null;
            if (tooltip != _activeTooltip)
            {
                _activeTooltip = tooltip;
                _toolTip.SetToolTip(this, tooltip);
            }
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
            if (ProjectEnabled && _projectBounds.Contains(e.Location)) ProjectClick?.Invoke(this, EventArgs.Empty);
            else if (_sessionBounds.Contains(e.Location)) SessionClick?.Invoke(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _toolTip.Dispose();
            base.Dispose(disposing);
        }
    }

    public sealed class ProgressLine : FluentControl
    {
        private readonly Timer _timer = new Timer { Interval = 16 };
        private float _phase;

        public ProgressLine()
        {
            _timer.Tick += (s, e) =>
            {
                _phase = (_phase + 0.012f) % 1.6f;
                Invalidate();
            };
        }

        [DefaultValue(false)]
        public bool Running
        {
            get => _timer.Enabled;
            set
            {
                _timer.Enabled = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? P.Window);
            if (!Running) return;
            var width = Width * 0.4f;
            var x = (_phase - 0.4f) * Width;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.FillRounded(e.Graphics, P.Accent, new RectangleF(x, 0, width, Height), Height / 2f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
