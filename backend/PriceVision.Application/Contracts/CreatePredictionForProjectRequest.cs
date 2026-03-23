namespace PriceVision.Application.Contracts;

public sealed record CreatePredictionForProjectRequest(
    bool PredictMaterials,
    bool PredictLabor);
