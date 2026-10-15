using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LCS.Theming
{
    internal static class AppLogo
    {
        private static readonly Color TileLight = Color.FromArgb(0x3A, 0x96, 0xDD);
        private static readonly Color TileDark = Color.FromArgb(0x0B, 0x55, 0xA8);

        public static Bitmap Render(int size)
        {
            var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);
            Draw(g, new RectangleF(0, 0, size, size));
            return bitmap;
        }

        public static void Draw(Graphics g, RectangleF bounds)
        {
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var size = Math.Min(bounds.Width, bounds.Height);
            var tile = new RectangleF(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size);
            using (var path = Theme.RoundedRect(tile, size * 0.22f))
            using (var brush = new LinearGradientBrush(tile, TileLight, TileDark, 45f))
            {
                g.FillPath(brush, path);
            }

            float X(float fraction) => tile.X + tile.Width * fraction;
            float Y(float fraction) => tile.Y + tile.Height * fraction;
            float R(float fraction) => size * fraction;

            using var white = new SolidBrush(Color.White);
            if (size <= 24)
            {
                // Small sizes: two bold gears stay readable.
                FillGear(g, white, X(0.42f), Y(0.44f), R(0.30f), 8, R(0.08f), R(0.11f));
                FillGear(g, white, X(0.74f), Y(0.73f), R(0.17f), 6, R(0.06f), R(0.06f));
            }
            else
            {
                FillGear(g, white, X(0.60f), Y(0.36f), R(0.24f), 10, R(0.055f), R(0.12f));
                FillGear(g, white, X(0.28f), Y(0.57f), R(0.17f), 8, R(0.05f), R(0.075f));
                FillGear(g, white, X(0.62f), Y(0.74f), R(0.145f), 8, R(0.045f), R(0.06f));
            }
            g.SmoothingMode = smoothing;
        }

        private static void FillGear(Graphics g, Brush brush, float cx, float cy, float radius, int teeth, float toothDepth, float holeRadius)
        {
            using var path = new GraphicsPath(FillMode.Alternate);
            var points = new PointF[teeth * 4];
            var root = radius - toothDepth;
            var step = (float)(Math.PI * 2 / teeth);
            for (var i = 0; i < teeth; i++)
            {
                var a = i * step - Math.PI / 2;
                points[i * 4] = Polar(cx, cy, root, a - step * 0.30);
                points[i * 4 + 1] = Polar(cx, cy, radius, a - step * 0.17);
                points[i * 4 + 2] = Polar(cx, cy, radius, a + step * 0.17);
                points[i * 4 + 3] = Polar(cx, cy, root, a + step * 0.30);
            }
            path.AddPolygon(points);
            path.AddEllipse(cx - holeRadius, cy - holeRadius, holeRadius * 2, holeRadius * 2);
            g.FillPath(brush, path);
        }

        private static PointF Polar(float cx, float cy, float radius, double angle)
        {
            return new PointF(cx + (float)(Math.Cos(angle) * radius), cy + (float)(Math.Sin(angle) * radius));
        }
    }
}
