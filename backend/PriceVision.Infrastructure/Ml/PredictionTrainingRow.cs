using Microsoft.ML.Data;

namespace PriceVision.Infrastructure.Ml;

public sealed class PredictionTrainingRow
{
    [LoadColumn(0)] public float AreaM2 { get; set; }
    [LoadColumn(1)] public string Type { get; set; } = string.Empty;
    [LoadColumn(2)] public string Location { get; set; } = string.Empty;
    [LoadColumn(3)] public float DurationDays { get; set; }
    [LoadColumn(4)] public float MaterialQuantity { get; set; }
    [LoadColumn(5)] public float LaborHours { get; set; }
}
