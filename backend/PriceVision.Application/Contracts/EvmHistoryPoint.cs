namespace PriceVision.Application.Contracts;

public sealed record EvmHistoryPoint(
    DateTime PeriodDateUtc,
    decimal PV,
    decimal EV,
    decimal AC,
    decimal CPI,
    decimal SPI,
    string CostInterpretation,
    string ScheduleInterpretation
);
