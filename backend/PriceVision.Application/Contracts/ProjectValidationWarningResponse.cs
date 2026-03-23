namespace PriceVision.Application.Contracts;

public sealed record ProjectValidationWarningResponse(
    string Code,
    string Title,
    string Message);
