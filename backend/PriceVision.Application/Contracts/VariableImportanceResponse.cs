namespace PriceVision.Application.Contracts;

public sealed record VariableImportanceResponse(
    string TechnicalName,
    string DisplayName,
    decimal Coefficient,
    decimal AbsoluteCoefficient,
    decimal ImportancePercentage,
    int Rank,
    string Direction,
    string Interpretation);
