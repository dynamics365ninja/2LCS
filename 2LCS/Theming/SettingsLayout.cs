using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LCS.Theming
{
    public sealed class SettingsPage : Panel
    {
        public SettingsPage()
        {
            AutoScroll = true;
            DoubleBuffered = true;
        }

        public SettingsHeader AddHeader(string text)
        {
            var header = new SettingsHeader { Text = text };
            Controls.Add(header);
            return header;
        }

        public SettingsRow AddRow(string title, string description, Control editor)
        {
            // Rows are built after the form was auto-scaled - editor sizes are given at 96 DPI.
            if (editor != null)
            {
                editor.Size = new Size(LogicalToDeviceUnits(editor.Width), editor is TextBox ? editor.Height : LogicalToDeviceUnits(editor.Height));
            }
            var row = new SettingsRow(title, description, editor);
            Controls.Add(row);
            return row;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var s = DeviceDpi / 96f;
            var width = ClientSize.Width - Padding.Horizontal;
            var y = Padding.Top + AutoScrollPosition.Y;
            var first = true;
            foreach (Control control in Controls)
            {
                if (!control.Visible) continue;
                if (control is SettingsHeader && !first) y += (int)(20 * s);
                var height = control is SettingsRow row ? row.HeightFor(width) : ((SettingsHeader)control).HeightFor();
                control.SetBounds(Padding.Left, y, width, height);
                y += height + (int)(4 * s);
                first = false;
            }
            AutoScrollMinSize = new Size(0, y - AutoScrollPosition.Y + Padding.Bottom);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            PerformLayout();
        }
    }

    public sealed class SettingsHeader : Control
    {
        public SettingsHeader()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Font = new Font("Segoe UI Semibold", 10.5f);
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        public int HeightFor() => (int)(Font.Height + 10 * DeviceDpi / 96f);

        protected override void OnPaint(PaintEventArgs e)
        {
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, 0, Width, Height), Theme.Current.Text,
                TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    public sealed class SettingsRow : Panel
    {
        private static Font _descriptionFont;
        private readonly Label _title = new Label { AutoSize = false, UseMnemonic = false, BackColor = Color.Transparent };
        private readonly Label _description = new Label { AutoSize = false, UseMnemonic = false, BackColor = Color.Transparent };

        public SettingsRow(string title, string description, Control editor)
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            _title.Text = title;
            _description.Text = description ?? string.Empty;
            _description.Visible = !string.IsNullOrEmpty(description);
            _descriptionFont ??= new Font("Segoe UI", 8.5f);
            _description.Font = _descriptionFont;
            Editor = editor;
            Controls.Add(_title);
            Controls.Add(_description);
            if (editor != null) Controls.Add(editor);
            // Clicking the text toggles a switch, like in Windows Settings.
            _title.Click += (s, e) => ToggleEditor();
            _description.Click += (s, e) => ToggleEditor();
        }

        public Control Editor { get; }

        private float S => DeviceDpi / 96f;

        private int TextWidth(int width) => Math.Max(80, width - (int)(20 * S) * 2 - (Editor?.Width ?? 0) - (int)(16 * S));

        public int HeightFor(int width)
        {
            var textWidth = TextWidth(width);
            var titleHeight = TextRenderer.MeasureText(_title.Text, _title.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;
            var descriptionHeight = _description.Visible
                ? TextRenderer.MeasureText(_description.Text, _description.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height
                : 0;
            var content = Math.Max(titleHeight + descriptionHeight, Editor?.Height ?? 0);
            return content + (int)(28 * S);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var padding = (int)(20 * S);
            var textWidth = TextWidth(Width);
            var titleHeight = TextRenderer.MeasureText(_title.Text, _title.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;
            var descriptionHeight = _description.Visible
                ? TextRenderer.MeasureText(_description.Text, _description.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height
                : 0;
            var top = (Height - titleHeight - descriptionHeight) / 2;
            _title.SetBounds(padding, top, textWidth, titleHeight);
            _description.SetBounds(padding, top + titleHeight, textWidth, descriptionHeight);
            Editor?.SetBounds(Width - padding - Editor.Width, (Height - Editor.Height) / 2, Editor.Width, Editor.Height);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var p = Theme.Current;
            e.Graphics.Clear(Parent?.BackColor ?? p.Window);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRounded(e.Graphics, p.Surface, bounds, 4 * S);
            Theme.DrawRounded(e.Graphics, p.Stroke, bounds, 4 * S);
            _title.ForeColor = Enabled ? p.Text : p.TextDisabled;
            _description.ForeColor = p.TextSecondary;
        }

        private void ToggleEditor()
        {
            if (Editor is CheckBox checkBox && checkBox.Enabled)
            {
                checkBox.Checked = !checkBox.Checked;
                checkBox.Focus();
            }
            else
            {
                Editor?.Focus();
            }
        }
    }
}
