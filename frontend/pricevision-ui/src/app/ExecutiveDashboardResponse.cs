using System;

namespace PriceVision.Application.Contracts;

public sealed record ExecutiveDashboardResponse(
    Guid ProjectId,
    string ProjectName,
    decimal EstimatedTotalCostCop,
    string RiskLevel,
    string RiskDescription,
    decimal? CPI,
    decimal? SPI,
    decimal ProjectedDeviationCop,
    decimal ProjectedDeviationPercentage,
    DateTime LastUpdatedUtc
);