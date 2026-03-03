using DashboardEnergie.Shared;

namespace DashboardEnergie.WinForms;

public sealed class DashboardForm : Form
{
    private const string ApiBaseAddress = "http://localhost:5188/";

    private readonly DashboardApiClient _apiClient = new(ApiBaseAddress);
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 5000 };
    private readonly Label _currentPowerValue = CreateMetricValueLabel();
    private readonly Label _peakPowerValue = CreateMetricValueLabel();
    private readonly Label _lastHourValue = CreateMetricValueLabel();
    private readonly Label _todayValue = CreateMetricValueLabel();
    private readonly Label _thresholdValue = CreateMetricCaptionLabel();
    private readonly Label _anomaliesValue = CreateMetricCaptionLabel();
    private readonly Label _sourceValue = CreateMetricCaptionLabel();
    private readonly PowerTrendChart _powerChart = new();
    private readonly DataGridView _latestReadingsGrid = new();
    private readonly ListView _alertsList = new();
    private readonly ListView _aggregationList = new();
    private readonly ToolStripStatusLabel _statusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    private bool _refreshInProgress;

    public DashboardForm()
    {
        Text = "Dashboard Energie";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        ClientSize = new Size(1360, 860);
        BackColor = Color.FromArgb(239, 243, 236);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        InitializeLayout();

        Shown += OnShownAsync;
        FormClosing += OnFormClosing;
        _refreshTimer.Tick += OnRefreshTimerTickAsync;
    }

    private void InitializeLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 54F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 46F));

        root.Controls.Add(BuildHeaderMetrics(), 0, 0);
        root.Controls.Add(BuildChartSection(), 0, 1);
        root.Controls.Add(BuildLowerSection(), 0, 2);

        var statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = false
        };
        statusStrip.Items.Add(_statusLabel);

        Controls.Add(root);
        Controls.Add(statusStrip);
    }

    private Control BuildHeaderMetrics()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

        var cards = new[]
        {
            CreateMetricCard("Puissance instantanee", _currentPowerValue, _thresholdValue, Color.FromArgb(65, 104, 81)),
            CreateMetricCard("Pic du jour", _peakPowerValue, _anomaliesValue, Color.FromArgb(145, 87, 48)),
            CreateMetricCard("Conso derniere heure", _lastHourValue, CreateMetricCaptionLabel("Mise a jour toutes les 5 secondes"), Color.FromArgb(45, 94, 118)),
            CreateMetricCard("Conso du jour", _todayValue, _sourceValue, Color.FromArgb(76, 78, 114))
        };

        for (var index = 0; index < cards.Length; index++)
        {
            cards[index].Margin = index == cards.Length - 1
                ? new Padding(0)
                : new Padding(0, 0, 14, 0);
            layout.Controls.Add(cards[index], index, 0);
        }

        return layout;
    }

    private Control BuildChartSection()
    {
        ConfigureChart();
        return CreateSectionPanel("Tendance temps reel", _powerChart, new Padding(0, 0, 0, 14));
    }

    private Control BuildLowerSection()
    {
        ConfigureLatestReadingsGrid();
        ConfigureAlertsList();
        ConfigureAggregationList();

        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));

        var readingsSection = CreateSectionPanel("Dernieres mesures", _latestReadingsGrid, new Padding(0, 0, 14, 0));

        var sideColumn = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        sideColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
        sideColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));

        var alertsSection = CreateSectionPanel("Alertes", _alertsList, new Padding(0, 0, 0, 14));
        var aggregationSection = CreateSectionPanel("Aggregations recentes", _aggregationList, Padding.Empty);

        sideColumn.Controls.Add(alertsSection, 0, 0);
        sideColumn.Controls.Add(aggregationSection, 0, 1);

        split.Controls.Add(readingsSection, 0, 0);
        split.Controls.Add(sideColumn, 1, 0);

        return split;
    }

    private static Panel CreateMetricCard(string title, Label valueLabel, Label footerLabel, Color accentColor)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 16, 18, 16),
            BackColor = Color.White
        };

        panel.Paint += (_, args) =>
        {
            using var borderPen = new Pen(Color.FromArgb(216, 223, 214), 1F);
            using var accentBrush = new SolidBrush(accentColor);

            args.Graphics.DrawRectangle(borderPen, 0, 0, panel.Width - 1, panel.Height - 1);
            args.Graphics.FillRectangle(accentBrush, 0, 0, 9, panel.Height);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = Color.FromArgb(77, 83, 76),
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point)
        };

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(valueLabel, 0, 1);
        layout.Controls.Add(footerLabel, 0, 2);
        panel.Controls.Add(layout);

        return panel;
    }

    private static Panel CreateSectionPanel(string title, Control content, Padding margin)
    {
        content.Dock = DockStyle.Fill;

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = margin,
            Padding = new Padding(16),
            BackColor = Color.White
        };

        panel.Paint += (_, args) =>
        {
            using var borderPen = new Pen(Color.FromArgb(216, 223, 214), 1F);
            args.Graphics.DrawRectangle(borderPen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = Color.FromArgb(48, 54, 48),
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point)
        };

        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(content, 0, 1);
        panel.Controls.Add(layout);

        return panel;
    }

    private void ConfigureChart()
    {
        _powerChart.BackColor = Color.White;
    }

    private void ConfigureLatestReadingsGrid()
    {
        _latestReadingsGrid.ReadOnly = true;
        _latestReadingsGrid.AutoGenerateColumns = true;
        _latestReadingsGrid.AllowUserToAddRows = false;
        _latestReadingsGrid.AllowUserToDeleteRows = false;
        _latestReadingsGrid.AllowUserToResizeRows = false;
        _latestReadingsGrid.BackgroundColor = Color.White;
        _latestReadingsGrid.BorderStyle = BorderStyle.None;
        _latestReadingsGrid.RowHeadersVisible = false;
        _latestReadingsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _latestReadingsGrid.MultiSelect = false;
        _latestReadingsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _latestReadingsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 237, 227);
        _latestReadingsGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(38, 50, 38);
    }

    private void ConfigureAlertsList()
    {
        _alertsList.View = View.Details;
        _alertsList.FullRowSelect = true;
        _alertsList.GridLines = false;
        _alertsList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _alertsList.Columns.Add("Heure", 110);
        _alertsList.Columns.Add("Niveau", 110);
        _alertsList.Columns.Add("Message", 320);
    }

    private void ConfigureAggregationList()
    {
        _aggregationList.View = View.Details;
        _aggregationList.FullRowSelect = true;
        _aggregationList.GridLines = false;
        _aggregationList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _aggregationList.Columns.Add("Periode", 90);
        _aggregationList.Columns.Add("Debut", 120);
        _aggregationList.Columns.Add("kWh", 80);
    }

    private async void OnShownAsync(object? sender, EventArgs eventArgs)
    {
        await RefreshDashboardAsync();
        _refreshTimer.Start();
    }

    private async void OnRefreshTimerTickAsync(object? sender, EventArgs eventArgs)
    {
        await RefreshDashboardAsync();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        _refreshTimer.Stop();
        _apiClient.Dispose();
    }

    private async Task RefreshDashboardAsync()
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshInProgress = true;
        _statusLabel.Text = "Synchronisation du dashboard...";

        try
        {
            var snapshot = await _apiClient.GetSnapshotAsync();
            ApplySnapshot(snapshot);
            _statusLabel.Text = $"Derniere synchro : {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"API indisponible : {exception.Message}";
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private void ApplySnapshot(DashboardSnapshotDto snapshot)
    {
        _currentPowerValue.Text = $"{snapshot.Summary.CurrentPowerWatts:F0} W";
        _peakPowerValue.Text = $"{snapshot.Summary.PeakTodayWatts:F0} W";
        _lastHourValue.Text = $"{snapshot.Summary.LastHourKwh:F2} kWh";
        _todayValue.Text = $"{snapshot.Summary.TodayKwh:F2} kWh";
        _thresholdValue.Text = $"Seuil : {snapshot.Summary.AlertThresholdWatts} W";
        _anomaliesValue.Text = $"{snapshot.Summary.AnomalyCount24h} anomalies sur 24 h";
        _sourceValue.Text = $"Source : {snapshot.Summary.SourceName}";

        UpdateChart(snapshot.LatestReadings);
        UpdateReadingsGrid(snapshot.LatestReadings);
        UpdateAlerts(snapshot.Alerts);
        UpdateAggregations(snapshot.HourlyConsumption, snapshot.DailyConsumption);
    }

    private void UpdateChart(IReadOnlyList<EnergyReadingDto> readings)
    {
        _powerChart.SetReadings(readings);
    }

    private void UpdateReadingsGrid(IReadOnlyList<EnergyReadingDto> readings)
    {
        var rows = readings
            .OrderByDescending(reading => reading.TimestampUtc)
            .Select(reading => new ReadingRow(
                reading.TimestampUtc.ToLocalTime().ToString("dd/MM HH:mm:ss"),
                reading.Source,
                $"{reading.PowerWatts:F0} W",
                $"{reading.EnergyKwh:F4}",
                reading.IsAnomaly ? "Oui" : "Non"))
            .ToList();

        _latestReadingsGrid.DataSource = rows;
    }

    private void UpdateAlerts(IReadOnlyList<EnergyAlertDto> alerts)
    {
        _alertsList.BeginUpdate();
        _alertsList.Items.Clear();

        foreach (var alert in alerts)
        {
            var item = new ListViewItem(alert.TimestampUtc.ToLocalTime().ToString("dd/MM HH:mm"));
            item.SubItems.Add(alert.Severity);
            item.SubItems.Add(alert.Message);
            item.ForeColor = alert.Severity == "Critique"
                ? Color.FromArgb(166, 44, 44)
                : Color.FromArgb(154, 112, 24);

            _alertsList.Items.Add(item);
        }

        if (_alertsList.Items.Count == 0)
        {
            _alertsList.Items.Add(new ListViewItem(new[] { "-", "RAS", "Aucune alerte recente." }));
        }

        _alertsList.EndUpdate();
    }

    private void UpdateAggregations(
        IReadOnlyList<EnergyAggregationPointDto> hourly,
        IReadOnlyList<EnergyAggregationPointDto> daily)
    {
        _aggregationList.BeginUpdate();
        _aggregationList.Items.Clear();
        _aggregationList.Groups.Clear();

        var hourlyGroup = new ListViewGroup("Dernieres heures", HorizontalAlignment.Left);
        var dailyGroup = new ListViewGroup("Derniers jours", HorizontalAlignment.Left);

        _aggregationList.Groups.Add(hourlyGroup);
        _aggregationList.Groups.Add(dailyGroup);

        foreach (var item in hourly.OrderByDescending(point => point.BucketStartUtc).Take(6))
        {
            var row = new ListViewItem("Heure", hourlyGroup);
            row.SubItems.Add(item.Label);
            row.SubItems.Add(item.ValueKwh.ToString("F2"));
            _aggregationList.Items.Add(row);
        }

        foreach (var item in daily.OrderByDescending(point => point.BucketStartUtc))
        {
            var row = new ListViewItem("Jour", dailyGroup);
            row.SubItems.Add(item.Label);
            row.SubItems.Add(item.ValueKwh.ToString("F2"));
            _aggregationList.Items.Add(row);
        }

        _aggregationList.EndUpdate();
    }

    private static Label CreateMetricValueLabel(string? initialText = null)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = initialText ?? "--",
            ForeColor = Color.FromArgb(34, 42, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold, GraphicsUnit.Point)
        };
    }

    private static Label CreateMetricCaptionLabel(string? initialText = null)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = initialText ?? string.Empty,
            ForeColor = Color.FromArgb(104, 108, 102),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point)
        };
    }

    private sealed record ReadingRow(
        string Horodatage,
        string Source,
        string Puissance,
        string EnergieKwh,
        string Alerte);
}
