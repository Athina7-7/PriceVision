namespace PriceVision.Domain.Entities;

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public float AreaM2 { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public float DurationMonths { get; set; }
    public decimal BaseCostCop { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
