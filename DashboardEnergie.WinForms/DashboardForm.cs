using DashboardEnergie.Shared;
using System.Drawing.Drawing2D;

namespace DashboardEnergie.WinForms;

public sealed class DashboardForm : Form
{
    private const int CardCornerRadius = 12;
    private const double Co2Factor = 0.0573;
    private const double EdfTariff = 0.2516;
    private const double RseTargetKwh = 4800d;

    private readonly DashboardApiClient _apiClient;
    private readonly ApiProcessManager _apiProcessManager;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly Button _reloadButton = new();
    private readonly Label _clockLabel = new();
    private readonly Panel _liveDot = new();
    private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1000 };
    private readonly TabControl _mainTabs = new();
    private readonly ToolStripStatusLabel _statusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ComboBox _technicianChartModeCombo = new();
    private readonly Label _technicianChartModeCaption = new();
    private readonly Dictionary<string, Button> _navigationButtons = new();

    private readonly Label _overviewTotalValue = CreateMetricValueLabel();
    private readonly Label _overviewCostValue = CreateMetricValueLabel();
    private readonly Label _overviewCo2Value = CreateMetricValueLabel();
    private readonly Label _overviewAverageValue = CreateMetricValueLabel();
    private readonly Label _overviewCoverageCaption = CreateMetricCaptionLabel();
    private readonly Label _overviewTariffCaption = CreateMetricCaptionLabel();
    private readonly Label _overviewCo2Caption = CreateMetricCaptionLabel();
    private readonly Label _overviewAverageCaption = CreateMetricCaptionLabel();

    private readonly Label _forecastTrendValue = CreateMetricValueLabel();
    private readonly Label _forecastJ1Value = CreateMetricValueLabel();
    private readonly Label _forecastJ7Value = CreateMetricValueLabel();
    private readonly Label _forecastTrendCaption = CreateMetricCaptionLabel();
    private readonly Label _forecastJ1Caption = CreateMetricCaptionLabel();
    private readonly Label _forecastJ7Caption = CreateMetricCaptionLabel();

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
    private readonly PowerTrendChart _overviewDailyChart = new();
    private readonly MonthlyTotalsChart _monthlyTotalsChart = new();
    private readonly ForecastTrendChart _forecastChart = new();
    private readonly DataGridView _latestReadingsGrid = new();
    private readonly DataGridView _monthlyBreakdownGrid = new();
    private readonly ListView _alertsList = new();
    private readonly ListView _overviewAnomaliesList = new();
    private readonly ListView _overviewDailyList = new();
    private readonly ListView _dailyAggregationList = new();
    private readonly ListView _rseTotalsList = new();
    private readonly ListView _forecastTable = new();

    private readonly Panel _rseProgressTrack = new();
    private readonly Panel _rseProgressFill = new();
    private readonly Label _rseProgressValue = new();
    private readonly Label _rseProgressPercentLabel = new();
    private double _rseProgressRatio;

    private bool _refreshInProgress;
    private DashboardSnapshotDto? _currentSnapshot;

    public DashboardForm()
    {
        var apiBaseAddress = ResolveApiBaseAddress();
        _apiClient = new DashboardApiClient(apiBaseAddress);
        _apiProcessManager = new ApiProcessManager();

        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Dashboard Energie";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1280, 820);
        ClientSize = new Size(1460, 920);
        BackColor = UiTheme.PageBackground;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        InitializeLayout();

        Shown += OnShownAsync;
        FormClosing += OnFormClosing;

        _clockTimer.Tick += (_, _) => _clockLabel.Text = DateTime.Now.ToString("dd MMM yyyy HH:mm:ss");
        _clockTimer.Start();
    }

    private void InitializeLayout()
    {
        ConfigureLatestReadingsGrid();
        ConfigureMonthlyBreakdownGrid();
        ConfigureAlertsList();
        ConfigureOverviewAnomaliesList();
        ConfigureOverviewDailyList();
        ConfigureDailyAggregationList();
        ConfigureRseTotalsList();
        ConfigureForecastTable();
        ConfigureTechnicianChartModeSelector();
        ConfigureCharts();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            ColumnCount = 1,
            RowCount = 2,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        root.Controls.Add(BuildTopBar(), 0, 0);
        root.Controls.Add(BuildMainShell(), 0, 1);

        var statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = false,
            BackColor = UiTheme.Surface,
            ForeColor = UiTheme.TextSecondary
        };
        statusStrip.Items.Add(_statusLabel);
        _statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        Controls.Add(root);
        Controls.Add(statusStrip);
    }

    private Control BuildTopBar()
    {
        var topBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            Padding = new Padding(18, 0, 18, 0)
        };

        topBar.Paint += (_, args) =>
        {
            using var borderPen = new Pen(UiTheme.Border, 1F);
            args.Graphics.DrawLine(borderPen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
        };

        var logoPanel = new Panel
        {
            Size = new Size(28, 28),
            Margin = new Padding(0, 0, 10, 0),
            BackColor = Color.Transparent
        };
        logoPanel.Paint += (_, args) =>
        {
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fillBrush = new LinearGradientBrush(
                new Rectangle(0, 0, 28, 28),
                UiTheme.BrandHover,
                UiTheme.Brand,
                45f);
            using var borderPen = new Pen(Color.FromArgb(72, UiTheme.Brand), 1F);

            var bounds = new Rectangle(0, 0, 27, 27);
            using var path = CreateRoundedPath(bounds, 7);
            args.Graphics.FillPath(fillBrush, path);
            args.Graphics.DrawPath(borderPen, path);

            using var iconBrush = new SolidBrush(Color.FromArgb(232, 244, 255));
            var bolt = new[]
            {
                new PointF(14f, 5f),
                new PointF(8f, 14f),
                new PointF(13f, 14f),
                new PointF(11f, 23f),
                new PointF(19f, 12f),
                new PointF(14f, 12f)
            };
            args.Graphics.FillPolygon(iconBrush, bolt);
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = "SparkVision Energie",
            ForeColor = UiTheme.TextPrimary,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point)
        };

        var subtitleLabel = new Label
        {
            AutoSize = true,
            Text = "v2.0",
            ForeColor = UiTheme.TextMuted,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(8, 7, 0, 0)
        };

        _liveDot.Size = new Size(8, 8);
        _liveDot.BackColor = UiTheme.AccentGreen;
        _liveDot.Margin = new Padding(0, 0, 6, 0);
        _liveDot.Paint += (_, args) =>
        {
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(UiTheme.AccentGreen);
            args.Graphics.FillEllipse(brush, 0, 0, _liveDot.Width - 1, _liveDot.Height - 1);
        };

        var liveLabel = new Label
        {
            AutoSize = true,
            Text = "LIVE",
            ForeColor = UiTheme.AccentGreen,
            Font = new Font("Consolas", 9F, FontStyle.Bold, GraphicsUnit.Point)
        };

        _clockLabel.AutoSize = true;
        _clockLabel.ForeColor = UiTheme.TextSecondary;
        _clockLabel.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
        _clockLabel.Text = DateTime.Now.ToString("dd MMM yyyy HH:mm:ss");

        _reloadButton.Text = "Exporter / Recharger";
        _reloadButton.Size = new Size(160, 34);
        _reloadButton.BackColor = Color.FromArgb(18, 37, 60);
        _reloadButton.ForeColor = UiTheme.Brand;
        _reloadButton.FlatStyle = FlatStyle.Flat;
        _reloadButton.FlatAppearance.BorderColor = Color.FromArgb(52, UiTheme.Brand);
        _reloadButton.FlatAppearance.BorderSize = 1;
        _reloadButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(22, 48, 76);
        _reloadButton.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
        _reloadButton.TextAlign = ContentAlignment.MiddleCenter;
        _reloadButton.Margin = new Padding(18, 0, 0, 0);
        _reloadButton.Click -= OnReloadClickedAsync;
        _reloadButton.Click += OnReloadClickedAsync;

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 14, 0, 0),
            BackColor = Color.Transparent
        };
        left.Controls.Add(logoPanel);
        left.Controls.Add(titleLabel);
        left.Controls.Add(subtitleLabel);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 16, 0, 0),
            BackColor = Color.Transparent
        };
        right.Controls.Add(_liveDot);
        right.Controls.Add(liveLabel);
        right.Controls.Add(_clockLabel);
        right.Controls.Add(_reloadButton);

        topBar.Controls.Add(right);
        topBar.Controls.Add(left);
        return topBar;
    }

    private Control BuildMainShell()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = BackColor,
            Padding = new Padding(0)
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        shell.Controls.Add(BuildSidebar(), 0, 0);
        shell.Controls.Add(BuildTabs(), 1, 0);
        return shell;
    }

    private Control BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(10, 16, 28),
            Padding = new Padding(12, 16, 12, 14)
        };

        sidebar.Paint += (_, args) =>
        {
            using var borderPen = new Pen(UiTheme.Border, 1F);
            args.Graphics.DrawLine(borderPen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };

        var caption = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "NAVIGATION",
            ForeColor = UiTheme.TextMuted,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(6, 0, 0, 0)
        };

        var navContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 220,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var navItems = new[]
        {
            ("overview", "◈  Vue d'ensemble"),
            ("technicien", "⬡  Technicien"),
            ("rse", "◉  RSE / Objectifs"),
            ("previsions", "◌  Previsions")
        };

        foreach (var (id, label) in navItems)
        {
            var button = CreateNavigationButton(label, id);
            _navigationButtons[id] = button;
            navContainer.Controls.Add(button);
        }

        var objectiveCard = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 112,
            BackColor = Color.FromArgb(14, 30, 45),
            Padding = new Padding(12)
        };
        objectiveCard.Paint += (_, args) =>
        {
            var bounds = new Rectangle(0, 0, objectiveCard.Width - 1, objectiveCard.Height - 1);
            DrawRoundedPanel(args.Graphics, bounds, Color.FromArgb(14, 30, 45), Color.FromArgb(38, 72, 92), 9);
        };

        var objectiveTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Text = "Objectif RSE 2025",
            ForeColor = UiTheme.TextSecondary,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point)
        };

        var objectiveValues = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 22,
            ColumnCount = 2
        };
        objectiveValues.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        objectiveValues.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

        _rseProgressValue.Dock = DockStyle.Fill;
        _rseProgressValue.ForeColor = UiTheme.AccentGreen;
        _rseProgressValue.Font = new Font("Consolas", 9F, FontStyle.Bold, GraphicsUnit.Point);
        _rseProgressValue.Text = "-- kWh";

        _rseProgressPercentLabel.Dock = DockStyle.Fill;
        _rseProgressPercentLabel.ForeColor = UiTheme.TextMuted;
        _rseProgressPercentLabel.TextAlign = ContentAlignment.MiddleRight;
        _rseProgressPercentLabel.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
        _rseProgressPercentLabel.Text = "-- %";

        objectiveValues.Controls.Add(_rseProgressValue, 0, 0);
        objectiveValues.Controls.Add(_rseProgressPercentLabel, 1, 0);

        _rseProgressTrack.Dock = DockStyle.Top;
        _rseProgressTrack.Height = 4;
        _rseProgressTrack.BackColor = Color.FromArgb(34, 50, 63);
        _rseProgressTrack.Margin = new Padding(0, 8, 0, 8);
        _rseProgressTrack.Resize += (_, _) => UpdateRseProgressBar();

        _rseProgressFill.Dock = DockStyle.Left;
        _rseProgressFill.Width = 0;
        _rseProgressFill.BackColor = UiTheme.AccentGreen;
        _rseProgressTrack.Controls.Add(_rseProgressFill);

        var objectiveFoot = new Label
        {
            Dock = DockStyle.Top,
            Height = 18,
            Text = $"Objectif : {RseTargetKwh:F0} kWh",
            ForeColor = UiTheme.TextMuted,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Padding(0, 4, 0, 0)
        };

        objectiveCard.Controls.Add(objectiveFoot);
        objectiveCard.Controls.Add(_rseProgressTrack);
        objectiveCard.Controls.Add(objectiveValues);
        objectiveCard.Controls.Add(objectiveTitle);

        sidebar.Controls.Add(objectiveCard);
        sidebar.Controls.Add(navContainer);
        sidebar.Controls.Add(caption);

        return sidebar;
    }

    private Button CreateNavigationButton(string text, string pageId)
    {
        var button = new Button
        {
            Width = 188,
            Height = 38,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.TextSecondary,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 6),
            Tag = pageId
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(10, 16, 28);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(22, 34, 54);
        button.Click += OnNavigationClicked;
        return button;
    }

    private void OnNavigationClicked(object? sender, EventArgs eventArgs)
    {
        if (sender is not Button button || button.Tag is not string pageId)
        {
            return;
        }

        SelectPage(pageId);
    }

    private void SelectPage(string pageId)
    {
        var index = pageId switch
        {
            "overview" => 0,
            "technicien" => 1,
            "rse" => 2,
            "previsions" => 3,
            _ => 0
        };

        if (_mainTabs.TabPages.Count > index)
        {
            _mainTabs.SelectedIndex = index;
        }

        UpdateNavigationVisuals();
    }

    private void UpdateNavigationVisuals()
    {
        var selectedId = _mainTabs.SelectedIndex switch
        {
            0 => "overview",
            1 => "technicien",
            2 => "rse",
            3 => "previsions",
            _ => "overview"
        };

        foreach (var (pageId, button) in _navigationButtons)
        {
            var selected = pageId == selectedId;
            button.BackColor = selected ? Color.FromArgb(17, 31, 56) : Color.Transparent;
            button.ForeColor = selected ? UiTheme.TextPrimary : UiTheme.TextSecondary;
            button.FlatAppearance.BorderColor = selected
                ? Color.FromArgb(46, 75, 120)
                : Color.FromArgb(10, 16, 28);
            button.Font = new Font(
                "Segoe UI",
                10F,
                selected ? FontStyle.Bold : FontStyle.Regular,
                GraphicsUnit.Point);
        }
    }

    private Control BuildTabs()
    {
        _mainTabs.Dock = DockStyle.Fill;
        _mainTabs.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        _mainTabs.Padding = new Point(0, 0);
        _mainTabs.DrawMode = TabDrawMode.Normal;
        _mainTabs.SizeMode = TabSizeMode.Fixed;
        _mainTabs.ItemSize = new Size(0, 1);
        _mainTabs.Appearance = TabAppearance.FlatButtons;
        _mainTabs.Multiline = true;
        _mainTabs.SelectedIndexChanged += (_, _) => UpdateNavigationVisuals();

        var overviewPage = new TabPage("Vue d'ensemble")
        {
            BackColor = BackColor,
            Padding = new Padding(18)
        };
        overviewPage.Controls.Add(BuildOverviewView());

        var technicianPage = new TabPage("Technicien")
        {
            BackColor = BackColor,
            Padding = new Padding(18)
        };
        technicianPage.Controls.Add(BuildTechnicianView());

        var rsePage = new TabPage("RSE / Objectifs")
        {
            BackColor = BackColor,
            Padding = new Padding(18)
        };
        rsePage.Controls.Add(BuildRseView());

        var forecastPage = new TabPage("Previsions")
        {
            BackColor = BackColor,
            Padding = new Padding(18)
        };
        forecastPage.Controls.Add(BuildForecastView());

        _mainTabs.TabPages.Clear();
        _mainTabs.TabPages.Add(overviewPage);
        _mainTabs.TabPages.Add(technicianPage);
        _mainTabs.TabPages.Add(rsePage);
        _mainTabs.TabPages.Add(forecastPage);
        _mainTabs.SelectedIndex = 0;

        UpdateNavigationVisuals();
        return _mainTabs;
    }

    private Control BuildOverviewView()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));

        var cards = BuildMetricStrip(
            ("Consommation totale", _overviewTotalValue, _overviewCoverageCaption, UiTheme.Brand),
            ("Cout estime", _overviewCostValue, _overviewTariffCaption, UiTheme.AccentAmber),
            ("Empreinte CO2", _overviewCo2Value, _overviewCo2Caption, UiTheme.AccentGreen),
            ("Moyenne journaliere", _overviewAverageValue, _overviewAverageCaption, UiTheme.AccentIndigo));

        var middle = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));

        var chartSection = CreateSectionPanel("Consommation journaliere", _overviewDailyChart, new Padding(0, 0, 14, 14));
        var anomalySection = CreateSectionPanel("Alertes en cours", _overviewAnomaliesList, new Padding(0, 0, 0, 14));

        middle.Controls.Add(chartSection, 0, 0);
        middle.Controls.Add(anomalySection, 1, 0);

        var bottom = CreateSectionPanel("Synthese quotidienne", _overviewDailyList, Padding.Empty);

        root.Controls.Add(cards, 0, 0);
        root.Controls.Add(middle, 0, 1);
        root.Controls.Add(bottom, 0, 2);
        return root;
    }

    private Control BuildTechnicianView()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
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

    private Control BuildForecastView()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));

        var cards = BuildMetricStrip(
            ("Tendance 7 jours", _forecastTrendValue, _forecastTrendCaption, UiTheme.AccentOrange),
            ("Prevision J+1", _forecastJ1Value, _forecastJ1Caption, UiTheme.AccentIndigo),
            ("Prevision J+7", _forecastJ7Value, _forecastJ7Caption, UiTheme.AccentAmber));

        var chartSection = CreateSectionPanel("Projection J-7 a J+7", _forecastChart, new Padding(0, 0, 0, 14));
        var tableSection = CreateSectionPanel("Tableau de prevision", _forecastTable, Padding.Empty);

        root.Controls.Add(cards, 0, 0);
        root.Controls.Add(chartSection, 0, 1);
        root.Controls.Add(tableSection, 0, 2);
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
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
            BackColor = UiTheme.Surface
        };

        panel.Paint += (_, args) =>
        {
            var bounds = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            DrawRoundedPanel(args.Graphics, bounds, UiTheme.Surface, UiTheme.Border, CardCornerRadius);

            using var accentBrush = new SolidBrush(accentColor);
            args.Graphics.FillRectangle(accentBrush, 0, 0, 8, panel.Height);

            using var topLineBrush = new SolidBrush(ControlPaint.Light(accentColor, 0.12f));
            args.Graphics.FillRectangle(topLineBrush, 8, 0, panel.Width - 8, 2);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = UiTheme.TextSecondary,
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
            BackColor = UiTheme.Surface
        };

        panel.Paint += (_, args) =>
        {
            var bounds = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            DrawRoundedPanel(args.Graphics, bounds, UiTheme.Surface, UiTheme.Border, CardCornerRadius);
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
            ForeColor = UiTheme.TextPrimary,
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
        grid.BackgroundColor = UiTheme.Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 34;
        grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.BrandSoft;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);
        grid.DefaultCellStyle.BackColor = UiTheme.Surface;
        grid.DefaultCellStyle.ForeColor = UiTheme.TextPrimary;
        grid.DefaultCellStyle.Padding = new Padding(2);
        grid.AlternatingRowsDefaultCellStyle.BackColor = UiTheme.SurfaceAlt;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.DefaultCellStyle.SelectionBackColor = UiTheme.BrandSoft;
        grid.DefaultCellStyle.SelectionForeColor = UiTheme.TextPrimary;
        grid.RowTemplate.Height = 30;
        grid.RowHeadersVisible = false;
    }

    private void ConfigureAlertsList()
    {
        ConfigureListView(_alertsList);
        _alertsList.View = View.Details;
        _alertsList.FullRowSelect = true;
        _alertsList.GridLines = false;
        _alertsList.HideSelection = false;
        _alertsList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _alertsList.Columns.Add("Heure");
        _alertsList.Columns.Add("Niveau");
        _alertsList.Columns.Add("Message");
        _alertsList.Resize += (_, _) => ResizeAlertsColumns();
    }

    private void ConfigureOverviewAnomaliesList()
    {
        ConfigureListView(_overviewAnomaliesList);
        _overviewAnomaliesList.View = View.Details;
        _overviewAnomaliesList.FullRowSelect = true;
        _overviewAnomaliesList.GridLines = false;
        _overviewAnomaliesList.HideSelection = false;
        _overviewAnomaliesList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _overviewAnomaliesList.Columns.Add("Date");
        _overviewAnomaliesList.Columns.Add("Niveau");
        _overviewAnomaliesList.Columns.Add("Message");
        _overviewAnomaliesList.Resize += (_, _) => ResizeOverviewAnomaliesColumns();
    }

    private void ConfigureOverviewDailyList()
    {
        ConfigureListView(_overviewDailyList);
        _overviewDailyList.View = View.Details;
        _overviewDailyList.FullRowSelect = true;
        _overviewDailyList.GridLines = false;
        _overviewDailyList.HideSelection = false;
        _overviewDailyList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _overviewDailyList.Columns.Add("Jour");
        _overviewDailyList.Columns.Add("kWh");
        _overviewDailyList.Columns.Add("Cout");
        _overviewDailyList.Columns.Add("CO2");
        _overviewDailyList.Resize += (_, _) => ResizeOverviewDailyColumns();
    }

    private void ConfigureDailyAggregationList()
    {
        ConfigureListView(_dailyAggregationList);
        _dailyAggregationList.View = View.Details;
        _dailyAggregationList.FullRowSelect = true;
        _dailyAggregationList.GridLines = false;
        _dailyAggregationList.HideSelection = false;
        _dailyAggregationList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _dailyAggregationList.Columns.Add("Jour");
        _dailyAggregationList.Columns.Add("kWh");
        _dailyAggregationList.Resize += (_, _) => ResizeDailyAggregationColumns();
    }

    private void ConfigureRseTotalsList()
    {
        ConfigureListView(_rseTotalsList);
        _rseTotalsList.View = View.Details;
        _rseTotalsList.FullRowSelect = true;
        _rseTotalsList.GridLines = false;
        _rseTotalsList.HideSelection = false;
        _rseTotalsList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _rseTotalsList.Columns.Add("Poste");
        _rseTotalsList.Columns.Add("kWh");
        _rseTotalsList.Columns.Add("Part");
        _rseTotalsList.Resize += (_, _) => ResizeRseTotalsColumns();
    }

    private void ConfigureForecastTable()
    {
        ConfigureListView(_forecastTable);
        _forecastTable.View = View.Details;
        _forecastTable.FullRowSelect = true;
        _forecastTable.GridLines = false;
        _forecastTable.HideSelection = false;
        _forecastTable.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _forecastTable.Columns.Add("Horizon");
        _forecastTable.Columns.Add("kWh");
        _forecastTable.Columns.Add("Basse");
        _forecastTable.Columns.Add("Haute");
        _forecastTable.Columns.Add("Cout");
        _forecastTable.Columns.Add("CO2");
        _forecastTable.Resize += (_, _) => ResizeForecastColumns();
    }

    private void ConfigureTechnicianChartModeSelector()
    {
        _technicianChartModeCaption.Dock = DockStyle.Fill;
        _technicianChartModeCaption.TextAlign = ContentAlignment.MiddleLeft;
        _technicianChartModeCaption.ForeColor = UiTheme.TextSecondary;
        _technicianChartModeCaption.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        _technicianChartModeCaption.Text = "Vue brute : puissance par point du fichier technicien.";

        _technicianChartModeCombo.Dock = DockStyle.Fill;
        _technicianChartModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _technicianChartModeCombo.FlatStyle = FlatStyle.Flat;
        _technicianChartModeCombo.BackColor = UiTheme.Surface;
        _technicianChartModeCombo.ForeColor = UiTheme.TextPrimary;
        _technicianChartModeCombo.Items.AddRange(
            [
                "Dernieres mesures (W)",
                "Aggregation horaire (kWh)",
                "Aggregation journaliere (kWh)"
            ]);
        _technicianChartModeCombo.SelectedIndex = 0;
        _technicianChartModeCombo.SelectedIndexChanged += OnTechnicianChartModeChanged;
    }

    private void ConfigureCharts()
    {
        _powerChart.Dock = DockStyle.Fill;
        _powerChart.Margin = Padding.Empty;
        _overviewDailyChart.Dock = DockStyle.Fill;
        _overviewDailyChart.Margin = Padding.Empty;
        _monthlyTotalsChart.Dock = DockStyle.Fill;
        _monthlyTotalsChart.Margin = Padding.Empty;
        _forecastChart.Dock = DockStyle.Fill;
        _forecastChart.Margin = Padding.Empty;
    }

    private static void ConfigureListView(ListView listView)
    {
        listView.BackColor = UiTheme.Surface;
        listView.ForeColor = UiTheme.TextPrimary;
        listView.BorderStyle = BorderStyle.None;
        listView.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
    }

    private async void OnShownAsync(object? sender, EventArgs eventArgs)
    {
        try
        {
            await EnsureApiReadyAsync(_lifetimeCts.Token);
            await RefreshDashboardAsync(_lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            UpdateStatus($"Initialisation impossible : {exception.Message}");
        }
    }

    private async void OnReloadClickedAsync(object? sender, EventArgs eventArgs)
    {
        if (_refreshInProgress)
        {
            return;
        }

        _reloadButton.Enabled = false;
        UpdateStatus("Rechargement des CSV dans SQLite...");

        try
        {
            await _apiClient.ReloadAsync(_lifetimeCts.Token);
            await RefreshDashboardAsync(_lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            UpdateStatus($"Rechargement impossible : {exception.Message}");
        }
        finally
        {
            _reloadButton.Enabled = true;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        _clockTimer.Stop();
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        _apiProcessManager.Dispose();
        _apiClient.Dispose();
    }

    private void OnTechnicianChartModeChanged(object? sender, EventArgs eventArgs)
    {
        if (_currentSnapshot is not null)
        {
            UpdateTechnicianChart(_currentSnapshot);
        }
    }

    private async Task EnsureApiReadyAsync(CancellationToken cancellationToken)
    {
        UpdateStatus("Verification de l'API locale...");
        var startup = await _apiProcessManager.EnsureApiRunningAsync(_apiClient, cancellationToken);
        UpdateStatus(startup.Message);
    }

    private async Task RefreshDashboardAsync(CancellationToken cancellationToken)
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshInProgress = true;
        _reloadButton.Enabled = false;
        UpdateStatus("Chargement des donnees du dashboard...");

        try
        {
            var snapshot = await _apiClient.GetSnapshotAsync(cancellationToken);
            _currentSnapshot = snapshot;
            ApplySnapshot(snapshot);
            UpdateStatus(
                $"Vue technicien : {snapshot.Summary.CoverageStart:dd/MM/yyyy} -> {snapshot.Summary.CoverageEnd:dd/MM/yyyy} | " +
                $"Vue RSE : {snapshot.RseMonthlyBreakdowns.Count} mois charges jusqu'a {snapshot.Summary.LatestMonthLabel}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            UpdateStatus($"API indisponible : {exception.Message}");
        }
        finally
        {
            _reloadButton.Enabled = true;
            _refreshInProgress = false;
        }
    }

    private void ApplySnapshot(DashboardSnapshotDto snapshot)
    {
        UpdateOverview(snapshot);
        ApplyTechnicianSummary(snapshot);
        ApplyRseSummary(snapshot);
        UpdateForecast(snapshot);

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

    private void UpdateOverview(DashboardSnapshotDto snapshot)
    {
        var daily = snapshot.DailyConsumption
            .OrderBy(point => point.BucketStart)
            .TakeLast(30)
            .ToList();

        var total = daily.Sum(point => point.ValueKwh);
        var average = daily.Count == 0 ? 0d : total / daily.Count;
        var estimatedCost = total * EdfTariff;
        var estimatedCo2 = total * Co2Factor;

        _overviewTotalValue.Text = $"{total:F1} kWh";
        _overviewCostValue.Text = $"{estimatedCost:F0} EUR";
        _overviewCo2Value.Text = $"{estimatedCo2:F1} kg";
        _overviewAverageValue.Text = $"{average:F2} kWh";
        _overviewCoverageCaption.Text = $"{snapshot.Summary.CoverageStart:dd/MM} -> {snapshot.Summary.CoverageEnd:dd/MM}";
        _overviewTariffCaption.Text = $"Tarif EDF: {EdfTariff:F4} EUR/kWh";
        _overviewCo2Caption.Text = $"Facteur CO2: {Co2Factor:F4} kg/kWh";
        _overviewAverageCaption.Text = daily.Count == 0 ? "Aucune donnee" : $"{daily.Count} jours observes";

        _overviewDailyChart.SetAggregations(daily);

        _overviewAnomaliesList.BeginUpdate();
        _overviewAnomaliesList.Items.Clear();
        foreach (var alert in snapshot.Alerts.Take(8))
        {
            var item = new ListViewItem(alert.Timestamp.ToString("dd/MM HH:mm"));
            item.SubItems.Add(alert.Severity);
            item.SubItems.Add(alert.Message);
            item.ForeColor = alert.Severity == "Critique" ? UiTheme.AlertCritical : UiTheme.AlertWatch;
            _overviewAnomaliesList.Items.Add(item);
        }

        if (_overviewAnomaliesList.Items.Count == 0)
        {
            _overviewAnomaliesList.Items.Add(new ListViewItem(new[] { "-", "OK", "Aucune alerte critique." }));
        }

        _overviewAnomaliesList.EndUpdate();
        ResizeOverviewAnomaliesColumns();

        _overviewDailyList.BeginUpdate();
        _overviewDailyList.Items.Clear();
        foreach (var point in daily.OrderByDescending(item => item.BucketStart).Take(14))
        {
            var row = new ListViewItem(point.BucketStart.ToString("dd/MM"));
            row.SubItems.Add(point.ValueKwh.ToString("F2"));
            row.SubItems.Add($"{(point.ValueKwh * EdfTariff):F2} EUR");
            row.SubItems.Add($"{(point.ValueKwh * Co2Factor):F2} kg");
            _overviewDailyList.Items.Add(row);
        }

        if (_overviewDailyList.Items.Count == 0)
        {
            _overviewDailyList.Items.Add(new ListViewItem(new[] { "-", "0.00", "0.00 EUR", "0.00 kg" }));
        }

        _overviewDailyList.EndUpdate();
        ResizeOverviewDailyColumns();

        UpdateRseProgress(totalRseKwh: snapshot.Summary.AnnualRseKwh);
    }

    private void UpdateForecast(DashboardSnapshotDto snapshot)
    {
        var points = BuildForecastSeries(snapshot.DailyConsumption);
        _forecastChart.SetPoints(points);

        var slope = CalculateSlope(snapshot.DailyConsumption);
        _forecastTrendValue.Text = $"{slope:+0.000;-0.000;0.000} kWh/j";
        _forecastTrendCaption.Text = slope >= 0
            ? "Hausse progressive sur la fin de periode"
            : "Baisse progressive sur la fin de periode";

        var j1 = points.FirstOrDefault(point => point.IsPredicted && point.Label == "J+1");
        var j7 = points.FirstOrDefault(point => point.IsPredicted && point.Label == "J+7");
        _forecastJ1Value.Text = j1 is null ? "--" : $"{j1.Value:F2} kWh";
        _forecastJ7Value.Text = j7 is null ? "--" : $"{j7.Value:F2} kWh";
        _forecastJ1Caption.Text = j1 is null ? "Donnees insuffisantes" : $"Intervalle [{j1.Low:F2} ; {j1.High:F2}]";
        _forecastJ7Caption.Text = j7 is null ? "Donnees insuffisantes" : $"Intervalle [{j7.Low:F2} ; {j7.High:F2}]";

        _forecastTable.BeginUpdate();
        _forecastTable.Items.Clear();

        foreach (var forecast in points.Where(point => point.IsPredicted).Take(7))
        {
            var row = new ListViewItem(forecast.Label);
            row.SubItems.Add(forecast.Value.ToString("F2"));
            row.SubItems.Add(forecast.Low.ToString("F2"));
            row.SubItems.Add(forecast.High.ToString("F2"));
            row.SubItems.Add($"{(forecast.Value * EdfTariff):F2} EUR");
            row.SubItems.Add($"{(forecast.Value * Co2Factor):F2} kg");
            _forecastTable.Items.Add(row);
        }

        if (_forecastTable.Items.Count == 0)
        {
            _forecastTable.Items.Add(new ListViewItem(new[] { "-", "--", "--", "--", "--", "--" }));
        }

        _forecastTable.EndUpdate();
        ResizeForecastColumns();
    }

    private static double CalculateSlope(IReadOnlyList<EnergyAggregationPointDto> dailyAggregations)
    {
        var values = dailyAggregations
            .OrderBy(point => point.BucketStart)
            .TakeLast(10)
            .Select(point => point.ValueKwh)
            .ToList();

        if (values.Count < 3)
        {
            return 0d;
        }

        var n = values.Count;
        var xMean = (n - 1) / 2d;
        var yMean = values.Average();
        var numerator = 0d;
        var denominator = 0d;

        for (var index = 0; index < n; index++)
        {
            var x = index - xMean;
            numerator += x * (values[index] - yMean);
            denominator += x * x;
        }

        return denominator <= 0d ? 0d : numerator / denominator;
    }

    private static IReadOnlyList<ForecastPoint> BuildForecastSeries(IReadOnlyList<EnergyAggregationPointDto> dailyAggregations)
    {
        var ordered = dailyAggregations
            .OrderBy(point => point.BucketStart)
            .TakeLast(30)
            .ToList();

        if (ordered.Count < 5)
        {
            return Array.Empty<ForecastPoint>();
        }

        var lastTen = ordered.TakeLast(Math.Min(10, ordered.Count)).ToList();
        var n = lastTen.Count;
        var xMean = (n - 1) / 2d;
        var yMean = lastTen.Average(point => point.ValueKwh);
        var numerator = 0d;
        var denominator = 0d;

        for (var index = 0; index < n; index++)
        {
            var x = index - xMean;
            numerator += x * (lastTen[index].ValueKwh - yMean);
            denominator += x * x;
        }

        var slope = denominator <= 0d ? 0d : numerator / denominator;
        var intercept = yMean - (slope * xMean);

        var points = new List<ForecastPoint>();
        foreach (var history in ordered.TakeLast(Math.Min(7, ordered.Count)))
        {
            points.Add(new ForecastPoint(
                history.BucketStart.ToString("dd/MM"),
                history.ValueKwh,
                history.ValueKwh,
                history.ValueKwh,
                false));
        }

        for (var dayOffset = 1; dayOffset <= 7; dayOffset++)
        {
            var x = (n - 1) + dayOffset;
            var predicted = Math.Max(0d, intercept + (slope * x));
            var low = Math.Max(0d, predicted - 1.4d);
            var high = predicted + 1.4d;

            points.Add(new ForecastPoint(
                $"J+{dayOffset}",
                Math.Round(predicted, 2),
                Math.Round(low, 2),
                Math.Round(high, 2),
                true));
        }

        return points;
    }

    private void UpdateRseProgress(double totalRseKwh)
    {
        _rseProgressRatio = RseTargetKwh <= 0d ? 0d : Math.Clamp(totalRseKwh / RseTargetKwh, 0d, 1d);
        _rseProgressValue.Text = $"{totalRseKwh:F0} kWh";
        _rseProgressPercentLabel.Text = $"{(_rseProgressRatio * 100d):F0}%";
        UpdateRseProgressBar();
    }

    private void UpdateRseProgressBar()
    {
        if (_rseProgressTrack.Width <= 0)
        {
            return;
        }

        var width = Math.Max(0, (int)Math.Round(_rseProgressTrack.Width * _rseProgressRatio));
        _rseProgressFill.Width = width;
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
                ? UiTheme.AlertCritical
                : UiTheme.AlertWatch;

            _alertsList.Items.Add(item);
        }

        if (_alertsList.Items.Count == 0)
        {
            _alertsList.Items.Add(new ListViewItem(new[] { "-", "RAS", "Aucune alerte detectee." }));
        }

        _alertsList.EndUpdate();
        ResizeAlertsColumns();
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
        ResizeDailyAggregationColumns();
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
        ResizeRseTotalsColumns();
    }

    private void ResizeAlertsColumns()
    {
        ResizeListViewColumns(_alertsList, 0.25, 0.20, 0.55);
    }

    private void ResizeOverviewAnomaliesColumns()
    {
        ResizeListViewColumns(_overviewAnomaliesList, 0.28, 0.20, 0.52);
    }

    private void ResizeOverviewDailyColumns()
    {
        ResizeListViewColumns(_overviewDailyList, 0.28, 0.22, 0.25, 0.25);
    }

    private void ResizeDailyAggregationColumns()
    {
        ResizeListViewColumns(_dailyAggregationList, 0.60, 0.40);
    }

    private void ResizeRseTotalsColumns()
    {
        ResizeListViewColumns(_rseTotalsList, 0.48, 0.24, 0.28);
    }

    private void ResizeForecastColumns()
    {
        ResizeListViewColumns(_forecastTable, 0.16, 0.16, 0.16, 0.16, 0.18, 0.18);
    }

    private static void ResizeListViewColumns(ListView listView, params double[] ratios)
    {
        if (listView.Columns.Count == 0 || listView.ClientSize.Width <= 0 || ratios.Length != listView.Columns.Count)
        {
            return;
        }

        var availableWidth = Math.Max(120, listView.ClientSize.Width - 6);
        var consumed = 0;

        for (var index = 0; index < listView.Columns.Count; index++)
        {
            var width = index == listView.Columns.Count - 1
                ? Math.Max(40, availableWidth - consumed)
                : Math.Max(40, (int)Math.Floor(availableWidth * ratios[index]));

            listView.Columns[index].Width = width;
            consumed += width;
        }
    }

    private static void DrawRoundedPanel(Graphics graphics, Rectangle bounds, Color fillColor, Color borderColor, int radius)
    {
        using var path = CreateRoundedPath(bounds, radius);
        using var fillBrush = new SolidBrush(fillColor);
        using var borderPen = new Pen(borderColor, 1F);
        graphics.FillPath(fillBrush, path);
        graphics.DrawPath(borderPen, path);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var clampedRadius = Math.Max(2, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
        var diameter = clampedRadius * 2;

        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void UpdateStatus(string message)
    {
        if (!IsDisposed)
        {
            _statusLabel.Text = message;
        }
    }

    private static string ResolveApiBaseAddress()
    {
        var configured = Environment.GetEnvironmentVariable("DASHBOARD_API_URL");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return "http://localhost:5188/";
        }

        var normalized = configured.Trim();
        if (!normalized.EndsWith('/'))
        {
            normalized += "/";
        }

        return Uri.TryCreate(normalized, UriKind.Absolute, out _)
            ? normalized
            : "http://localhost:5188/";
    }

    private static Label CreateMetricValueLabel(string? initialText = null)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = initialText ?? "--",
            ForeColor = UiTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point)
        };
    }

    private static Label CreateMetricCaptionLabel(string? initialText = null)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = initialText ?? string.Empty,
            ForeColor = UiTheme.TextMuted,
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
