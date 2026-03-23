namespace PriceVision.Application.Contracts;

public sealed record ProjectSummaryResponse(
    Guid ProjectId,
    string Name,
    float AreaM2,
    string Location,
    string Type,
    float DurationMonths,
    decimal BaseCostCop,
    DateTime CreatedAtUtc,
    bool HasPrediction,
    bool HasMaterialsPrediction,
    bool HasLaborPrediction,
    bool HasFinancialPrediction,
    bool HasEvm);
