namespace PriceVision.Domain.Entities;

public sealed class Prediction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public float AreaM2 { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public bool PredictedMaterials { get; set; }
    public bool PredictedLabor { get; set; }
    public float EstimatedMaterialQuantity { get; set; }
    public decimal EstimatedMaterialCostCop { get; set; }
    public float RequiredLaborHours { get; set; }
    public string ModelType { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
