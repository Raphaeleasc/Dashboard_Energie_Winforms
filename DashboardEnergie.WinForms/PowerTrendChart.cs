using DashboardEnergie.Shared;
using System.Drawing.Drawing2D;

namespace DashboardEnergie.WinForms;

internal sealed class PowerTrendChart : Control
{
    private IReadOnlyList<ChartPoint> _points = Array.Empty<ChartPoint>();
    private string _axisTitle = "Watts";
    private string _emptyMessage = "Aucune donnee disponible.";
    private double _minimumScale = 200d;
    private string _valueFormat = "F0";

    public PowerTrendChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Brand;
        Padding = new Padding(0);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    }

    public void SetReadings(IReadOnlyList<EnergyReadingDto> readings)
    {
        _points = readings
            .Select(reading => new ChartPoint(reading.Timestamp.ToString("HH:mm"), reading.PowerWatts))
            .ToList();
        _axisTitle = "Watts";
        _emptyMessage = "Aucune mesure technique disponible.";
        _minimumScale = 200d;
        _valueFormat = "F0";
        Invalidate();
    }

    public void SetAggregations(IReadOnlyList<EnergyAggregationPointDto> aggregations)
    {
        _points = aggregations
            .Select(point => new ChartPoint(point.Label, point.ValueKwh))
            .ToList();
        _axisTitle = "kWh";
        _emptyMessage = "Aucune aggregation disponible.";
        _minimumScale = 1d;
        _valueFormat = "F2";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);

        var plotArea = new Rectangle(58, 22, Math.Max(140, Width - 86), Math.Max(120, Height - 76));

        using var axisPen = new Pen(UiTheme.BorderStrong, 1F);
        using var gridPen = new Pen(Color.FromArgb(233, 239, 235), 1F);
        using var linePen = new Pen(UiTheme.Brand, 2.8F);
        using var fillBrush = new SolidBrush(Color.FromArgb(56, UiTheme.Brand));
        using var textBrush = new SolidBrush(UiTheme.TextSecondary);

        DrawGrid(graphics, plotArea, axisPen, gridPen, textBrush, _axisTitle);

        if (_points.Count == 0)
        {
            TextRenderer.DrawText(
                graphics,
                _emptyMessage,
                Font,
                plotArea,
                UiTheme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var maxValue = Math.Max(_minimumScale, _points.Max(point => point.Value) * 1.15d);
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
        SolidBrush textBrush,
        string axisTitle)
    {
        graphics.DrawRectangle(axisPen, plotArea);

        for (var index = 1; index < 5; index++)
        {
            var y = plotArea.Top + (plotArea.Height / 5f * index);
            graphics.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
        }

        TextRenderer.DrawText(
            graphics,
            axisTitle,
            SystemFonts.CaptionFont,
            new Rectangle(plotArea.Left - 48, plotArea.Top - 4, 46, 20),
            textBrush.Color,
            TextFormatFlags.Right);
    }

    private PointF[] BuildPoints(Rectangle plotArea, double maxValue)
    {
        if (_points.Count == 1)
        {
            return
            [
                new PointF(plotArea.Left, ValueToY(plotArea, _points[0].Value, maxValue)),
                new PointF(plotArea.Right, ValueToY(plotArea, _points[0].Value, maxValue))
            ];
        }

        var points = new PointF[_points.Count];

        for (var index = 0; index < _points.Count; index++)
        {
            var x = plotArea.Left + (plotArea.Width * index / (float)(_points.Count - 1));
            points[index] = new PointF(x, ValueToY(plotArea, _points[index].Value, maxValue));
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
                tick.ToString(_valueFormat),
                Font,
                new Rectangle(4, (int)y - 8, 46, 18),
                textBrush.Color,
                TextFormatFlags.Right);
        }

        var labelIndexes = _points.Count <= 4
            ? Enumerable.Range(0, _points.Count).ToArray()
            : new[] { 0, _points.Count / 3, (_points.Count * 2) / 3, _points.Count - 1 };

        foreach (var index in labelIndexes.Distinct())
        {
            var x = plotArea.Left + (plotArea.Width * index / (float)Math.Max(1, _points.Count - 1));
            var label = _points[index].Label;
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

    private sealed record ChartPoint(string Label, double Value);
}
