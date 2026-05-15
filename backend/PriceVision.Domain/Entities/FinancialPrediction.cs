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
    public decimal StandardError { get; set; }
    public decimal ConfidenceIntervalLower { get; set; }
    public decimal ConfidenceIntervalUpper { get; set; }
    public string ConfidenceExplanation { get; set; } = string.Empty;
    public decimal HistoricalAverageCostPerM2Cop { get; set; }
    public decimal LocationTrendFactor { get; set; }
    public string ModelType { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
