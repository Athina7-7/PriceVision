namespace PriceVision.Application.Contracts;

public sealed record CreateProjectPredictionRequest(
    float AreaM2,
    string Location,
    string Type,
    float DurationMonths,
    decimal BaseCostCop,
    bool PredictMaterials,
    bool PredictLabor);
