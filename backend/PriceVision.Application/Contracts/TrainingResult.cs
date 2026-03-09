namespace PriceVision.Application.Contracts;

public sealed record TrainingResult(
    int DatasetRows,
    double MaterialQuantityR2,
    double MaterialQuantityRmse,
    double LaborHoursR2,
    double LaborHoursRmse,
    string ModelVersion
);
