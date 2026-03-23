namespace PriceVision.Domain.Entities;

public sealed class FinancialPrediction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public decimal EstimatedTotalCostCop { get; set; }
    public decimal MinimumEstimatedCostCop { get; set; }
    public decimal MaximumEstimatedCostCop { get; set; }
    public float ConfidencePercentage { get; set; }
    public string ConfidenceLevel { get; set; } = string.Empty;
    public decimal HistoricalAverageCostPerM2Cop { get; set; }
    public decimal LocationTrendFactor { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
