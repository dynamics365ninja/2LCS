using LCS.JsonObjects;
using LCS.Theming;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace LCS.Forms
{
    public sealed class EnvironmentDetailsPane : FluentControl
    {
        private const TextFormatFlags SingleLine = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix
            | TextFormatFlags.EndEllipsis;

        private readonly List<Hotspot> _hotspots = new List<Hotspot>();
        private readonly Timer _copiedTimer = new Timer { Interval = 1500 };
        private readonly ToolTip _toolTip = new ToolTip();
        private List<CloudHostedInstance> _instances = new List<CloudHostedInstance>();
        private Func<CloudHostedInstance, string> _lcsUrl;
        private int _scroll;
        private int _contentHeight;
        private Hotspot _hover;
        private string _copiedValue;
        private Font _titleFont;
        private Font _labelFont;
        private Font _sectionFont;

        public EnvironmentDetailsPane()
        {
            _copiedTimer.Tick += (s, e) =>
            {
                _copiedTimer.Stop();
                _copiedValue = null;
                Invalidate();
            };
        }

        internal void SetEnvironments(IEnumerable<CloudHostedInstance> instances, Func<CloudHostedInstance, string> lcsUrl)
        {
            var list = instances?.ToList() ?? new List<CloudHostedInstance>();
            if (list.Count == _instances.Count && list.SequenceEqual(_instances)) return;
            _instances = list;
            _lcsUrl = lcsUrl;
            _scroll = 0;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var p = P;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRounded(g, p.Surface, card, 6 * S);
            Theme.DrawRounded(g, p.Stroke, card, 6 * S);

            _titleFont ??= new Font("Segoe UI Semibold", 12f);
            _labelFont ??= new Font("Segoe UI", 8.5f);
            _sectionFont ??= new Font("Segoe UI Semibold", 9.5f);
            _hotspots.Clear();

            var clip = Rectangle.Round(RectangleF.Inflate(card, -1, -1));
            g.SetClip(clip);
            var x = 18 * S;
            var width = Width - 36 * S;
            var y = 16 * S - _scroll;

            if (_instances.Count == 0)
            {
                DrawEmpty(g, p);
            }
            else if (_instances.Count > 1)
            {
                y = DrawMultiple(g, p, x, y, width);
            }
            else
            {
                y = DrawSingle(g, p, _instances[0], x, y, width);
            }

            _contentHeight = (int)(y + _scroll + 16 * S);
            g.ResetClip();
            if (_contentHeight > Height)
            {
                var trackHeight = Height - 16 * S;
                var thumbHeight = Math.Max(24 * S, trackHeight * Height / _contentHeight);
                var thumbTop = 8 * S + (trackHeight - thumbHeight) * _scroll / Math.Max(1, _contentHeight - Height);
                Theme.FillRounded(g, Theme.WithAlpha(p.TextSecondary, 110), new RectangleF(Width - 7 * S, thumbTop, 3 * S, thumbHeight), 1.5f * S);
            }
        }

        private void DrawEmpty(Graphics g, Palette p)
        {
            var glyph = 32 * S;
            var top = Height / 2f - 50 * S;
            FluentIcons.Draw(g, Glyph.Info, p.TextDisabled, new RectangleF(0, top, Width, glyph), glyph);
            TextRenderer.DrawText(g, "No environment selected", F(_sectionFont),
                new Rectangle(0, (int)(top + glyph + 12 * S), Width, (int)(24 * S)), p.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, "Select an environment to see its details.", F(_labelFont),
                new Rectangle((int)(16 * S), (int)(top + glyph + 38 * S), Width - (int)(32 * S), (int)(40 * S)), p.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        private float DrawMultiple(Graphics g, Palette p, float x, float y, float width)
        {
            TextRenderer.DrawText(g, $"{_instances.Count} environments selected", F(_titleFont), new Rectangle((int)x, (int)y, (int)width, (int)(28 * S)), p.Text, SingleLine);
            y += 32 * S;
            TextRenderer.DrawText(g, "Actions in the command bar and the context menu apply to all of them.", F(_labelFont),
                new Rectangle((int)x, (int)y, (int)width, (int)(36 * S)), p.TextSecondary, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            y += 44 * S;
            foreach (var instance in _instances)
            {
                TextRenderer.DrawText(g, instance.DisplayName, Font, new Rectangle((int)x, (int)y, (int)(width * 0.55f), (int)(26 * S)), p.Text,
                    SingleLine | TextFormatFlags.VerticalCenter);
                if (!string.IsNullOrEmpty(instance.DeploymentStatus))
                {
                    GridStyler.DrawStatusPill(g, x + width * 0.55f, y + 13 * S, instance.DeploymentStatus, F(_labelFont), S, width * 0.45f);
                }
                y += 30 * S;
            }
            return y;
        }

        private float DrawSingle(Graphics g, Palette p, CloudHostedInstance instance, float x, float y, float width)
        {
            TextRenderer.DrawText(g, instance.DisplayName ?? instance.EnvironmentId, F(_titleFont), new Rectangle((int)x, (int)y, (int)width, (int)(28 * S)), p.Text, SingleLine);
            y += 30 * S;
            var subtitle = string.Join(" · ", new[] { instance.TopologyDisplayName ?? instance.TopologyType, instance.CurrentApplicationReleaseName }
                .Where(v => !string.IsNullOrEmpty(v)).Distinct());
            if (!string.IsNullOrEmpty(subtitle))
            {
                TextRenderer.DrawText(g, subtitle, F(_labelFont), new Rectangle((int)x, (int)y, (int)width, (int)(20 * S)), p.TextSecondary, SingleLine);
                y += 22 * S;
            }
            if (!string.IsNullOrEmpty(instance.DeploymentStatus) || !string.IsNullOrEmpty(instance.VmState))
            {
                y += 4 * S;
                var labelWidth = 44 * S;
                if (!string.IsNullOrEmpty(instance.DeploymentStatus))
                {
                    TextRenderer.DrawText(g, "LCS", F(_labelFont), new Rectangle((int)x, (int)y, (int)labelWidth, (int)(22 * S)), p.TextSecondary, SingleLine | TextFormatFlags.VerticalCenter);
                    GridStyler.DrawStatusPill(g, x + labelWidth, y + 11 * S, instance.DeploymentStatus, F(_labelFont), S, width - labelWidth);
                    y += 28 * S;
                }
                if (!string.IsNullOrEmpty(instance.VmState))
                {
                    TextRenderer.DrawText(g, "Azure", F(_labelFont), new Rectangle((int)x, (int)y, (int)labelWidth, (int)(22 * S)), p.TextSecondary, SingleLine | TextFormatFlags.VerticalCenter);
                    GridStyler.DrawStatusPill(g, x + labelWidth, y + 11 * S, instance.VmState, F(_labelFont), S, width - labelWidth);
                    y += 28 * S;
                }
                y += 2 * S;
            }
            if (instance.VmStateMismatch && !string.IsNullOrEmpty(instance.VmStateDetail))
            {
                y = Warning(g, p, instance.VmStateDetail.Split(new[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.None)[0], x, y, width);
            }
            else if (string.IsNullOrEmpty(instance.VmState) && !string.IsNullOrEmpty(instance.VmStateDetail))
            {
                var height = TextRenderer.MeasureText(g, instance.VmStateDetail, F(_labelFont), new Size((int)width, int.MaxValue), TextFormatFlags.WordBreak).Height;
                TextRenderer.DrawText(g, instance.VmStateDetail, F(_labelFont), new Rectangle((int)x, (int)y, (int)width, height), p.TextSecondary,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                y += height + 6 * S;
            }

            y = Section(g, p, "Overview", x, y, width);
            y = Field(g, p, "Environment ID", instance.EnvironmentId, x, y, width, copy: true);
            y = Field(g, p, "Instance ID", instance.InstanceId, x, y, width, copy: true);
            y = Field(g, p, "Deployed by", instance.DeployedBy, x, y, width);
            y = Field(g, p, "Deployed on", instance.DeployedOn, x, y, width);
            y = Field(g, p, "Environment admin", instance.EnvironmentAdmin, x, y, width, copy: true);

            y = Section(g, p, "Versions", x, y, width);
            y = Field(g, p, "Application release", instance.CurrentApplicationReleaseName, x, y, width);
            y = Field(g, p, "Application build", instance.CurrentApplicationBuildVersion, x, y, width, copy: true);
            y = Field(g, p, "Platform release", instance.CurrentPlatformReleaseName, x, y, width);
            y = Field(g, p, "Platform version", instance.CurrentPlatformVersion, x, y, width, copy: true);
            y = Field(g, p, "Build info", instance.BuildInfo, x, y, width);

            var links = (instance.NavigationLinks ?? Array.Empty<Navigationlink>())
                .Where(l => !string.IsNullOrEmpty(l.NavigationUri))
                .Select(l => (l.DisplayName, l.NavigationUri))
                .ToList();
            var lcs = _lcsUrl?.Invoke(instance);
            if (!string.IsNullOrEmpty(lcs)) links.Add(("Environment page in LCS", lcs));
            if (links.Count > 0)
            {
                y = Section(g, p, "Links", x, y, width);
                foreach (var (name, url) in links)
                {
                    var bounds = new RectangleF(x - 6 * S, y, width + 12 * S, 30 * S);
                    var hotspot = new Hotspot(bounds, url, isLink: true, tooltip: url);
                    _hotspots.Add(hotspot);
                    if (Equals(_hover, hotspot)) Theme.FillRounded(g, p.HoverOverlay, bounds, 4 * S);
                    FluentIcons.Draw(g, Glyph.OpenInNew, p.AccentText, new RectangleF(x, y, 14 * S, 30 * S), 12 * S);
                    TextRenderer.DrawText(g, name, Font, new Rectangle((int)(x + 22 * S), (int)y, (int)(width - 22 * S), (int)(30 * S)), p.AccentText,
                        SingleLine | TextFormatFlags.VerticalCenter);
                    y += 32 * S;
                }
            }
            return y;
        }

        private float Warning(Graphics g, Palette p, string text, float x, float y, float width)
        {
            var padding = 10 * S;
            var iconSpace = 26 * S;
            var textWidth = (int)(width - padding * 2 - iconSpace);
            var textHeight = TextRenderer.MeasureText(g, text, F(_labelFont), new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;
            var box = new RectangleF(x, y + 4 * S, width, textHeight + padding * 2);
            Theme.FillRounded(g, Theme.Blend(p.Surface, p.Caution, p.IsDark ? 0.16 : 0.10), box, 4 * S);
            Theme.DrawRounded(g, p.Stroke, box, 4 * S);
            FluentIcons.Draw(g, Glyph.Warning, p.Caution, new RectangleF(box.X + padding, box.Y + padding, 16 * S, 18 * S), 14 * S);
            TextRenderer.DrawText(g, text, F(_labelFont), new Rectangle((int)(box.X + padding + iconSpace), (int)(box.Y + padding), textWidth, textHeight), p.Text,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return box.Bottom + 8 * S;
        }

        private float Section(Graphics g, Palette p, string title, float x, float y, float width)
        {
            y += 12 * S;
            using (var pen = new Pen(p.Divider))
            {
                g.DrawLine(pen, x, y, x + width, y);
            }
            y += 12 * S;
            TextRenderer.DrawText(g, title, F(_sectionFont), new Rectangle((int)x, (int)y, (int)width, (int)(22 * S)), p.Text, SingleLine);
            return y + 26 * S;
        }

        private float Field(Graphics g, Palette p, string label, string value, float x, float y, float width, bool copy = false)
        {
            if (string.IsNullOrWhiteSpace(value)) return y;
            var rowHeight = 42 * S;
            var bounds = new RectangleF(x - 6 * S, y - 2 * S, width + 12 * S, rowHeight);
            Hotspot hotspot = null;
            if (copy)
            {
                hotspot = new Hotspot(bounds, value, isLink: false, tooltip: $"Copy {label.ToLowerInvariant()}");
                _hotspots.Add(hotspot);
                if (Equals(_hover, hotspot)) Theme.FillRounded(g, p.HoverOverlay, bounds, 4 * S);
            }
            TextRenderer.DrawText(g, label, F(_labelFont), new Rectangle((int)x, (int)y, (int)width, (int)(18 * S)), p.TextSecondary, SingleLine);
            var valueWidth = copy ? width - 28 * S : width;
            TextRenderer.DrawText(g, value, Font, new Rectangle((int)x, (int)(y + 18 * S), (int)valueWidth, (int)(20 * S)), p.Text, SingleLine);
            if (copy && (Equals(_hover, hotspot) || _copiedValue == value))
            {
                var copied = _copiedValue == value;
                FluentIcons.Draw(g, copied ? Glyph.CheckMark : "", copied ? p.Success : p.TextSecondary,
                    new RectangleF(x + width - 20 * S, y, 20 * S, rowHeight - 4 * S), 14 * S);
            }
            return y + rowHeight + 2 * S;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var max = Math.Max(0, _contentHeight - Height);
            _scroll = Math.Max(0, Math.Min(max, _scroll - e.Delta / 2));
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hit = _hotspots.FirstOrDefault(h => h.Bounds.Contains(e.Location));
            Cursor = hit != null ? Cursors.Hand : Cursors.Default;
            if (Equals(hit, _hover)) return;
            _hover = hit;
            _toolTip.SetToolTip(this, hit?.Tooltip);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = null;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            var hit = _hotspots.FirstOrDefault(h => h.Bounds.Contains(e.Location));
            if (hit == null) return;
            if (hit.IsLink)
            {
                WebBrowserHelper.OpenUri(hit.Value);
                return;
            }
            Clipboard.SetText(hit.Value);
            _copiedValue = hit.Value;
            _copiedTimer.Stop();
            _copiedTimer.Start();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _copiedTimer.Dispose();
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class Hotspot
        {
            public Hotspot(RectangleF bounds, string value, bool isLink, string tooltip)
            {
                Bounds = bounds;
                Value = value;
                IsLink = isLink;
                Tooltip = tooltip;
            }

            public RectangleF Bounds { get; }
            public string Value { get; }
            public bool IsLink { get; }
            public string Tooltip { get; }

            public override bool Equals(object obj) => obj is Hotspot other && other.Value == Value && other.IsLink == IsLink;

            public override int GetHashCode() => HashCode.Combine(Value, IsLink);
        }
    }
}
