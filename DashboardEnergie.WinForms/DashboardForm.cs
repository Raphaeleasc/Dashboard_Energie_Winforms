using DashboardEnergie.Shared;

namespace DashboardEnergie.WinForms;

public sealed class DashboardForm : Form
{
    private const string ApiBaseAddress = "http://localhost:5188/";

    private readonly DashboardApiClient _apiClient = new(ApiBaseAddress);
    private readonly Button _reloadButton = new();
    private readonly ToolStripStatusLabel _statusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ComboBox _technicianChartModeCombo = new();
    private readonly Label _technicianChartModeCaption = new();

    private readonly Label _techCurrentPowerValue = CreateMetricValueLabel();
    private readonly Label _techPeakDayValue = CreateMetricValueLabel();
    private readonly Label _techLastHourValue = CreateMetricValueLabel();
    private readonly Label _techDayTotalValue = CreateMetricValueLabel();
    private readonly Label _techThresholdCaption = CreateMetricCaptionLabel();
    private readonly Label _techAnomaliesCaption = CreateMetricCaptionLabel();
    private readonly Label _techCoverageCaption = CreateMetricCaptionLabel();
    private readonly Label _techLastUpdateCaption = CreateMetricCaptionLabel();

    private readonly Label _rseAnnualTotalValue = CreateMetricValueLabel();
    private readonly Label _rseLatestMonthValue = CreateMetricValueLabel();
    private readonly Label _rseTopCategoryValue = CreateMetricValueLabel();
    private readonly Label _rseMonthCountValue = CreateMetricValueLabel();
    private readonly Label _rseLatestMonthCaption = CreateMetricCaptionLabel();
    private readonly Label _rseShareCaption = CreateMetricCaptionLabel();
    private readonly Label _rseCoverageCaption = CreateMetricCaptionLabel();
    private readonly Label _rseSourceCaption = CreateMetricCaptionLabel();

    private readonly PowerTrendChart _powerChart = new();
    private readonly MonthlyTotalsChart _monthlyTotalsChart = new();
    private readonly DataGridView _latestReadingsGrid = new();
    private readonly DataGridView _monthlyBreakdownGrid = new();
    private readonly ListView _alertsList = new();
    private readonly ListView _dailyAggregationList = new();
    private readonly ListView _rseTotalsList = new();

    private bool _refreshInProgress;
    private DashboardSnapshotDto? _currentSnapshot;

    public DashboardForm()
    {
        Text = "Dashboard Energie";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1280, 820);
        ClientSize = new Size(1460, 920);
        BackColor = Color.FromArgb(238, 242, 236);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        InitializeLayout();

        Shown += OnShownAsync;
        FormClosing += OnFormClosing;
    }

    private void InitializeLayout()
    {
        ConfigureLatestReadingsGrid();
        ConfigureMonthlyBreakdownGrid();
        ConfigureAlertsList();
        ConfigureDailyAggregationList();
        ConfigureRseTotalsList();
        ConfigureTechnicianChartModeSelector();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildTabs(), 0, 1);

        var statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = false
        };
        statusStrip.Items.Add(_statusLabel);

        Controls.Add(root);
        Controls.Add(statusStrip);
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(20, 16, 20, 16),
            Margin = new Padding(0, 0, 0, 14)
        };

        header.Paint += (_, args) =>
        {
            using var borderPen = new Pen(Color.FromArgb(216, 223, 214), 1F);
            args.Graphics.DrawRectangle(borderPen, 0, 0, header.Width - 1, header.Height - 1);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        titlePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        titlePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Dashboard de consommation energetique",
            ForeColor = Color.FromArgb(34, 42, 34),
            Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point)
        };

        var subtitleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Solution WinForms + API locale + SQLite alimentee par les jeux de donnees technicien et RSE.",
            ForeColor = Color.FromArgb(92, 98, 92),
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point)
        };

        _reloadButton.Dock = DockStyle.Fill;
        _reloadButton.Text = "Recharger les CSV";
        _reloadButton.BackColor = Color.FromArgb(59, 92, 68);
        _reloadButton.ForeColor = Color.White;
        _reloadButton.FlatStyle = FlatStyle.Flat;
        _reloadButton.FlatAppearance.BorderSize = 0;
        _reloadButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);
        _reloadButton.Margin = new Padding(24, 8, 0, 8);
        _reloadButton.Click += OnReloadClickedAsync;

        titlePanel.Controls.Add(titleLabel, 0, 0);
        titlePanel.Controls.Add(subtitleLabel, 0, 1);
        layout.Controls.Add(titlePanel, 0, 0);
        layout.Controls.Add(_reloadButton, 1, 0);
        header.Controls.Add(layout);

        return header;
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Point(18, 8)
        };

        var technicianPage = new TabPage("Technicien d'exploitation")
        {
            BackColor = BackColor,
            Padding = new Padding(12)
        };
        technicianPage.Controls.Add(BuildTechnicianView());

        var rsePage = new TabPage("Responsable energie / RSE")
        {
            BackColor = BackColor,
            Padding = new Padding(12)
        };
        rsePage.Controls.Add(BuildRseView());

        tabs.TabPages.Add(technicianPage);
        tabs.TabPages.Add(rsePage);

        return tabs;
    }

    private Control BuildTechnicianView()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));

        var cards = BuildMetricStrip(
            ("Puissance instantanee", _techCurrentPowerValue, _techThresholdCaption, Color.FromArgb(65, 104, 81)),
            ("Pic sur le dernier jour", _techPeakDayValue, _techAnomaliesCaption, Color.FromArgb(145, 87, 48)),
            ("Derniere heure chargee", _techLastHourValue, _techCoverageCaption, Color.FromArgb(45, 94, 118)),
            ("Total du dernier jour", _techDayTotalValue, _techLastUpdateCaption, Color.FromArgb(76, 78, 114)));

        var chartSection = CreateSectionPanel(
            "Analyse des donnees techniques",
            BuildTechnicianChartPanel(),
            new Padding(0, 0, 0, 14));

        var lower = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));

        var readingsSection = CreateSectionPanel("Dernieres valeurs techniques", _latestReadingsGrid, new Padding(0, 0, 14, 0));

        var sideColumn = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        sideColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
        sideColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));

        var alertsSection = CreateSectionPanel("Alertes et seuils", _alertsList, new Padding(0, 0, 0, 14));
        var dailySection = CreateSectionPanel("Aggregations journalieres", _dailyAggregationList, Padding.Empty);

        sideColumn.Controls.Add(alertsSection, 0, 0);
        sideColumn.Controls.Add(dailySection, 0, 1);

        lower.Controls.Add(readingsSection, 0, 0);
        lower.Controls.Add(sideColumn, 1, 0);

        root.Controls.Add(cards, 0, 0);
        root.Controls.Add(chartSection, 0, 1);
        root.Controls.Add(lower, 0, 2);

        return root;
    }

    private Control BuildRseView()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));

        var cards = BuildMetricStrip(
            ("Total annuel", _rseAnnualTotalValue, _rseLatestMonthCaption, Color.FromArgb(59, 92, 68)),
            ("Dernier mois", _rseLatestMonthValue, _rseShareCaption, Color.FromArgb(154, 112, 24)),
            ("Poste principal", _rseTopCategoryValue, _rseCoverageCaption, Color.FromArgb(80, 92, 146)),
            ("Mois charges", _rseMonthCountValue, _rseSourceCaption, Color.FromArgb(134, 82, 117)));

        var chartSection = CreateSectionPanel("Evolution mensuelle RSE", _monthlyTotalsChart, new Padding(0, 0, 0, 14));

        var lower = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));

        var monthlySection = CreateSectionPanel("Detail mensuel par poste", _monthlyBreakdownGrid, new Padding(0, 0, 14, 0));
        var categoriesSection = CreateSectionPanel("Repartition annuelle par categorie", _rseTotalsList, Padding.Empty);

        lower.Controls.Add(monthlySection, 0, 0);
        lower.Controls.Add(categoriesSection, 1, 0);

        root.Controls.Add(cards, 0, 0);
        root.Controls.Add(chartSection, 0, 1);
        root.Controls.Add(lower, 0, 2);

        return root;
    }

    private Control BuildTechnicianChartPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));

        header.Controls.Add(_technicianChartModeCaption, 0, 0);
        header.Controls.Add(_technicianChartModeCombo, 1, 0);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(_powerChart, 0, 1);

        return layout;
    }

    private static Control BuildMetricStrip(params (string title, Label value, Label footer, Color accent)[] cards)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = cards.Length,
            RowCount = 1
        };

        for (var index = 0; index < cards.Length; index++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / cards.Length));
            var card = CreateMetricCard(cards[index].title, cards[index].value, cards[index].footer, cards[index].accent);
            card.Margin = index == cards.Length - 1
                ? new Padding(0)
                : new Padding(0, 0, 14, 0);
            layout.Controls.Add(card, index, 0);
        }

        return layout;
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

    private void ConfigureLatestReadingsGrid()
    {
        ConfigureGrid(_latestReadingsGrid);
    }

    private void ConfigureMonthlyBreakdownGrid()
    {
        ConfigureGrid(_monthlyBreakdownGrid);
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.ReadOnly = true;
        grid.AutoGenerateColumns = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 237, 227);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(38, 50, 38);
    }

    private void ConfigureAlertsList()
    {
        _alertsList.View = View.Details;
        _alertsList.FullRowSelect = true;
        _alertsList.GridLines = false;
        _alertsList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _alertsList.Columns.Add("Heure", 120);
        _alertsList.Columns.Add("Niveau", 110);
        _alertsList.Columns.Add("Message", 340);
    }

    private void ConfigureDailyAggregationList()
    {
        _dailyAggregationList.View = View.Details;
        _dailyAggregationList.FullRowSelect = true;
        _dailyAggregationList.GridLines = false;
        _dailyAggregationList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _dailyAggregationList.Columns.Add("Jour", 100);
        _dailyAggregationList.Columns.Add("kWh", 90);
    }

    private void ConfigureRseTotalsList()
    {
        _rseTotalsList.View = View.Details;
        _rseTotalsList.FullRowSelect = true;
        _rseTotalsList.GridLines = false;
        _rseTotalsList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _rseTotalsList.Columns.Add("Poste", 150);
        _rseTotalsList.Columns.Add("kWh", 90);
        _rseTotalsList.Columns.Add("Part", 90);
    }

    private void ConfigureTechnicianChartModeSelector()
    {
        _technicianChartModeCaption.Dock = DockStyle.Fill;
        _technicianChartModeCaption.TextAlign = ContentAlignment.MiddleLeft;
        _technicianChartModeCaption.ForeColor = Color.FromArgb(92, 98, 92);
        _technicianChartModeCaption.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        _technicianChartModeCaption.Text = "Vue brute : puissance par point du fichier technicien.";

        _technicianChartModeCombo.Dock = DockStyle.Fill;
        _technicianChartModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _technicianChartModeCombo.FlatStyle = FlatStyle.Flat;
        _technicianChartModeCombo.Items.AddRange(
            [
                "Dernieres mesures (W)",
                "Aggregation horaire (kWh)",
                "Aggregation journaliere (kWh)"
            ]);
        _technicianChartModeCombo.SelectedIndex = 0;
        _technicianChartModeCombo.SelectedIndexChanged += OnTechnicianChartModeChanged;
    }

    private async void OnShownAsync(object? sender, EventArgs eventArgs)
    {
        await RefreshDashboardAsync();
    }

    private async void OnReloadClickedAsync(object? sender, EventArgs eventArgs)
    {
        if (_refreshInProgress)
        {
            return;
        }

        _reloadButton.Enabled = false;
        _statusLabel.Text = "Rechargement des CSV dans SQLite...";

        try
        {
            await _apiClient.ReloadAsync();
            await RefreshDashboardAsync();
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"Rechargement impossible : {exception.Message}";
        }
        finally
        {
            _reloadButton.Enabled = true;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        _apiClient.Dispose();
    }

    private void OnTechnicianChartModeChanged(object? sender, EventArgs eventArgs)
    {
        if (_currentSnapshot is not null)
        {
            UpdateTechnicianChart(_currentSnapshot);
        }
    }

    private async Task RefreshDashboardAsync()
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshInProgress = true;
        _reloadButton.Enabled = false;
        _statusLabel.Text = "Chargement des donnees du dashboard...";

        try
        {
            var snapshot = await _apiClient.GetSnapshotAsync();
            _currentSnapshot = snapshot;
            ApplySnapshot(snapshot);
            _statusLabel.Text =
                $"Vue technicien : {snapshot.Summary.CoverageStart:dd/MM/yyyy} -> {snapshot.Summary.CoverageEnd:dd/MM/yyyy} | " +
                $"Vue RSE : 12 mois charges jusqu'a {snapshot.Summary.LatestMonthLabel}";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"API indisponible : {exception.Message}";
        }
        finally
        {
            _reloadButton.Enabled = true;
            _refreshInProgress = false;
        }
    }

    private void ApplySnapshot(DashboardSnapshotDto snapshot)
    {
        ApplyTechnicianSummary(snapshot);
        ApplyRseSummary(snapshot);

        UpdateTechnicianChart(snapshot);
        _monthlyTotalsChart.SetBreakdowns(snapshot.RseMonthlyBreakdowns);

        UpdateReadingsGrid(snapshot.LatestReadings);
        UpdateAlerts(snapshot.Alerts);
        UpdateDailyAggregations(snapshot.DailyConsumption);
        UpdateMonthlyBreakdowns(snapshot.RseMonthlyBreakdowns);
        UpdateRseTotals(snapshot.RseCategoryTotals);
    }

    private void UpdateTechnicianChart(DashboardSnapshotDto snapshot)
    {
        switch (_technicianChartModeCombo.SelectedIndex)
        {
            case 1:
                _powerChart.SetAggregations(snapshot.HourlyConsumption);
                _technicianChartModeCaption.Text = "Aggregation horaire : consommation totale par heure sur la fin du jeu technicien.";
                break;
            case 2:
                _powerChart.SetAggregations(snapshot.DailyConsumption);
                _technicianChartModeCaption.Text = "Aggregation journaliere : total kWh par jour pour visualiser les variations globales.";
                break;
            default:
                _powerChart.SetReadings(snapshot.LatestReadings);
                _technicianChartModeCaption.Text = "Vue brute : puissance par point du fichier technicien.";
                break;
        }
    }

    private void ApplyTechnicianSummary(DashboardSnapshotDto snapshot)
    {
        _techCurrentPowerValue.Text = $"{snapshot.Summary.CurrentPowerWatts:F0} W";
        _techPeakDayValue.Text = $"{snapshot.Summary.PeakTodayWatts:F0} W";
        _techLastHourValue.Text = $"{snapshot.Summary.LastHourKwh:F2} kWh";
        _techDayTotalValue.Text = $"{snapshot.Summary.TodayKwh:F2} kWh";
        _techThresholdCaption.Text = $"Seuil alerte : {snapshot.Summary.AlertThresholdWatts} W";
        _techAnomaliesCaption.Text = $"{snapshot.Summary.AnomalyCount24h} alertes sur les 24 dernieres heures";
        _techCoverageCaption.Text =
            $"Periode : {snapshot.Summary.CoverageStart:dd/MM/yyyy} -> {snapshot.Summary.CoverageEnd:dd/MM/yyyy}";
        _techLastUpdateCaption.Text = $"Derniere mesure : {snapshot.Summary.LastUpdate:dd/MM/yyyy HH:mm}";
    }

    private void ApplyRseSummary(DashboardSnapshotDto snapshot)
    {
        var latestMonth = snapshot.RseMonthlyBreakdowns.LastOrDefault();
        var dominantCategory = snapshot.RseCategoryTotals.OrderByDescending(item => item.TotalKwh).FirstOrDefault();

        _rseAnnualTotalValue.Text = $"{snapshot.Summary.AnnualRseKwh:F1} kWh";
        _rseLatestMonthValue.Text = latestMonth is null ? "--" : $"{latestMonth.TotalKwh:F1} kWh";
        _rseTopCategoryValue.Text = dominantCategory?.Category ?? "--";
        _rseMonthCountValue.Text = snapshot.RseMonthlyBreakdowns.Count.ToString();
        _rseLatestMonthCaption.Text = $"Dernier mois : {snapshot.Summary.LatestMonthLabel}";
        _rseShareCaption.Text = dominantCategory is null ? string.Empty : $"{dominantCategory.SharePercent:F1} % du total annuel";
        _rseCoverageCaption.Text = "Couverture : exercice 2025 complet";
        _rseSourceCaption.Text = $"Source : {snapshot.Summary.SourceName}";
    }

    private void UpdateReadingsGrid(IReadOnlyList<EnergyReadingDto> readings)
    {
        var rows = readings
            .OrderByDescending(reading => reading.Timestamp)
            .Select(reading => new TechnicianRow(
                reading.Timestamp.ToString("dd/MM HH:mm"),
                $"{reading.PowerWatts:F0} W",
                $"{reading.EnergyKwh:F3}",
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
            var item = new ListViewItem(alert.Timestamp.ToString("dd/MM HH:mm"));
            item.SubItems.Add(alert.Severity);
            item.SubItems.Add(alert.Message);
            item.ForeColor = alert.Severity == "Critique"
                ? Color.FromArgb(166, 44, 44)
                : Color.FromArgb(154, 112, 24);

            _alertsList.Items.Add(item);
        }

        if (_alertsList.Items.Count == 0)
        {
            _alertsList.Items.Add(new ListViewItem(new[] { "-", "RAS", "Aucune alerte detectee." }));
        }

        _alertsList.EndUpdate();
    }

    private void UpdateDailyAggregations(IReadOnlyList<EnergyAggregationPointDto> dailyAggregations)
    {
        _dailyAggregationList.BeginUpdate();
        _dailyAggregationList.Items.Clear();

        foreach (var item in dailyAggregations.OrderByDescending(point => point.BucketStart).Take(10))
        {
            var row = new ListViewItem(item.Label);
            row.SubItems.Add(item.ValueKwh.ToString("F2"));
            _dailyAggregationList.Items.Add(row);
        }

        if (_dailyAggregationList.Items.Count == 0)
        {
            _dailyAggregationList.Items.Add(new ListViewItem(new[] { "-", "0.00" }));
        }

        _dailyAggregationList.EndUpdate();
    }

    private void UpdateMonthlyBreakdowns(IReadOnlyList<RseMonthlyBreakdownDto> breakdowns)
    {
        var rows = breakdowns
            .Select(item => new RseMonthlyRow(
                item.Month,
                item.TotalKwh.ToString("F1"),
                item.HeatingKwh.ToString("F1"),
                item.WaterHeatingKwh.ToString("F1"),
                item.AppliancesKwh.ToString("F1"),
                item.LightingKwh.ToString("F1"),
                item.OtherKwh.ToString("F1")))
            .ToList();

        _monthlyBreakdownGrid.DataSource = rows;
    }

    private void UpdateRseTotals(IReadOnlyList<RseCategoryTotalDto> totals)
    {
        _rseTotalsList.BeginUpdate();
        _rseTotalsList.Items.Clear();

        foreach (var total in totals)
        {
            var row = new ListViewItem(total.Category);
            row.SubItems.Add(total.TotalKwh.ToString("F1"));
            row.SubItems.Add($"{total.SharePercent:F1} %");
            _rseTotalsList.Items.Add(row);
        }

        if (_rseTotalsList.Items.Count == 0)
        {
            _rseTotalsList.Items.Add(new ListViewItem(new[] { "-", "0.0", "0.0 %" }));
        }

        _rseTotalsList.EndUpdate();
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
            Font = new Font("Segoe UI", 23F, FontStyle.Bold, GraphicsUnit.Point)
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

    private sealed record TechnicianRow(
        string Horodatage,
        string Puissance,
        string EnergieKwh,
        string Alerte);

    private sealed record RseMonthlyRow(
        string Mois,
        string TotalKwh,
        string Chauffage,
        string EauChaude,
        string Appareils,
        string Eclairage,
        string Autres);
}
