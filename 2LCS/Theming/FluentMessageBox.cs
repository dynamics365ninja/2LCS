using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LCS.Theming
{
    internal static class FluentMessageBox
    {
        public static DialogResult Show(string text) =>
            Show(null, text, "2LCS", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(string text, string caption) =>
            Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) =>
            Show(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
            Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(IWin32Window owner, string text) =>
            Show(owner, text, "2LCS", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(IWin32Window owner, string text, string caption) =>
            Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) =>
            Show(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
            Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton)
        {
            // Create the dialog on the owner's UI thread (some callers run on a BackgroundWorker).
            if (owner is Control control && !control.IsDisposed && control.InvokeRequired)
            {
                return (DialogResult)control.Invoke(new Func<DialogResult>(() => Show(owner, text, caption, buttons, icon, defaultButton)));
            }
            if (owner == null)
            {
                owner = Form.ActiveForm;
                if (owner is Control active && active.InvokeRequired) owner = null;
            }
            if (owner == null && Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired)
            {
                // Not on a UI thread and nothing to marshal to.
                return MessageBox.Show(text, caption, buttons, icon, defaultButton);
            }

            using var dialog = new MessageDialog(text, string.IsNullOrEmpty(caption) ? "2LCS" : caption, buttons, icon, defaultButton);
            return owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        }

        private sealed class MessageDialog : ThemedForm
        {
            private readonly string _caption;
            private readonly string _text;
            private readonly MessageBoxIcon _icon;
            private readonly Panel _content = new Panel();
            private readonly Label _title = new Label();
            private readonly TextBox _message = new TextBox();
            private readonly FlowLayoutPanel _footer = new FlowLayoutPanel();

            public MessageDialog(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
            {
                _caption = caption;
                _text = text ?? string.Empty;
                _icon = icon;

                AutoScaleMode = AutoScaleMode.Dpi;
                AutoScaleDimensions = new SizeF(96F, 96F);
                Text = caption;
                Font = Theme.UiFont;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                ShowIcon = false;
                StartPosition = FormStartPosition.CenterParent;
                KeyPreview = true;

                _title.Text = caption;
                _title.Font = new Font("Segoe UI Semibold", 12f);
                _title.AutoEllipsis = true;
                _title.UseMnemonic = false;

                _message.Text = _text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
                _message.Multiline = true;
                _message.ReadOnly = true;
                _message.BorderStyle = BorderStyle.None;
                _message.TabStop = false;
                _message.WordWrap = true;

                _footer.Dock = DockStyle.Bottom;
                _footer.FlowDirection = FlowDirection.RightToLeft;
                _footer.WrapContents = false;

                _content.Dock = DockStyle.Fill;
                _content.Controls.Add(_message);
                _content.Controls.Add(_title);
                _content.Paint += PaintIcon;

                Controls.Add(_content);
                Controls.Add(_footer);

                var definitions = ButtonsFor(buttons);
                var buttonsCreated = new List<Button>();
                // Right-to-left flow: add in reverse so the first button is leftmost.
                for (var i = definitions.Count - 1; i >= 0; i--)
                {
                    var (label, result) = definitions[i];
                    var button = new Button { Text = label, DialogResult = result, AutoSize = false };
                    _footer.Controls.Add(button);
                    buttonsCreated.Insert(0, button);
                }
                var defaultIndex = Math.Min(DefaultIndex(defaultButton), buttonsCreated.Count - 1);
                AcceptButton = buttonsCreated[defaultIndex];
                var cancel = buttonsCreated.Find(b => b.DialogResult == DialogResult.Cancel || b.DialogResult == DialogResult.No)
                    ?? (buttonsCreated.Count == 1 ? buttonsCreated[0] : null);
                if (cancel != null) CancelButton = cancel;
                ActiveControl = buttonsCreated[defaultIndex];
            }

            protected override void OnLoad(EventArgs e)
            {
                base.OnLoad(e);
                var p = Theme.Current;
                _content.BackColor = p.Surface;
                _footer.BackColor = p.Window;
                _title.ForeColor = p.Text;
                _title.BackColor = p.Surface;
                _message.BackColor = p.Surface;
                _message.ForeColor = p.Text;
                LayoutDialog();
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.Control && e.KeyCode == Keys.C && _message.SelectionLength == 0)
                {
                    Clipboard.SetText($"{_caption}{Environment.NewLine}{Environment.NewLine}{_text}");
                    e.Handled = true;
                }
            }

            private void LayoutDialog()
            {
                int Px(int value) => LogicalToDeviceUnits(value);
                var iconSpace = _icon == MessageBoxIcon.None ? 0 : Px(44);
                var width = Px(460);
                var left = Px(24) + iconSpace;
                var textWidth = width - left - Px(24);

                _title.SetBounds(left, Px(22), textWidth, Px(30));
                var measured = TextRenderer.MeasureText(_message.Text, _message.Font, new Size(textWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                var maxHeight = Px(360);
                var messageHeight = Math.Min(measured.Height + Px(4), maxHeight);
                if (measured.Height + Px(4) > maxHeight) _message.ScrollBars = ScrollBars.Vertical;
                _message.SetBounds(left, Px(58), textWidth, messageHeight);

                var buttonWidth = Px(110);
                foreach (Control button in _footer.Controls)
                {
                    button.Size = new Size(buttonWidth, Px(32));
                    button.Margin = new Padding(Px(8), 0, 0, 0);
                }
                _footer.Padding = new Padding(Px(24), Px(20), Px(24), Px(20));
                _footer.Height = Px(32) + Px(40);

                var contentHeight = Px(58) + messageHeight + Px(24);
                ClientSize = new Size(width, contentHeight + _footer.Height);
            }

            private void PaintIcon(object sender, PaintEventArgs e)
            {
                if (_icon == MessageBoxIcon.None) return;
                var p = Theme.Current;
                var (glyph, color) = _icon switch
                {
                    MessageBoxIcon.Error => ("\uE783", p.Critical),
                    MessageBoxIcon.Warning => (Glyph.Warning, p.Caution),
                    MessageBoxIcon.Question => ("\uE9CE", p.AccentText),
                    _ => (Glyph.Info, p.AccentText)
                };
                var size = LogicalToDeviceUnits(28);
                FluentIcons.Draw(e.Graphics, glyph, color, new RectangleF(LogicalToDeviceUnits(22), LogicalToDeviceUnits(22), size, size), size);
            }

            private static int DefaultIndex(MessageBoxDefaultButton button) => button switch
            {
                MessageBoxDefaultButton.Button2 => 1,
                MessageBoxDefaultButton.Button3 => 2,
                _ => 0
            };

            private static List<(string, DialogResult)> ButtonsFor(MessageBoxButtons buttons) => buttons switch
            {
                MessageBoxButtons.OKCancel => new List<(string, DialogResult)> { ("OK", DialogResult.OK), ("Cancel", DialogResult.Cancel) },
                MessageBoxButtons.YesNo => new List<(string, DialogResult)> { ("Yes", DialogResult.Yes), ("No", DialogResult.No) },
                MessageBoxButtons.YesNoCancel => new List<(string, DialogResult)> { ("Yes", DialogResult.Yes), ("No", DialogResult.No), ("Cancel", DialogResult.Cancel) },
                MessageBoxButtons.RetryCancel => new List<(string, DialogResult)> { ("Retry", DialogResult.Retry), ("Cancel", DialogResult.Cancel) },
                MessageBoxButtons.AbortRetryIgnore => new List<(string, DialogResult)> { ("Abort", DialogResult.Abort), ("Retry", DialogResult.Retry), ("Ignore", DialogResult.Ignore) },
                _ => new List<(string, DialogResult)> { ("OK", DialogResult.OK) }
            };
        }
    }
}
