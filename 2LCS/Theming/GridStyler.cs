using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LCS.Theming
{
    internal sealed class GridOptions
    {
        public ISet<string> StatusColumns { get; init; } = new HashSet<string>();

        public Func<(string Title, string Hint)> EmptyState { get; init; }

        public string EmptyGlyph { get; init; } = Glyph.Cloud;

        public ISet<string> WarningColumns { get; init; } = new HashSet<string>();

        public Func<object, bool> IsWarning { get; init; }
    }

    internal static class GridStyler
    {
        private enum StatusKind { Neutral, Success, Info, Caution, Critical }

        private sealed class State
        {
            public int HoverRow = -1;
            public GridOptions Options = new GridOptions();
        }

        private static readonly ConditionalWeakTable<DataGridView, State> States = new ConditionalWeakTable<DataGridView, State>();
        private static Font _emptyTitleFont;

        public static void Apply(DataGridView grid, GridOptions options = null)
        {
            if (States.TryGetValue(grid, out var existing))
            {
                if (options != null)
                {
                    existing.Options = options;
                    PadStatusColumns(grid, options);
                }
                // Re-applying (theme switch) refreshes the colors.
                ApplyColors(grid);
                grid.Invalidate();
                return;
            }
            var state = new State { Options = options ?? new GridOptions() };
            States.Add(grid, state);

            grid.BorderStyle = BorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.AllowUserToResizeRows = false;
            var header = grid.ColumnHeadersDefaultCellStyle;
            header.Alignment = DataGridViewContentAlignment.MiddleLeft;
            header.WrapMode = DataGridViewTriState.False;
            if (grid.RowHeadersVisible)
            {
                grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            }
            ApplyMetrics(grid);
            grid.DpiChangedAfterParent += (sender, e) => ApplyMetrics((DataGridView)sender);
            ApplyColors(grid);

            typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(grid, true);

            PadStatusColumns(grid, state.Options);
            grid.CellPainting += OnCellPainting;
            grid.Paint += OnPaint;
            grid.CellMouseEnter += (sender, e) => SetHoverRow((DataGridView)sender, e.RowIndex);
            grid.MouseLeave += (sender, e) => SetHoverRow((DataGridView)sender, -1);
            grid.DataBindingComplete += (sender, e) => ((Control)sender).Invalidate();
            grid.RowsRemoved += (sender, e) => ((Control)sender).Invalidate();
        }

        private static void ApplyMetrics(DataGridView grid)
        {
            var s = grid.DeviceDpi / 96f;
            int Px(float value) => (int)Math.Round(value * s);

            grid.ColumnHeadersHeight = Px(36);
            grid.RowTemplate.Height = Px(32);
            foreach (DataGridViewRow row in grid.Rows)
            {
                row.Height = grid.RowTemplate.Height;
            }
            grid.DefaultCellStyle.Padding = new Padding(Px(6), 0, Px(6), 0);
            grid.ColumnHeadersDefaultCellStyle.Font = Theme.ForDpi(Theme.UiSemibold, grid.DeviceDpi);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(Px(6), 0, Px(6), 0);
            if (grid.RowHeadersVisible) grid.RowHeadersWidth = Math.Max(4, Px(22));
        }

        private static void ApplyColors(DataGridView grid)
        {
            var p = Theme.Current;
            grid.BackgroundColor = p.Surface;
            grid.GridColor = p.Divider;

            var cell = grid.DefaultCellStyle;
            cell.BackColor = p.Surface;
            cell.ForeColor = p.Text;
            cell.SelectionBackColor = p.Selection;
            cell.SelectionForeColor = p.Text;
            grid.AlternatingRowsDefaultCellStyle.BackColor = p.Surface;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = p.Selection;

            var header = grid.ColumnHeadersDefaultCellStyle;
            header.BackColor = p.Surface;
            header.ForeColor = p.TextSecondary;
            header.SelectionBackColor = p.Surface;
            header.SelectionForeColor = p.TextSecondary;

            var rowHeader = grid.RowHeadersDefaultCellStyle;
            rowHeader.BackColor = p.Surface;
            rowHeader.SelectionBackColor = p.Selection;
            rowHeader.ForeColor = p.TextSecondary;
        }

        private static void PadStatusColumns(DataGridView grid, GridOptions options)
        {
            var s = grid.DeviceDpi / 96f;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!options.StatusColumns.Contains(column.Name)) continue;
                // Room for the warning marker after the pill.
                var right = options.WarningColumns.Contains(column.Name) ? 38 : 14;
                column.DefaultCellStyle.Padding = new Padding((int)(28 * s), 0, (int)(right * s), 0);
            }
        }

        private static void SetHoverRow(DataGridView grid, int rowIndex)
        {
            if (!States.TryGetValue(grid, out var state) || state.HoverRow == rowIndex) return;
            var previous = state.HoverRow;
            state.HoverRow = rowIndex;
            if (previous >= 0 && previous < grid.RowCount) grid.InvalidateRow(previous);
            if (rowIndex >= 0 && rowIndex < grid.RowCount) grid.InvalidateRow(rowIndex);
        }

        private static void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var grid = (DataGridView)sender;
            if (!States.TryGetValue(grid, out var state)) return;
            var p = Theme.Current;
            var s = grid.DeviceDpi / 96f;
            var g = e.Graphics;

            if (e.RowIndex == -1)
            {
                PaintColumnHeader(grid, e, p, s);
                e.Handled = true;
                return;
            }

            var row = grid.Rows[e.RowIndex];
            var hovered = state.HoverRow == e.RowIndex;
            if (hovered)
            {
                e.CellStyle.BackColor = p.RowHover;
                e.CellStyle.SelectionBackColor = p.SelectionHover;
            }

            if (e.ColumnIndex == -1)
            {
                var back = row.Selected ? e.CellStyle.SelectionBackColor : e.CellStyle.BackColor;
                using (var brush = new SolidBrush(back)) g.FillRectangle(brush, e.CellBounds);
                using (var pen = new Pen(p.Divider)) g.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                if (row.Selected)
                {
                    // Windows 11 list selection indicator.
                    var height = 16 * s;
                    var indicator = new RectangleF(e.CellBounds.Left + 4 * s, e.CellBounds.Top + (e.CellBounds.Height - height) / 2, 3 * s, height);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    Theme.FillRounded(g, p.Accent, indicator, 1.5f * s);
                    g.SmoothingMode = SmoothingMode.Default;
                }
                e.Handled = true;
                return;
            }

            var column = grid.Columns[e.ColumnIndex];
            if (state.Options.StatusColumns.Contains(column.Name) && e.FormattedValue is string status && status.Length > 0)
            {
                e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);
                var pillWidth = PaintStatusPill(g, e.CellBounds, status, e.CellStyle.Font, p, s);
                if (state.Options.WarningColumns.Contains(column.Name) && state.Options.IsWarning?.Invoke(row.DataBoundItem) == true)
                {
                    var glyph = new RectangleF(e.CellBounds.X + 6 * s + pillWidth + 6 * s, e.CellBounds.Y, 16 * s, e.CellBounds.Height);
                    FluentIcons.Draw(g, Glyph.Warning, p.Caution, glyph, 14 * s);
                }
                e.Handled = true;
                return;
            }

            // Everything except the dotted focus rectangle.
            e.Paint(e.ClipBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.Focus);
            e.Handled = true;
        }

        private static void PaintColumnHeader(DataGridView grid, DataGridViewCellPaintingEventArgs e, Palette p, float s)
        {
            var g = e.Graphics;
            var bounds = e.CellBounds;
            using (var brush = new SolidBrush(p.Surface)) g.FillRectangle(brush, bounds);

            if (e.ColumnIndex >= 0)
            {
                var column = grid.Columns[e.ColumnIndex];
                var padding = e.CellStyle.Padding;
                var textBounds = new Rectangle(bounds.X + padding.Left, bounds.Y, bounds.Width - padding.Horizontal, bounds.Height);
                var sort = column.HeaderCell.SortGlyphDirection;
                if (sort != SortOrder.None)
                {
                    var glyphSize = 12 * s;
                    var glyphBounds = new RectangleF(textBounds.Right - glyphSize, bounds.Y, glyphSize, bounds.Height);
                    FluentIcons.Draw(g, sort == SortOrder.Ascending ? Glyph.ChevronUp : Glyph.ChevronDown, p.TextSecondary, glyphBounds, 10 * s);
                    textBounds.Width -= (int)(glyphSize + 4 * s);
                }
                TextRenderer.DrawText(g, column.HeaderText, e.CellStyle.Font, textBounds, p.TextSecondary,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

                using var separator = new Pen(p.Divider);
                var inset = (int)(10 * s);
                g.DrawLine(separator, bounds.Right - 1, bounds.Top + inset, bounds.Right - 1, bounds.Bottom - inset);
            }

            using var pen = new Pen(p.Stroke);
            g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
        }

        private static float PaintStatusPill(Graphics g, Rectangle cell, string status, Font font, Palette p, float s)
        {
            return DrawStatusPill(g, cell.X + 6 * s, cell.Y + cell.Height / 2f, status, font, s, cell.Width - 12 * s);
        }

        internal static float DrawStatusPill(Graphics g, float x, float centerY, string status, Font font, float s, float maxWidth)
        {
            var p = Theme.Current;
            var color = Classify(status) switch
            {
                StatusKind.Success => p.Success,
                StatusKind.Info => p.AccentText,
                StatusKind.Caution => p.Caution,
                StatusKind.Critical => p.Critical,
                _ => p.Neutral
            };

            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis
                | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            var textWidth = Theme.MeasureText(g, status, font, flags).Width;
            var height = 22 * s;
            var dot = 6 * s;
            var padX = 9 * s;
            var gap = 6 * s;
            var width = Math.Min(padX + dot + gap + textWidth + padX, maxWidth);
            if (width <= dot + padX * 2) return 0;
            var pill = new RectangleF(x, centerY - height / 2f, width, height);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.FillRounded(g, Theme.WithAlpha(color, p.IsDark ? 46 : 30), pill, height / 2);
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, pill.X + padX, pill.Y + (height - dot) / 2, dot, dot);
            }
            g.SmoothingMode = SmoothingMode.Default;

            var textLeft = (int)(pill.X + padX + dot + gap);
            var textBounds = new Rectangle(textLeft, (int)pill.Y, (int)(pill.Right - padX / 2) - textLeft, (int)Math.Ceiling(height));
            TextRenderer.DrawText(g, status, font, textBounds, p.Text, flags);
            return width;
        }

        private static StatusKind Classify(string status)
        {
            var value = status.Trim().ToLowerInvariant();
            if (value.Contains("fail") || value.Contains("error")) return StatusKind.Critical;
            if (value.Contains("billed")) return StatusKind.Caution;
            if (value == "running") return StatusKind.Success;
            if (value.StartsWith("partly")) return StatusKind.Caution;
            if (value.Split(' ').Any(word => word.EndsWith("ing", StringComparison.Ordinal))) return StatusKind.Info;
            if (value.Contains("queue") || value.Contains("pending") || value.Contains("incomplete") || value.Contains("warning")) return StatusKind.Caution;
            if (value.StartsWith("not ") || value.Contains("stop") || value.Contains("dealloc") || value.Contains("delete") || value.Contains("cancel")) return StatusKind.Neutral;
            if (value.Contains("deployed") || value.Contains("running") || value.Contains("started") || value.Contains("succe") || value == "active") return StatusKind.Success;
            return StatusKind.Neutral;
        }

        private static void OnPaint(object sender, PaintEventArgs e)
        {
            var grid = (DataGridView)sender;
            if (!States.TryGetValue(grid, out var state)) return;
            if (grid.Rows.GetRowCount(DataGridViewElementStates.Visible) > 0) return;

            var p = Theme.Current;
            var s = grid.DeviceDpi / 96f;
            var (title, hint) = state.Options.EmptyState?.Invoke() ?? ("Nothing to show", null);
            if (string.IsNullOrEmpty(title)) return;

            var area = grid.ClientRectangle;
            if (grid.ColumnHeadersVisible)
            {
                area.Y += grid.ColumnHeadersHeight;
                area.Height -= grid.ColumnHeadersHeight;
            }
            if (area.Height < 80 * s) return;

            var g = e.Graphics;
            _emptyTitleFont ??= new Font("Segoe UI Semibold", 11f);
            var glyphSize = 40 * s;
            var titleHeight = TextRenderer.MeasureText(g, title, Theme.ForDpi(_emptyTitleFont, grid.DeviceDpi)).Height;
            var hintHeight = string.IsNullOrEmpty(hint) ? 0 : TextRenderer.MeasureText(g, hint, grid.Font).Height;
            var total = glyphSize + 14 * s + titleHeight + 4 * s + hintHeight;
            var top = area.Y + (area.Height - total) / 2;

            FluentIcons.Draw(g, state.Options.EmptyGlyph, p.TextDisabled, new RectangleF(area.X, top, area.Width, glyphSize), glyphSize);
            top += glyphSize + 14 * s;
            const TextFormatFlags centered = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
            TextRenderer.DrawText(g, title, Theme.ForDpi(_emptyTitleFont, grid.DeviceDpi), new Rectangle(area.X, (int)top, area.Width, titleHeight), p.Text, centered);
            top += titleHeight + 4 * s;
            if (!string.IsNullOrEmpty(hint))
            {
                TextRenderer.DrawText(g, hint, grid.Font, new Rectangle(area.X + (int)(24 * s), (int)top, area.Width - (int)(48 * s), hintHeight * 2), p.TextSecondary, centered);
            }
        }
    }
}
