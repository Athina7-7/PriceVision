using System;

namespace PriceVision.Application.Contracts;

public sealed record SimilarProjectResponse(
    Guid ProjectId,
    string ProjectName,
    string Type,
    string Location,
    float AreaM2,
    float DurationMonths,
    decimal BaseCostCop,
    decimal SimilarityPercentage,
    decimal CostDifferencePercentage,
    decimal DurationDifferencePercentage,
    DateTime CreatedAtUtc
);