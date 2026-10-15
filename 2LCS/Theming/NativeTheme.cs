using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LCS.Theming
{
    internal static class NativeTheme
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        private const int DWMWCP_ROUND = 2;
        private const int DWMWCP_ROUNDSMALL = 3;

        public static void ApplyWindowChrome(Form form, Palette palette)
        {
            if (!form.IsHandleCreated) return;
            var handle = form.Handle;
            SetInt(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, palette.IsDark ? 1 : 0);
            SetInt(handle, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND);
            SetInt(handle, DWMWA_CAPTION_COLOR, ColorTranslator.ToWin32(palette.Window));
            SetInt(handle, DWMWA_TEXT_COLOR, ColorTranslator.ToWin32(palette.Text));
        }

        public static void ApplyPopupChrome(Control popup, Palette palette)
        {
            if (!popup.IsHandleCreated) return;
            SetInt(popup.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUNDSMALL);
            SetInt(popup.Handle, DWMWA_BORDER_COLOR, ColorTranslator.ToWin32(palette.Stroke));
        }

        public static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

        private static void SetInt(IntPtr handle, int attribute, int value)
        {
            try
            {
                DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
            }
            catch (Exception)
            {
                // dwmapi not available or attribute unsupported.
            }
        }

        [DllImport("user32.dll")]
        internal static extern int GetDpiForSystem();

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
