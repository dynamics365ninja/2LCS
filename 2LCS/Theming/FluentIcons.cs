using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;

namespace LCS.Theming
{
    internal static class Glyph
    {
        public const string Person = "";
        public const string Switch = "";
        public const string Refresh = "";
        public const string Link = "";
        public const string Export = "";
        public const string SignOut = "";
        public const string Key = "";
        public const string Settings = "";
        public const string Info = "";
        public const string OpenInNew = "";
        public const string Monitor = "";
        public const string Play = "";
        public const string Stop = "";
        public const string Power = "";
        public const string Delete = "";
        public const string Package = "";
        public const string History = "";
        public const string Globe = "";
        public const string Repair = "";
        public const string Command = "";
        public const string Calendar = "";
        public const string Shield = "";
        public const string Restart = "";
        public const string Folder = "";
        public const string Cloud = "";
        public const string Search = "";
        public const string ChevronDown = "";
        public const string ChevronUp = "";
        public const string ChevronRight = "";
        public const string CheckMark = "";
        public const string Document = "";
        public const string PasswordShow = "";
        public const string Close = "";
        public const string Manage = "";
        public const string Warning = "";
    }

    internal static class FluentIcons
    {
        private static readonly Dictionary<(string, int, int), Bitmap> Cache = new Dictionary<(string, int, int), Bitmap>();

        // Glyphs missing in Segoe MDL2 Assets.
        private static readonly Dictionary<string, string> Mdl2Fallbacks = new Dictionary<string, string>
        {
            [Glyph.SignOut] = Glyph.Power
        };

        public static Bitmap Get(string glyph, Color color, int pixelSize)
        {
            glyph = Resolve(glyph);
            var key = (glyph, color.ToArgb(), pixelSize);
            lock (Cache)
            {
                if (Cache.TryGetValue(key, out var cached)) return cached;
                var bitmap = new Bitmap(pixelSize, pixelSize);
                using (var g = Graphics.FromImage(bitmap))
                {
                    Draw(g, glyph, color, new RectangleF(0, 0, pixelSize, pixelSize), pixelSize);
                }
                Cache[key] = bitmap;
                return bitmap;
            }
        }

        public static void Draw(Graphics g, string glyph, Color color, RectangleF bounds, float pixelSize)
        {
            glyph = Resolve(glyph);
            var previousHint = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var font = new Font(Theme.IconFontName, pixelSize, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat(StringFormat.GenericTypographic))
            {
                // GenericTypographic has LineLimit: a glyph taller than the bounds would not be drawn.
                format.FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.NoWrap;
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                g.DrawString(glyph, font, brush, bounds, format);
            }
            g.TextRenderingHint = previousHint;
        }

        private static string Resolve(string glyph)
        {
            return !Theme.HasFluentIcons && Mdl2Fallbacks.TryGetValue(glyph, out var fallback) ? fallback : glyph;
        }
    }
}
