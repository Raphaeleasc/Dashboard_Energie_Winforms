using DashboardEnergie.Api.Data;
using DashboardEnergie.Api.Options;
using DashboardEnergie.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DashboardEnergie.Tests;

public sealed class CsvImportAndSnapshotTests
{
    [Fact]
    public async Task ReloadAsync_ImportsAllRowsFromBothCsvFiles()
    {
        await using var harness = await TestHarness.CreateAsync();

        var importService = harness.CreateImportService();
        await importService.ReloadAsync();

        Assert.Equal(720, await harness.DbContext.TechnicianReadings.CountAsync());
        Assert.Equal(12, await harness.DbContext.RseMonthlyBreakdowns.CountAsync());
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsExpectedCoverageAndRseTotals()
    {
        await using var harness = await TestHarness.CreateAsync();

        var importService = harness.CreateImportService();
        await importService.ReloadAsync();

        var queryService = harness.CreateQueryService();
        var snapshot = await queryService.GetSnapshotAsync();

        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0), snapshot.Summary.CoverageStart);
        Assert.Equal(new DateTime(2025, 1, 30, 23, 0, 0), snapshot.Summary.CoverageEnd);
        Assert.Equal(4631.1, snapshot.Summary.AnnualRseKwh);
        Assert.Equal("2025-12", snapshot.Summary.LatestMonthLabel);
        Assert.Equal(24, snapshot.HourlyConsumption.Count);
        Assert.Equal(12, snapshot.RseMonthlyBreakdowns.Count);
        Assert.Contains(snapshot.RseCategoryTotals, item => item.Category == "Chauffage");
    }

    private sealed class TestHarness : IAsyncDisposable
    {
        private readonly string _databasePath;

        private TestHarness(EnergyDbContext dbContext, string databasePath, string repoRoot)
        {
            DbContext = dbContext;
            _databasePath = databasePath;
            ContentRootPath = Path.Combine(repoRoot, "DashboardEnergie.Api");
            Options = Microsoft.Extensions.Options.Options.Create(new DatasetOptions());
        }

        public EnergyDbContext DbContext { get; }

        public string ContentRootPath { get; }

        public IOptions<DatasetOptions> Options { get; }

        public static async Task<TestHarness> CreateAsync()
        {
            var repoRoot = FindRepoRoot();
            var tempDirectory = Path.Combine(Path.GetTempPath(), "dashboard-energie-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            var databasePath = Path.Combine(tempDirectory, "dashboard-tests.db");
            var options = new DbContextOptionsBuilder<EnergyDbContext>()
                .UseSqlite($"Data Source={databasePath};Pooling=False")
                .Options;

            var dbContext = new EnergyDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            return new TestHarness(dbContext, databasePath, repoRoot);
        }

        public CsvDatasetImportService CreateImportService()
        {
            return new CsvDatasetImportService(
                DbContext,
                new TestWebHostEnvironment(ContentRootPath),
                Options,
                NullLogger<CsvDatasetImportService>.Instance);
        }

        public EnergyQueryService CreateQueryService()
        {
            return new EnergyQueryService(DbContext, Options);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();

            if (File.Exists(_databasePath))
            {
                try
                {
                    File.Delete(_databasePath);
                }
                catch (IOException)
                {
                }
            }

            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static string FindRepoRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "DashboardEnergie.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate the repository root.");
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DashboardEnergie.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
