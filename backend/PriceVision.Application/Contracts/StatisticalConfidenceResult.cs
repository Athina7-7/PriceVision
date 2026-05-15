namespace PriceVision.Application.Contracts;

public sealed record StatisticalConfidenceResult(
    float ConfidencePercentage,
    string ConfidenceLevel,
    decimal StandardError,
    decimal ConfidenceIntervalLower,
    decimal ConfidenceIntervalUpper,
    string ConfidenceExplanation);
