namespace PriceVision.Application.Contracts;

public sealed record EvmSummaryResponse(
    Guid RecordId,
    Guid ProjectId,
    string ProjectName,
    float AreaM2,
    string Type,
    string Location,
    float DurationMonths,
    decimal BaseCostCop,
    DateTime PeriodDateUtc,
    decimal PV,
    decimal EV,
    decimal AC,
    decimal CPI,
    decimal SPI,
    string CostInterpretation,
    string ScheduleInterpretation,
    DateTime CreatedAtUtc);
