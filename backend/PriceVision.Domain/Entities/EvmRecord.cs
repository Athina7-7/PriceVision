namespace PriceVision.Domain.Entities;

public sealed class EvmRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public DateTime PeriodDateUtc { get; set; }
    public decimal PV { get; set; }
    public decimal EV { get; set; }
    public decimal AC { get; set; }
    public decimal CPI { get; set; }
    public decimal SPI { get; set; }
    public string CostInterpretation { get; set; } = string.Empty;
    public string ScheduleInterpretation { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
