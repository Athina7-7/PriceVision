namespace PriceVision.Infrastructure.Ml;

public sealed class PredictionInputModel
{
    public float AreaM2 { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public float DurationDays { get; set; }
}
