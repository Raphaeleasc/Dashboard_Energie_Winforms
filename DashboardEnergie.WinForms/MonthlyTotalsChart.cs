using DashboardEnergie.Shared;
using System.Drawing.Drawing2D;

namespace DashboardEnergie.WinForms;

internal sealed class MonthlyTotalsChart : Control
{
    private IReadOnlyList<RseMonthlyBreakdownDto> _breakdowns = Array.Empty<RseMonthlyBreakdownDto>();

    public MonthlyTotalsChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.AccentBlue;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    }

    public void SetBreakdowns(IReadOnlyList<RseMonthlyBreakdownDto> breakdowns)
    {
        _breakdowns = breakdowns;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);

        var plotArea = new Rectangle(56, 20, Math.Max(160, Width - 84), Math.Max(140, Height - 74));

        using var axisPen = new Pen(UiTheme.BorderStrong, 1F);
        using var gridPen = new Pen(Color.FromArgb(234, 239, 244), 1F);
        using var barBrush = new SolidBrush(UiTheme.AccentBlue);
        using var lastBarBrush = new SolidBrush(UiTheme.AccentAmber);
        using var textBrush = new SolidBrush(UiTheme.TextSecondary);

        DrawGrid(graphics, plotArea, axisPen, gridPen, textBrush);

        if (_breakdowns.Count == 0)
        {
            TextRenderer.DrawText(
                graphics,
                "Aucune donnee RSE disponible.",
                Font,
                plotArea,
                UiTheme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var maxValue = Math.Max(100d, _breakdowns.Max(item => item.TotalKwh) * 1.15d);
        var slotWidth = plotArea.Width / Math.Max(1f, _breakdowns.Count);
        var barWidth = Math.Max(12f, slotWidth * 0.56f);

        for (var index = 0; index < _breakdowns.Count; index++)
        {
            var breakdown = _breakdowns[index];
            var heightRatio = breakdown.TotalKwh / maxValue;
            var barHeight = (float)(plotArea.Height * heightRatio);
            var x = plotArea.Left + (slotWidth * index) + ((slotWidth - barWidth) / 2f);
            var y = plotArea.Bottom - barHeight;
            var rectangle = new RectangleF(x, y, barWidth, barHeight);

            graphics.FillRectangle(index == _breakdowns.Count - 1 ? lastBarBrush : barBrush, rectangle);

            var monthLabel = breakdown.Month.Length >= 7
                ? breakdown.Month[5..7]
                : breakdown.Month;
            TextRenderer.DrawText(
                graphics,
                monthLabel,
                Font,
                new Rectangle((int)x - 8, plotArea.Bottom + 6, (int)barWidth + 16, 18),
                textBrush.Color,
                TextFormatFlags.HorizontalCenter);
        }

        DrawYAxisLabels(graphics, plotArea, maxValue, textBrush);
    }

    private static void DrawGrid(
        Graphics graphics,
        Rectangle plotArea,
        Pen axisPen,
        Pen gridPen,
        SolidBrush textBrush)
    {
        graphics.DrawRectangle(axisPen, plotArea);

        for (var index = 1; index < 5; index++)
        {
            var y = plotArea.Top + (plotArea.Height / 5f * index);
            graphics.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
        }

        TextRenderer.DrawText(
            graphics,
            "kWh / mois",
            SystemFonts.CaptionFont,
            new Rectangle(plotArea.Left - 50, plotArea.Top - 4, 46, 20),
            textBrush.Color,
            TextFormatFlags.Right);
    }

    private void DrawYAxisLabels(Graphics graphics, Rectangle plotArea, double maxValue, SolidBrush textBrush)
    {
        var ticks = new[] { 0d, maxValue * 0.25d, maxValue * 0.5d, maxValue * 0.75d, maxValue };
        foreach (var tick in ticks)
        {
            var y = ValueToY(plotArea, tick, maxValue);
            TextRenderer.DrawText(
                graphics,
                $"{tick:F0}",
                Font,
                new Rectangle(4, (int)y - 8, 46, 18),
                textBrush.Color,
                TextFormatFlags.Right);
        }
    }

    private static float ValueToY(Rectangle plotArea, double value, double maxValue)
    {
        var ratio = maxValue <= 0d ? 0d : value / maxValue;
        ratio = Math.Clamp(ratio, 0d, 1d);
        return plotArea.Bottom - (float)(ratio * plotArea.Height);
    }
}
