namespace PriceVision.Application.Contracts;

public sealed record PredictionRequest(
    Guid ProjectId,
    float AreaM2,
    string Type,
    string Location,
    float Duration,
    string DurationUnit
);
