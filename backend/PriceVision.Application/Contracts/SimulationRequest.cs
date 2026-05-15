namespace PriceVision.Application.Contracts;

public sealed record SimulationRequest(
    float SimulatedDurationMonths,
    decimal SimulatedBaseCostCop);
