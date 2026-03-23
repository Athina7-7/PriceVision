namespace PriceVision.Application.Contracts;

public sealed record CreateProjectRequest(
    string Name,
    float AreaM2,
    string Location,
    string Type,
    float DurationMonths,
    decimal BaseCostCop);
