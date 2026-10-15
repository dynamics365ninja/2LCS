using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

namespace LCS.Theming
{
    internal enum ThemeMode
    {
        System,
        Light,
        Dark
    }

    internal sealed class Palette
    {
        public bool IsDark { get; init; }

        public Color Window { get; init; }
        public Color Surface { get; init; }
        public Color SurfaceAlt { get; init; }
        public Color Control { get; init; }
        public Color ControlHover { get; init; }
        public Color ControlPressed { get; init; }
        public Color ControlStroke { get; init; }
        public Color Stroke { get; init; }
        public Color Divider { get; init; }
        public Color Text { get; init; }
        public Color TextSecondary { get; init; }
        public Color TextDisabled { get; init; }
        public Color Accent { get; init; }
        public Color AccentHover { get; init; }
        public Color AccentPressed { get; init; }
        public Color OnAccent { get; init; }
        public Color AccentText { get; init; }
        public Color HoverOverlay { get; init; }
        public Color PressedOverlay { get; init; }
        public Color Selection { get; init; }
        public Color SelectionHover { get; init; }
        public Color RowHover { get; init; }
        public Color Success { get; init; }
        public Color Caution { get; init; }
        public Color Critical { get; init; }
        public Color Neutral { get; init; }

        public static Palette Light(Color accent)
        {
            var surface = Color.White;
            return new Palette
            {
                IsDark = false,
                Window = Color.FromArgb(0xF3, 0xF3, 0xF3),
                Surface = surface,
                SurfaceAlt = Color.FromArgb(0xF5, 0xF5, 0xF5),
                Control = Color.FromArgb(0xFB, 0xFB, 0xFB),
                ControlHover = Color.FromArgb(0xF6, 0xF6, 0xF6),
                ControlPressed = Color.FromArgb(0xF0, 0xF0, 0xF0),
                ControlStroke = Color.FromArgb(0xD6, 0xD6, 0xD6),
                Stroke = Color.FromArgb(0xE5, 0xE5, 0xE5),
                Divider = Color.FromArgb(0xEE, 0xEE, 0xEE),
                Text = Color.FromArgb(0x1A, 0x1A, 0x1A),
                TextSecondary = Color.FromArgb(0x5F, 0x5F, 0x5F),
                TextDisabled = Color.FromArgb(0xA0, 0xA0, 0xA0),
                Accent = accent,
                AccentHover = Theme.Blend(accent, Color.White, 0.10),
                AccentPressed = Theme.Blend(accent, Color.White, 0.22),
                OnAccent = Theme.ReadableOn(accent),
                AccentText = accent,
                HoverOverlay = Color.FromArgb(14, 0, 0, 0),
                PressedOverlay = Color.FromArgb(8, 0, 0, 0),
                Selection = Theme.Blend(surface, accent, 0.13),
                SelectionHover = Theme.Blend(surface, accent, 0.18),
                RowHover = Color.FromArgb(0xF7, 0xF7, 0xF7),
                Success = Color.FromArgb(0x0F, 0x7B, 0x0F),
                Caution = Color.FromArgb(0x9D, 0x5D, 0x00),
                Critical = Color.FromArgb(0xC4, 0x2B, 0x1C),
                Neutral = Color.FromArgb(0x70, 0x70, 0x70)
            };
        }

        public static Palette Dark(Color accent)
        {
            var surface = Color.FromArgb(0x2B, 0x2B, 0x2B);
            return new Palette
            {
                IsDark = true,
                Window = Color.FromArgb(0x20, 0x20, 0x20),
                Surface = surface,
                SurfaceAlt = Color.FromArgb(0x38, 0x38, 0x38),
                Control = Color.FromArgb(0x2D, 0x2D, 0x2D),
                ControlHover = Color.FromArgb(0x32, 0x32, 0x32),
                ControlPressed = Color.FromArgb(0x27, 0x27, 0x27),
                ControlStroke = Color.FromArgb(0x48, 0x48, 0x48),
                Stroke = Color.FromArgb(0x3A, 0x3A, 0x3A),
                Divider = Color.FromArgb(0x35, 0x35, 0x35),
                Text = Color.White,
                TextSecondary = Color.FromArgb(0xC5, 0xC5, 0xC5),
                TextDisabled = Color.FromArgb(0x78, 0x78, 0x78),
                Accent = accent,
                AccentHover = Theme.Blend(accent, Color.Black, 0.10),
                AccentPressed = Theme.Blend(accent, Color.Black, 0.20),
                OnAccent = Theme.ReadableOn(accent),
                AccentText = accent,
                HoverOverlay = Color.FromArgb(15, 255, 255, 255),
                PressedOverlay = Color.FromArgb(9, 255, 255, 255),
                Selection = Theme.Blend(surface, accent, 0.22),
                SelectionHover = Theme.Blend(surface, accent, 0.28),
                RowHover = Color.FromArgb(0x32, 0x32, 0x32),
                Success = Color.FromArgb(0x6C, 0xCB, 0x5F),
                Caution = Color.FromArgb(0xFC, 0xE1, 0x00),
                Critical = Color.FromArgb(0xFF, 0x99, 0xA4),
                Neutral = Color.FromArgb(0x9D, 0x9D, 0x9D)
            };
        }
    }

    internal static class Theme
    {
        private static readonly Color DefaultLightAccent = Color.FromArgb(0x00, 0x5F, 0xB8);
        private static readonly Color DefaultDarkAccent = Color.FromArgb(0x60, 0xCD, 0xFF);

        private static Font _uiFont;
        private static Font _uiSemibold;
        private static Font _titleFont;
        private static Font _monoFont;

        public static Palette Current { get; private set; } = Palette.Light(DefaultLightAccent);

        public static ThemeMode Mode { get; private set; } = ThemeMode.System;

        public static string IconFontName { get; private set; } = "Segoe MDL2 Assets";

        public static bool HasFluentIcons { get; private set; }

        public static Font UiFont => _uiFont ??= new Font("Segoe UI", 9f);

        public static Font UiSemibold => _uiSemibold ??= new Font("Segoe UI Semibold", 9f);

        public static Font TitleFont => _titleFont ??= new Font(HasFont("Segoe UI Variable Display Semib") ? "Segoe UI Variable Display Semib" : "Segoe UI Semibold", 13f);

        // Point sizes are converted with the system DPI; on a monitor with another DPI owner-drawn text needs a scaled font.
        public static Font ForDpi(Font font, int dpi)
        {
            var system = SystemDpi;
            if (dpi <= 0 || dpi == system) return font;
            lock (DpiFonts)
            {
                if (!DpiFonts.TryGetValue((font, dpi), out var scaled))
                {
                    DpiFonts[(font, dpi)] = scaled = new Font(font.FontFamily, font.SizeInPoints * dpi / system, font.Style, GraphicsUnit.Point);
                }
                return scaled;
            }
        }

        private static readonly Dictionary<(Font, int), Font> DpiFonts = new Dictionary<(Font, int), Font>();
        private static int _systemDpi;

        private static int SystemDpi => _systemDpi > 0 ? _systemDpi : _systemDpi = NativeTheme.GetDpiForSystem();

        public static Font MonoFont => _monoFont ??= HasFont("Cascadia Mono") ? new Font("Cascadia Mono", 9.5f) : new Font("Consolas", 10f);

        public static void Initialize()
        {
            Mode = Enum.TryParse(Properties.Settings.Default.theme, true, out ThemeMode mode) ? mode : ThemeMode.System;
            var dark = Mode == ThemeMode.Dark || (Mode == ThemeMode.System && WindowsUsesDarkApps());
            var accent = ReadAccentColor(dark) ?? (dark ? DefaultDarkAccent : DefaultLightAccent);
            Current = dark ? Palette.Dark(accent) : Palette.Light(accent);

            HasFluentIcons = HasFont("Segoe Fluent Icons");
            IconFontName = HasFluentIcons ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        }

        public static bool WindowsUsesDarkApps()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Color? ReadAccentColor(bool dark)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
                if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 32)
                {
                    // Shades 0-2 light, 3 base, 4-6 dark; Windows uses Light2 in dark mode, Dark1 in light mode.
                    var index = dark ? 1 : 4;
                    return Color.FromArgb(palette[index * 4], palette[index * 4 + 1], palette[index * 4 + 2]);
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public static bool HasFont(string familyName)
        {
            using var fonts = new InstalledFontCollection();
            return fonts.Families.Any(f => f.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));
        }

        public static Size MeasureText(IDeviceContext dc, string text, Font font, System.Windows.Forms.TextFormatFlags flags)
        {
            flags &= ~(System.Windows.Forms.TextFormatFlags.EndEllipsis | System.Windows.Forms.TextFormatFlags.PathEllipsis
                | System.Windows.Forms.TextFormatFlags.WordEllipsis | System.Windows.Forms.TextFormatFlags.WordBreak);
            return System.Windows.Forms.TextRenderer.MeasureText(dc, text, font, new Size(int.MaxValue, int.MaxValue), flags);
        }

        public static Color Blend(Color from, Color to, double amount)
        {
            int Mix(int a, int b) => (int)Math.Round(a + (b - a) * amount);
            return Color.FromArgb(Mix(from.R, to.R), Mix(from.G, to.G), Mix(from.B, to.B));
        }

        public static Color WithAlpha(Color color, int alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

        public static Color ReadableOn(Color background)
        {
            var luminance = (0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B) / 255;
            return luminance > 0.55 ? Color.Black : Color.White;
        }

        public static int Scale(int value, int dpi) => (int)Math.Round(value * dpi / 96.0);

        public static float Scale(float value, int dpi) => value * dpi / 96f;

        public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            var d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (d <= 0.5f)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Color color, RectangleF bounds, float radius)
        {
            using var path = RoundedRect(bounds, radius);
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }

        public static void DrawRounded(Graphics g, Color color, RectangleF bounds, float radius, float width = 1f)
        {
            using var path = RoundedRect(bounds, radius);
            using var pen = new Pen(color, width);
            g.DrawPath(pen, path);
        }
    }
}
