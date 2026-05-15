namespace PriceVision.Application.Contracts;

public sealed record SimulationResult(
    Guid ProjectId,
    string ProjectName,
    DateTime SimulatedAtUtc,
    IReadOnlyList<SimulationMetricComparison> Metrics,
    decimal OriginalEstimatedTotalCostCop,
    decimal SimulatedEstimatedTotalCostCop,
    decimal EstimatedTotalCostDifferenceCop,
    decimal EstimatedTotalCostPercentageDifference);
