using DashboardEnergie.Api.Services;
using DashboardEnergie.Shared;
using Microsoft.AspNetCore.Mvc;

namespace DashboardEnergie.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController(EnergyQueryService queryService) : ControllerBase
{
    [HttpGet("snapshot")]
    public async Task<ActionResult<DashboardSnapshotDto>> GetSnapshot(CancellationToken cancellationToken)
    {
        var snapshot = await queryService.GetSnapshotAsync(cancellationToken);
        return Ok(snapshot);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await queryService.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    [HttpGet("latest")]
    public async Task<ActionResult<IReadOnlyList<EnergyReadingDto>>> GetLatest(
        [FromQuery] int count = 18,
        CancellationToken cancellationToken = default)
    {
        var readings = await queryService.GetLatestReadingsAsync(count, cancellationToken);
        return Ok(readings);
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<IReadOnlyList<EnergyAlertDto>>> GetAlerts(
        [FromQuery] int count = 8,
        CancellationToken cancellationToken = default)
    {
        var alerts = await queryService.GetAlertsAsync(count, cancellationToken);
        return Ok(alerts);
    }

    [HttpGet("aggregations")]
    public async Task<ActionResult<IReadOnlyList<EnergyAggregationPointDto>>> GetAggregations(
        [FromQuery] AggregationPeriod period = AggregationPeriod.Hour,
        [FromQuery] int points = 12,
        CancellationToken cancellationToken = default)
    {
        var aggregations = await queryService.GetAggregationsAsync(period, points, cancellationToken);
        return Ok(aggregations);
    }
}
