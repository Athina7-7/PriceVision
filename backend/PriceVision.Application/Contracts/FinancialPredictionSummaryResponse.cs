namespace PriceVision.Application.Contracts;

public sealed record FinancialPredictionSummaryResponse(
    Guid FinancialPredictionId,
    Guid ProjectId,
    string ProjectName,
    float AreaM2,
    string Type,
    string Location,
    float DurationMonths,
    decimal BaseCostCop,
    decimal EstimatedTotalCostCop,
    decimal MinimumEstimatedCostCop,
    decimal MaximumEstimatedCostCop,
    float ConfidencePercentage,
    string ConfidenceLevel,
    decimal HistoricalAverageCostPerM2Cop,
    decimal LocationTrendFactor,
    DateTime CreatedAtUtc);
