using System.Drawing.Drawing2D;

namespace DashboardEnergie.WinForms;

internal sealed class ForecastTrendChart : Control
{
    private IReadOnlyList<ForecastPoint> _points = Array.Empty<ForecastPoint>();
    private string _emptyMessage = "Pas assez de donnees pour la prevision.";

    public ForecastTrendChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.TextPrimary;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    }

    public void SetPoints(IReadOnlyList<ForecastPoint> points)
    {
        _points = points;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        var graphics = eventArgs.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);

        var plotArea = new Rectangle(58, 20, Math.Max(180, Width - 88), Math.Max(150, Height - 80));

        using var axisPen = new Pen(UiTheme.BorderStrong, 1F);
        using var gridPen = new Pen(UiTheme.Border, 1F);
        using var textBrush = new SolidBrush(UiTheme.TextSecondary);
        using var historyPen = new Pen(UiTheme.Brand, 2.4F);
        using var forecastPen = new Pen(UiTheme.AccentIndigo, 2.2F) { DashStyle = DashStyle.Dash };
        using var rangeBrush = new SolidBrush(Color.FromArgb(36, UiTheme.AccentIndigo));
        using var markerBrush = new SolidBrush(UiTheme.AccentOrange);

        DrawGrid(graphics, plotArea, axisPen, gridPen, textBrush);

        if (_points.Count < 3)
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

        var low = _points.Min(point => point.Low);
        var high = _points.Max(point => point.High);
        var span = Math.Max(0.5, high - low);
        var minScale = low - (span * 0.12);
        var maxScale = high + (span * 0.12);

        var chartPoints = BuildPoints(plotArea, minScale, maxScale);
        var historyPoints = chartPoints.Where(point => !point.Source.IsPredicted).ToList();
        var forecastPoints = chartPoints.Where(point => point.Source.IsPredicted).ToList();

        DrawForecastRange(graphics, plotArea, chartPoints, minScale, maxScale, rangeBrush);

        if (historyPoints.Count > 1)
        {
            graphics.DrawLines(historyPen, historyPoints.Select(point => point.Location).ToArray());
        }

        if (forecastPoints.Count > 1)
        {
            graphics.DrawLines(forecastPen, forecastPoints.Select(point => point.Location).ToArray());
        }

        var boundary = chartPoints.FirstOrDefault(point => point.Source.IsPredicted);
        if (boundary is not null)
        {
            graphics.FillEllipse(markerBrush, boundary.Location.X - 4, boundary.Location.Y - 4, 8, 8);
        }

        DrawAxisLabels(graphics, plotArea, minScale, maxScale, chartPoints, textBrush);
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
            "kWh",
            SystemFonts.CaptionFont,
            new Rectangle(plotArea.Left - 48, plotArea.Top - 4, 44, 20),
            textBrush.Color,
            TextFormatFlags.Right);
    }

    private void DrawForecastRange(
        Graphics graphics,
        Rectangle plotArea,
        IReadOnlyList<ChartPoint> points,
        double minScale,
        double maxScale,
        SolidBrush fillBrush)
    {
        var forecast = points.Where(point => point.Source.IsPredicted).ToList();
        if (forecast.Count < 2)
        {
            return;
        }

        var upper = forecast
            .Select(point => new PointF(point.Location.X, ValueToY(plotArea, point.Source.High, minScale, maxScale)))
            .ToList();
        var lower = forecast
            .Select(point => new PointF(point.Location.X, ValueToY(plotArea, point.Source.Low, minScale, maxScale)))
            .Reverse()
            .ToList();

        var polygon = upper.Concat(lower).ToArray();
        graphics.FillPolygon(fillBrush, polygon);
    }

    private List<ChartPoint> BuildPoints(Rectangle plotArea, double minScale, double maxScale)
    {
        var count = Math.Max(1, _points.Count);
        var points = new List<ChartPoint>(count);

        for (var index = 0; index < count; index++)
        {
            var source = _points[index];
            var x = count == 1
                ? plotArea.Left + (plotArea.Width / 2f)
                : plotArea.Left + (plotArea.Width * index / (float)(count - 1));
            var y = ValueToY(plotArea, source.Value, minScale, maxScale);
            points.Add(new ChartPoint(source, new PointF(x, y)));
        }

        return points;
    }

    private void DrawAxisLabels(
        Graphics graphics,
        Rectangle plotArea,
        double minScale,
        double maxScale,
        IReadOnlyList<ChartPoint> points,
        SolidBrush textBrush)
    {
        var ticks = new[]
        {
            minScale,
            minScale + ((maxScale - minScale) * 0.25),
            minScale + ((maxScale - minScale) * 0.50),
            minScale + ((maxScale - minScale) * 0.75),
            maxScale
        };

        foreach (var tick in ticks)
        {
            var y = ValueToY(plotArea, tick, minScale, maxScale);
            TextRenderer.DrawText(
                graphics,
                tick.ToString("F1"),
                Font,
                new Rectangle(4, (int)y - 8, 46, 18),
                textBrush.Color,
                TextFormatFlags.Right);
        }

        var indexes = new[] { 0, Math.Max(0, points.Count / 2), points.Count - 1 };
        foreach (var index in indexes.Distinct())
        {
            if (index < 0 || index >= points.Count)
            {
                continue;
            }

            var point = points[index];
            TextRenderer.DrawText(
                graphics,
                point.Source.Label,
                Font,
                new Rectangle((int)point.Location.X - 32, plotArea.Bottom + 7, 72, 18),
                textBrush.Color,
                TextFormatFlags.HorizontalCenter);
        }
    }

    private static float ValueToY(Rectangle plotArea, double value, double minScale, double maxScale)
    {
        var denominator = Math.Max(0.00001, maxScale - minScale);
        var ratio = (value - minScale) / denominator;
        ratio = Math.Clamp(ratio, 0d, 1d);
        return plotArea.Bottom - (float)(ratio * plotArea.Height);
    }

    private sealed record ChartPoint(ForecastPoint Source, PointF Location);
}

internal sealed record ForecastPoint(
    string Label,
    double Value,
    double Low,
    double High,
    bool IsPredicted);
