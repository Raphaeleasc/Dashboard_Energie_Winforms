using DashboardEnergie.Shared;
using System.Drawing.Drawing2D;

namespace DashboardEnergie.WinForms;

internal sealed class PowerTrendChart : Control
{
    private IReadOnlyList<EnergyReadingDto> _readings = Array.Empty<EnergyReadingDto>();

    public PowerTrendChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        ForeColor = Color.FromArgb(47, 84, 62);
        Padding = new Padding(0);
    }

    public void SetReadings(IReadOnlyList<EnergyReadingDto> readings)
    {
        _readings = readings;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);

        var plotArea = new Rectangle(56, 20, Math.Max(140, Width - 80), Math.Max(120, Height - 70));

        using var axisPen = new Pen(Color.FromArgb(180, 188, 178), 1F);
        using var gridPen = new Pen(Color.FromArgb(231, 236, 228), 1F);
        using var linePen = new Pen(Color.FromArgb(47, 84, 62), 3F);
        using var fillBrush = new SolidBrush(Color.FromArgb(75, 86, 141, 106));
        using var textBrush = new SolidBrush(Color.FromArgb(88, 94, 88));

        DrawGrid(graphics, plotArea, axisPen, gridPen, textBrush);

        if (_readings.Count == 0)
        {
            TextRenderer.DrawText(
                graphics,
                "Aucune donnee disponible.",
                Font,
                plotArea,
                Color.FromArgb(100, 106, 100),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var maxValue = Math.Max(200d, _readings.Max(reading => reading.PowerWatts) * 1.15d);
        var points = BuildPoints(plotArea, maxValue);

        using var path = new GraphicsPath();
        path.AddLines(points);
        graphics.DrawLines(linePen, points);

        var areaPoints = points
            .Concat([new PointF(points[^1].X, plotArea.Bottom), new PointF(points[0].X, plotArea.Bottom)])
            .ToArray();
        graphics.FillPolygon(fillBrush, areaPoints);

        DrawAxisLabels(graphics, plotArea, maxValue, textBrush);
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
            "Watts",
            SystemFonts.CaptionFont,
            new Rectangle(plotArea.Left - 42, plotArea.Top - 4, 40, 20),
            textBrush.Color,
            TextFormatFlags.Right);
    }

    private PointF[] BuildPoints(Rectangle plotArea, double maxValue)
    {
        if (_readings.Count == 1)
        {
            return
            [
                new PointF(plotArea.Left, ValueToY(plotArea, _readings[0].PowerWatts, maxValue)),
                new PointF(plotArea.Right, ValueToY(plotArea, _readings[0].PowerWatts, maxValue))
            ];
        }

        var points = new PointF[_readings.Count];

        for (var index = 0; index < _readings.Count; index++)
        {
            var x = plotArea.Left + (plotArea.Width * index / (float)(_readings.Count - 1));
            points[index] = new PointF(x, ValueToY(plotArea, _readings[index].PowerWatts, maxValue));
        }

        return points;
    }

    private void DrawAxisLabels(Graphics graphics, Rectangle plotArea, double maxValue, SolidBrush textBrush)
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

        var labelIndexes = _readings.Count <= 4
            ? Enumerable.Range(0, _readings.Count).ToArray()
            : new[] { 0, _readings.Count / 3, (_readings.Count * 2) / 3, _readings.Count - 1 };

        foreach (var index in labelIndexes.Distinct())
        {
            var x = plotArea.Left + (plotArea.Width * index / (float)Math.Max(1, _readings.Count - 1));
            var label = _readings[index].Timestamp.ToString("HH:mm");
            TextRenderer.DrawText(
                graphics,
                label,
                Font,
                new Rectangle((int)x - 30, plotArea.Bottom + 6, 68, 18),
                textBrush.Color,
                TextFormatFlags.HorizontalCenter);
        }
    }

    private static float ValueToY(Rectangle plotArea, double value, double maxValue)
    {
        var ratio = maxValue <= 0d ? 0d : value / maxValue;
        ratio = Math.Clamp(ratio, 0d, 1d);
        return plotArea.Bottom - (float)(ratio * plotArea.Height);
    }
}
