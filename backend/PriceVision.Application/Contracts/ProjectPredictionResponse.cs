namespace PriceVision.Application.Contracts;

public sealed record ProjectPredictionResponse(
    Guid PredictionId,
    Guid ProjectId,
    string Name,
    float AreaM2,
    string Location,
    string Type,
    float DurationMonths,
    decimal BaseCostCop,
    DateTime CreatedAtUtc,
    bool PredictMaterials,
    bool PredictLabor,
    string ModelType,
    string ModelVersion,
    MaterialsEstimate? MaterialesEstimados,
    float? ManoObraRequeridaHorasPersona);
