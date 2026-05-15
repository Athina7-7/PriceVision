namespace PriceVision.Application.Contracts;

public sealed record SimulationMetricComparison(
    string Label,
    decimal OriginalValue,
    decimal SimulatedValue,
    decimal AbsoluteDifference,
    decimal PercentageDifference);
