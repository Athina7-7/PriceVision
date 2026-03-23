namespace PriceVision.Application.Contracts;

public sealed record CreateProjectResponse(
    ProjectSummaryResponse Project,
    IReadOnlyList<ProjectValidationWarningResponse> ValidationWarnings);
