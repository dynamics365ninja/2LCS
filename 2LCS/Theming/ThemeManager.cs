using Microsoft.Web.WebView2.WinForms;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LCS.Theming
{
    internal static class ThemeManager
    {
        private static readonly ConditionalWeakTable<object, object> Styled = new ConditionalWeakTable<object, object>();
        private static FluentRenderer _renderer;

        public static FluentRenderer Renderer => _renderer ??= new FluentRenderer(Theme.Current);

        public static void InitializeApplication()
        {
            Theme.Initialize();
            // WinForms native dark mode (.NET 9+) for scroll bars, check boxes, text boxes...
            Application.SetColorMode(Theme.Current.IsDark ? SystemColorMode.Dark : SystemColorMode.Classic);
            ToolStripManager.Renderer = Renderer;
        }

        public static event EventHandler ThemeChanged;

        public static void Apply(Control root)
        {
            var p = Theme.Current;
            ApplyControl(root, p);
        }

        public static void ApplyThemeChange()
        {
            var previous = Theme.Current;
            Theme.Initialize();
            var current = Theme.Current;
            if (previous.IsDark == current.IsDark && previous.Accent == current.Accent) return;

            Application.SetColorMode(current.IsDark ? SystemColorMode.Dark : SystemColorMode.Classic);
            _renderer = new FluentRenderer(current);
            ToolStripManager.Renderer = _renderer;

            foreach (var form in Application.OpenForms.Cast<Form>().ToList())
            {
                form.SuspendLayout();
                NativeTheme.ApplyWindowChrome(form, current);
                RemapColors(form, previous, current);
                // Native controls pick up light/dark only when their window is created.
                RecreateNativeControls(form);
                ApplyControl(form, current);
                form.ResumeLayout(true);
                form.Invalidate(true);
            }
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        private static readonly PropertyInfo[] PaletteColors = typeof(Palette).GetProperties()
            .Where(property => property.PropertyType == typeof(Color)).ToArray();

        private static readonly MethodInfo RecreateHandleMethod =
            typeof(Control).GetMethod("RecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void RemapColors(Control control, Palette from, Palette to)
        {
            var back = Remap(control.BackColor, from, to);
            if (back.HasValue) control.BackColor = back.Value;
            var fore = Remap(control.ForeColor, from, to);
            if (fore.HasValue) control.ForeColor = fore.Value;
            foreach (Control child in control.Controls)
            {
                RemapColors(child, from, to);
            }
        }

        private static Color? Remap(Color color, Palette from, Palette to)
        {
            if (color.IsEmpty || color.A == 0) return null;
            foreach (var property in PaletteColors)
            {
                if (((Color)property.GetValue(from)).ToArgb() == color.ToArgb()) return (Color)property.GetValue(to);
            }
            return null;
        }

        private static void RecreateNativeControls(Control control)
        {
            foreach (Control child in control.Controls)
            {
                if (child is WebView2) continue;
                if (child.IsHandleCreated && (child is TextBoxBase || child is CheckBox || child is RadioButton || child is ComboBox
                    || child is ListBox || child is ListView || child is TreeView || child is ScrollBar || child is UpDownBase
                    || child is DateTimePicker))
                {
                    RecreateHandleMethod?.Invoke(child, null);
                }
                RecreateNativeControls(child);
            }
        }

        private static void ApplyControl(Control control, Palette p)
        {
            if (control.ContextMenuStrip != null) StyleToolStrip(control.ContextMenuStrip);

            switch (control)
            {
                case Form form:
                    form.BackColor = p.Window;
                    form.ForeColor = p.Text;
                    break;
                case DataGridView grid:
                    GridStyler.Apply(grid);
                    return;
                case ToolStrip strip:
                    StyleToolStrip(strip);
                    return;
                case WebView2 webView:
                    webView.DefaultBackgroundColor = p.Window;
                    return;
                case Button button:
                    FluentButton.Attach(button);
                    return;
                case LinkLabel link:
                    link.LinkColor = p.AccentText;
                    link.ActiveLinkColor = p.AccentText;
                    link.VisitedLinkColor = p.AccentText;
                    link.LinkBehavior = LinkBehavior.HoverUnderline;
                    link.BackColor = Color.Transparent;
                    return;
                case Label label:
                    if (IsDefaultForeColor(label.ForeColor)) label.ForeColor = p.Text;
                    label.BackColor = Color.Transparent;
                    return;
                case CheckBox _:
                case RadioButton _:
                    control.ForeColor = p.Text;
                    control.BackColor = Color.Transparent;
                    return;
                case TextBoxBase textBox:
                    textBox.BackColor = textBox.ReadOnly && !(textBox is RichTextBox) && !textBox.Multiline ? p.Window : p.Surface;
                    textBox.ForeColor = p.Text;
                    if (textBox.BorderStyle == BorderStyle.Fixed3D) textBox.BorderStyle = BorderStyle.FixedSingle;
                    return;
                case ComboBox _:
                case ListBox _:
                case ListView _:
                case TreeView _:
                case NumericUpDown _:
                case DateTimePicker _:
                    control.BackColor = p.Surface;
                    control.ForeColor = p.Text;
                    break;
                case GroupBox groupBox:
                    groupBox.ForeColor = p.TextSecondary;
                    groupBox.BackColor = Color.Transparent;
                    break;
                case TabPage tabPage:
                    tabPage.UseVisualStyleBackColor = false;
                    tabPage.BackColor = p.Window;
                    tabPage.ForeColor = p.Text;
                    break;
                case PictureBox _:
                    return;
                case SettingsRow row:
                    row.BackColor = p.Surface;
                    row.ForeColor = p.Text;
                    break;
                default:
                    if (IsDefaultBackColor(control.BackColor)) control.BackColor = p.Window;
                    if (IsDefaultForeColor(control.ForeColor)) control.ForeColor = p.Text;
                    break;
            }

            foreach (Control child in control.Controls)
            {
                ApplyControl(child, p);
                // GroupBox children would inherit the secondary caption color.
                if (control is GroupBox && IsDefaultForeColor(child.ForeColor, p.TextSecondary)) child.ForeColor = p.Text;
            }
        }

        public static void StyleToolStrip(ToolStrip strip)
        {
            var p = Theme.Current;
            strip.Renderer = Renderer;
            strip.ForeColor = p.Text;
            strip.BackColor = strip is ToolStripDropDown ? Renderer.MenuBackground : p.Window;

            if (Styled.TryGetValue(strip, out _))
            {
                StyleItems(strip.Items, strip.DeviceDpi);
                return;
            }
            Styled.Add(strip, null);

            if (strip is ToolStripDropDown dropDown)
            {
                var s = dropDown.DeviceDpi / 96f;
                dropDown.Padding = new Padding(0, (int)(4 * s), 0, (int)(4 * s));
                if (NativeTheme.IsWindows11)
                {
                    dropDown.DropShadowEnabled = true;
                    dropDown.HandleCreated += (sender, e) => NativeTheme.ApplyPopupChrome((Control)sender, Theme.Current);
                    if (dropDown.IsHandleCreated) NativeTheme.ApplyPopupChrome(dropDown, p);
                }
                // Items added at runtime (custom/project links) are styled when the menu opens.
                dropDown.Opening += (sender, e) => StyleItems(((ToolStrip)sender).Items, ((ToolStrip)sender).DeviceDpi);
            }
            StyleItems(strip.Items, strip.DeviceDpi);
        }

        private static void StyleItems(ToolStripItemCollection items, int dpi)
        {
            var s = dpi / 96f;
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.IsOnDropDown)
                {
                    menuItem.Padding = new Padding(0, (int)(4 * s), 0, (int)(4 * s));
                }
                if (item is ToolStripDropDownItem dropDownItem && dropDownItem.HasDropDownItems)
                {
                    StyleToolStrip(dropDownItem.DropDown);
                }
            }
        }

        public static bool IsDefaultBackColor(Color color)
        {
            return color == SystemColors.Control || color == SystemColors.Window || color == SystemColors.ButtonFace
                || color == SystemColors.ControlLight || color == Color.White || color == Color.Transparent
                || color == SystemColors.Menu || color == Color.Empty;
        }

        public static bool IsDefaultForeColor(Color color, Color? alsoDefault = null)
        {
            return color == SystemColors.ControlText || color == SystemColors.WindowText || color == Color.Black
                || color == SystemColors.MenuText || color == Color.Empty || (alsoDefault.HasValue && color == alsoDefault.Value);
        }
    }

    internal static class FluentButton
    {
        private static readonly ConditionalWeakTable<Button, object> Attached = new ConditionalWeakTable<Button, object>();
        private static readonly PropertyInfo ShowFocusCuesProperty =
            typeof(Control).GetProperty("ShowFocusCues", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Attach(Button button)
        {
            button.BackColor = Theme.Current.Control;
            button.ForeColor = Theme.Current.Text;
            if (Attached.TryGetValue(button, out _))
            {
                button.Invalidate();
                return;
            }
            Attached.Add(button, null);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.Paint += OnPaint;
            button.EnabledChanged += (s, e) => ((Control)s).Invalidate();
        }

        public static bool IsPrimary(Button button)
        {
            var form = button.FindForm();
            return form != null && form.AcceptButton == button;
        }

        private static void OnPaint(object sender, PaintEventArgs e)
        {
            var button = (Button)sender;
            var p = Theme.Current;
            var g = e.Graphics;
            var s = button.DeviceDpi / 96f;

            var hover = button.Enabled && button.ClientRectangle.Contains(button.PointToClient(Control.MousePosition));
            var pressed = hover && (Control.MouseButtons & MouseButtons.Left) != 0;
            var primary = IsPrimary(button);

            Color fill, border, text;
            if (!button.Enabled)
            {
                fill = p.IsDark ? Color.FromArgb(0x2A, 0x2A, 0x2A) : Color.FromArgb(0xF5, 0xF5, 0xF5);
                border = p.Stroke;
                text = p.TextDisabled;
            }
            else if (primary)
            {
                fill = pressed ? p.AccentPressed : hover ? p.AccentHover : p.Accent;
                border = fill;
                text = p.OnAccent;
            }
            else
            {
                fill = pressed ? p.ControlPressed : hover ? p.ControlHover : p.Control;
                border = p.ControlStroke;
                text = pressed ? p.TextSecondary : p.Text;
            }

            g.Clear(button.Parent?.BackColor is Color parentColor && parentColor.A == 255 ? parentColor : p.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0.5f, 0.5f, button.Width - 1.5f, button.Height - 1.5f);
            var radius = 4 * s;
            Theme.FillRounded(g, fill, bounds, radius);
            Theme.DrawRounded(g, border, bounds, radius);

            var focusCues = ShowFocusCuesProperty?.GetValue(button) is bool cues && cues;
            if (button.Focused && focusCues)
            {
                Theme.DrawRounded(g, p.Text, new RectangleF(1.5f, 1.5f, button.Width - 3.5f, button.Height - 3.5f), radius, 1.5f * s);
            }

            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                | TextFormatFlags.SingleLine;
            if (!focusCues) flags |= TextFormatFlags.HidePrefix;
            var textBounds = Rectangle.Inflate(button.ClientRectangle, -(int)(6 * s), 0);
            if (button.Image != null)
            {
                var image = button.Image;
                var textWidth = Theme.MeasureText(g, button.Text, button.Font, flags).Width;
                var spacing = string.IsNullOrEmpty(button.Text) ? 0 : (int)(6 * s);
                var total = image.Width + spacing + textWidth;
                var x = (button.Width - total) / 2;
                g.DrawImage(image, x, (button.Height - image.Height) / 2, image.Width, image.Height);
                textBounds = new Rectangle(x + image.Width + spacing, 0, textWidth + 2, button.Height);
                flags &= ~TextFormatFlags.HorizontalCenter;
            }
            TextRenderer.DrawText(g, button.Text, button.Font, textBounds, text, flags);
        }
    }
}
