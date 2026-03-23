namespace PriceVision.Application.Contracts;

public sealed record EvmCalculationResponse(
    Guid RecordId,
    Guid ProjectId,
    DateTime PeriodDateUtc,
    decimal PV,
    decimal EV,
    decimal AC,
    decimal CPI,
    decimal SPI,
    string CostInterpretation,
    string ScheduleInterpretation
);
