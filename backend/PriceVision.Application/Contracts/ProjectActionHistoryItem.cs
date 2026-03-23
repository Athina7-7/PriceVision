namespace PriceVision.Application.Contracts;

public sealed record ProjectActionHistoryItem(
    string ActionType,
    DateTime OccurredAtUtc,
    string Title,
    string Summary);
