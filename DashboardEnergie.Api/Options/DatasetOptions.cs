namespace DashboardEnergie.Api.Options;

public sealed class DatasetOptions
{
    public const string SectionName = "Datasets";

    public string TechnicianCsvPath { get; set; } = "../Data/technician_dataset.csv";

    public string RseCsvPath { get; set; } = "../Data/rse_dataset_detailed.csv";

    public int AlertThresholdWatts { get; set; } = 1500;

    public string TechnicianSourceName { get; set; } = "technician_dataset.csv";

    public string RseSourceName { get; set; } = "rse_dataset_detailed.csv";
}
