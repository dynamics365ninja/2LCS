using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace LCS.Theming
{
    internal sealed class FluentRenderer : ToolStripProfessionalRenderer
    {
        private readonly Palette _p;

        public FluentRenderer(Palette palette) : base(new FluentColorTable(palette))
        {
            _p = palette;
            RoundedEdges = false;
        }

        public Color MenuBackground => _p.IsDark ? Color.FromArgb(0x2C, 0x2C, 0x2C) : Color.FromArgb(0xF9, 0xF9, 0xF9);

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            var color = e.ToolStrip is ToolStripDropDown ? MenuBackground : _p.Window;
            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;
            if (e.ToolStrip is ToolStripDropDown)
            {
                // On Windows 11 DWM draws a rounded border (NativeTheme.ApplyPopupChrome).
                if (NativeTheme.IsWindows11) return;
                using var pen = new Pen(_p.Stroke);
                g.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
            else if (e.ToolStrip is StatusStrip)
            {
                using var pen = new Pen(_p.Divider);
                g.DrawLine(pen, 0, 0, e.ToolStrip.Width, 0);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            var active = item.Enabled && (item.Selected || item.Pressed);
            if (!active) return;
            FillItemHighlight(e.Graphics, item, item.Pressed && !item.IsOnDropDown ? _p.PressedOverlay : _p.HoverOverlay);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            if (item is ToolStripButton button && button.Checked)
            {
                FillItemHighlight(e.Graphics, item, _p.HoverOverlay);
                return;
            }
            if (!item.Enabled || !(item.Selected || item.Pressed)) return;
            FillItemHighlight(e.Graphics, item, item.Pressed ? _p.PressedOverlay : _p.HoverOverlay);
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            OnRenderButtonBackground(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled)
            {
                e.TextColor = _p.TextDisabled;
            }
            else if (e.Item is ToolStripStatusLabel)
            {
                e.TextColor = e.Item.Tag is Color custom ? custom : _p.TextSecondary;
            }
            else
            {
                e.TextColor = _p.Text;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Image == null) return;
            if (e.Item.Enabled)
            {
                e.Graphics.DrawImage(e.Image, e.ImageRectangle);
                return;
            }
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = 0.35f });
            var r = e.ImageRectangle;
            e.Graphics.DrawImage(e.Image, r, 0, 0, e.Image.Width, e.Image.Height, GraphicsUnit.Pixel, attributes);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            var glyph = e.Direction switch
            {
                ArrowDirection.Down => Glyph.ChevronDown,
                ArrowDirection.Up => Glyph.ChevronUp,
                _ => Glyph.ChevronRight
            };
            var color = e.Item == null || e.Item.Enabled ? _p.TextSecondary : _p.TextDisabled;
            var size = Theme.Scale(10f, DpiOf(e.Item?.Owner));
            FluentIcons.Draw(e.Graphics, glyph, color, e.ArrowRectangle, size);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var bounds = e.Item.ContentRectangle;
            var s = DpiOf(e.Item.Owner) / 96f;
            using var pen = new Pen(_p.Divider);
            if (e.Vertical)
            {
                var x = e.Item.Width / 2;
                var inset = (int)(e.Item.Height * 0.25f);
                e.Graphics.DrawLine(pen, x, inset, x, e.Item.Height - inset);
            }
            else
            {
                var y = e.Item.Height / 2;
                var inset = (int)(4 * s);
                e.Graphics.DrawLine(pen, inset, y, e.Item.Width - inset, y);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var size = Theme.Scale(14f, DpiOf(e.Item.Owner));
            FluentIcons.Draw(e.Graphics, Glyph.CheckMark, _p.AccentText, e.ImageRectangle, size);
        }

        protected override void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e)
        {
        }

        private void FillItemHighlight(Graphics g, ToolStripItem item, Color color)
        {
            var s = DpiOf(item.Owner) / 96f;
            RectangleF r = item.IsOnDropDown
                ? new RectangleF(4 * s, 1, item.Width - 8 * s, item.Height - 2)
                : new RectangleF(0, 2 * s, item.Width - 1, item.Height - 4 * s);
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.FillRounded(g, color, r, 4 * s);
            g.SmoothingMode = smoothing;
        }

        private static int DpiOf(Control control) => control?.DeviceDpi ?? 96;

        private sealed class FluentColorTable : ProfessionalColorTable
        {
            private readonly Palette _p;

            public FluentColorTable(Palette palette)
            {
                _p = palette;
                UseSystemColors = false;
            }

            public override Color MenuBorder => _p.Stroke;
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuItemSelected => _p.ControlHover;
            public override Color MenuItemSelectedGradientBegin => _p.ControlHover;
            public override Color MenuItemSelectedGradientEnd => _p.ControlHover;
            public override Color MenuItemPressedGradientBegin => _p.ControlPressed;
            public override Color MenuItemPressedGradientEnd => _p.ControlPressed;
            public override Color MenuStripGradientBegin => _p.Window;
            public override Color MenuStripGradientEnd => _p.Window;
            public override Color ToolStripDropDownBackground => _p.IsDark ? Color.FromArgb(0x2C, 0x2C, 0x2C) : Color.FromArgb(0xF9, 0xF9, 0xF9);
            public override Color ToolStripBorder => _p.Window;
            public override Color ToolStripGradientBegin => _p.Window;
            public override Color ToolStripGradientMiddle => _p.Window;
            public override Color ToolStripGradientEnd => _p.Window;
            public override Color StatusStripGradientBegin => _p.Window;
            public override Color StatusStripGradientEnd => _p.Window;
            public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
            public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
            public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
            public override Color SeparatorDark => _p.Divider;
            public override Color SeparatorLight => _p.Divider;
            public override Color ButtonSelectedHighlight => _p.ControlHover;
            public override Color ButtonSelectedBorder => Color.Transparent;
            public override Color ButtonPressedBorder => Color.Transparent;
            public override Color ButtonCheckedHighlight => _p.ControlHover;
            public override Color CheckBackground => Color.Transparent;
            public override Color CheckSelectedBackground => Color.Transparent;
            public override Color CheckPressedBackground => Color.Transparent;
            public override Color OverflowButtonGradientBegin => _p.Window;
            public override Color OverflowButtonGradientMiddle => _p.Window;
            public override Color OverflowButtonGradientEnd => _p.Window;
            public override Color GripDark => _p.Divider;
            public override Color GripLight => _p.Window;
        }
    }
}
