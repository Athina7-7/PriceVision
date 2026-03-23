namespace PriceVision.Application.Contracts;

public sealed record PredictionSummaryResponse(
    Guid PredictionId,
    Guid ProjectId,
    string ProjectName,
    float AreaM2,
    string Type,
    string Location,
    float DurationMonths,
    decimal BaseCostCop,
    bool PredictedMaterials,
    bool PredictedLabor,
    float EstimatedMaterialQuantity,
    decimal EstimatedMaterialCostCop,
    float RequiredLaborHours,
    DateTime CreatedAtUtc);
