using PriceVision.Api.Reports;
using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;
using PriceVision.Domain.Entities;
using PriceVision.Infrastructure;
using PriceVision.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ProjectPdfReportGenerator>();
builder.Services.AddSingleton<ProjectExcelReportGenerator>();

var corsPolicyName = "AllowFrontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PriceVisionDbContext>();
    await DatabaseSchemaInitializer.EnsureSchemaAsync(dbContext);
}

app.UseCors(corsPolicyName);
app.UseHttpsRedirection();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
   .WithName("HealthCheck");

app.MapGet("/api/projects", async (
    int? take,
    IProjectRepository projectRepository,
    IPredictionRepository predictionRepository,
    IFinancialPredictionRepository financialPredictionRepository,
    IEvmRepository evmRepository,
    CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 20, 1, 100);
    var projects = await projectRepository.GetRecentAsync(limit, cancellationToken);

    var response = new List<ProjectSummaryResponse>();
    foreach (var project in projects)
    {
        var hasPrediction = await predictionRepository.ExistsForProjectAsync(project.Id, cancellationToken);
        var hasMaterialsPrediction = await predictionRepository.ExistsForProjectAsync(project.Id, predictedMaterials: true, predictedLabor: false, cancellationToken);
        var hasLaborPrediction = await predictionRepository.ExistsForProjectAsync(project.Id, predictedMaterials: false, predictedLabor: true, cancellationToken);
        var hasFinancialPrediction = await financialPredictionRepository.ExistsForProjectAsync(project.Id, cancellationToken);
        var hasEvm = await evmRepository.ExistsForProjectAsync(project.Id, cancellationToken);
        response.Add(new ProjectSummaryResponse(
            ProjectId: project.Id,
            Name: project.Name,
            AreaM2: project.AreaM2,
            Location: project.Location,
            Type: project.Type,
            DurationMonths: project.DurationMonths,
            BaseCostCop: project.BaseCostCop,
            CreatedAtUtc: project.CreatedAtUtc,
            HasPrediction: hasPrediction,
            HasMaterialsPrediction: hasMaterialsPrediction,
            HasLaborPrediction: hasLaborPrediction,
            HasFinancialPrediction: hasFinancialPrediction,
            HasEvm: hasEvm));
    }

    return Results.Ok(response);
})
.WithName("GetRecentProjects");

app.MapGet("/api/projects/{projectId:guid}/history", async (
    Guid projectId,
    IProjectRepository projectRepository,
    IPredictionRepository predictionRepository,
    IFinancialPredictionRepository financialPredictionRepository,
    IEvmRepository evmRepository,
    CancellationToken cancellationToken) =>
{
    var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
    if (project is null)
    {
        return Results.NotFound();
    }

    var predictions = await predictionRepository.GetByProjectIdAsync(projectId, cancellationToken);
    var financialPrediction = await financialPredictionRepository.GetByProjectIdAsync(projectId, cancellationToken);
    var evmRecords = await evmRepository.GetByProjectIdAsync(projectId, 120, cancellationToken);

    var history = new List<ProjectActionHistoryItem>
    {
    };

    history.AddRange(predictions.Select(prediction => new ProjectActionHistoryItem(
        ActionType: "prediction",
        OccurredAtUtc: prediction.CreatedAtUtc,
        Title: "Prediccion generada",
        Summary: BuildPredictionHistorySummary(prediction))));

    if (financialPrediction is not null)
    {
        history.Add(new ProjectActionHistoryItem(
            ActionType: "financial_prediction",
            OccurredAtUtc: financialPrediction.CreatedAtUtc,
            Title: "Prediccion financiera generada",
            Summary: $"Costo total {financialPrediction.EstimatedTotalCostCop:N0} COP, rango {financialPrediction.MinimumEstimatedCostCop:N0} - {financialPrediction.MaximumEstimatedCostCop:N0} COP, confianza {financialPrediction.ConfidencePercentage:N0}% ({financialPrediction.ConfidenceLevel})"));
    }

    history.AddRange(evmRecords.Select(record => new ProjectActionHistoryItem(
        ActionType: "evm",
        OccurredAtUtc: record.CreatedAtUtc,
        Title: "Calculo EVM guardado",
        Summary: $"PV {record.PV:N0}, EV {record.EV:N0}, AC {record.AC:N0}, CPI {record.CPI:N2}, SPI {record.SPI:N2}")));

    return Results.Ok(history.OrderByDescending(x => x.OccurredAtUtc));
})
.WithName("GetProjectActionHistory");

app.MapPost("/api/projects", async (
    CreateProjectRequest request,
    IProjectValidationService projectValidationService,
    IProjectRepository projectRepository,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "El nombre del proyecto es obligatorio." });
    }

    if (request.AreaM2 <= 0)
    {
        return Results.BadRequest(new { error = "El area debe ser mayor que cero." });
    }

    if (request.DurationMonths <= 0)
    {
        return Results.BadRequest(new { error = "La duracion debe ser mayor que cero." });
    }

    if (request.BaseCostCop < 0)
    {
        return Results.BadRequest(new { error = "Los costos base no pueden ser negativos." });
    }

    if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Location))
    {
        return Results.BadRequest(new { error = "Tipo de proyecto y ubicacion son obligatorios." });
    }

    var warnings = await projectValidationService.ValidateAsync(request, cancellationToken);

    var project = new Project
    {
        Name = request.Name.Trim(),
        AreaM2 = request.AreaM2,
        Location = request.Location.Trim(),
        Type = request.Type.Trim(),
        DurationMonths = request.DurationMonths,
        BaseCostCop = request.BaseCostCop,
        CreatedAtUtc = DateTime.UtcNow
    };

    await projectRepository.AddAsync(project, cancellationToken);

    var projectSummary = new ProjectSummaryResponse(
        ProjectId: project.Id,
        Name: project.Name,
        AreaM2: project.AreaM2,
        Location: project.Location,
        Type: project.Type,
        DurationMonths: project.DurationMonths,
        BaseCostCop: project.BaseCostCop,
        CreatedAtUtc: project.CreatedAtUtc,
        HasPrediction: false,
        HasMaterialsPrediction: false,
        HasLaborPrediction: false,
        HasFinancialPrediction: false,
        HasEvm: false);

    return Results.Ok(new CreateProjectResponse(projectSummary, warnings));
})
.WithName("CreateProject");

app.MapPost("/api/projects/{projectId:guid}/financial-predict", async (
    Guid projectId,
    IFinancialForecastService financialForecastService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await financialForecastService.CreateForProjectAsync(projectId, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateFinancialPredictionForProject");

app.MapPost("/api/projects/{projectId:guid}/predict", async (
    Guid projectId,
    CreatePredictionForProjectRequest request,
    IProjectRepository projectRepository,
    IPredictiveModelService predictiveModelService,
    IPredictionRepository predictionRepository,
    IModelTrainingService trainingService,
    CancellationToken cancellationToken) =>
{
    var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
    if (project is null)
    {
        return Results.NotFound();
    }

    var hasMaterialsPrediction = await predictionRepository.ExistsForProjectAsync(projectId, predictedMaterials: true, predictedLabor: false, cancellationToken);
    var hasLaborPrediction = await predictionRepository.ExistsForProjectAsync(projectId, predictedMaterials: false, predictedLabor: true, cancellationToken);

    if (request.PredictMaterials && hasMaterialsPrediction)
    {
        return Results.BadRequest(new { error = "Este proyecto ya tiene una prediccion de materiales registrada." });
    }

    if (request.PredictLabor && hasLaborPrediction)
    {
        return Results.BadRequest(new { error = "Este proyecto ya tiene una prediccion de mano de obra registrada." });
    }

    if (!request.PredictMaterials && !request.PredictLabor)
    {
        return Results.BadRequest(new { error = "Selecciona al menos un modelo de prediccion." });
    }

    var predictionRequest = new PredictionRequest(
        ProjectId: projectId,
        AreaM2: project.AreaM2,
        Type: project.Type,
        Location: project.Location,
        Duration: project.DurationMonths,
        DurationUnit: "meses");

    PredictionResult prediction;
    try
    {
        prediction = predictiveModelService.Predict(predictionRequest);
    }
    catch (InvalidOperationException)
    {
        trainingService.Train(3000);
        prediction = predictiveModelService.Predict(predictionRequest);
    }

    var predictionEntity = predictiveModelService.BuildPredictionEntity(
        predictionRequest,
        prediction,
        predictedMaterials: request.PredictMaterials,
        predictedLabor: request.PredictLabor);
    await predictionRepository.AddAsync(predictionEntity, cancellationToken);

    var response = new ProjectPredictionResponse(
        PredictionId: predictionEntity.Id,
        ProjectId: project.Id,
        Name: project.Name,
        AreaM2: project.AreaM2,
        Location: project.Location,
        Type: project.Type,
        DurationMonths: project.DurationMonths,
        BaseCostCop: project.BaseCostCop,
        CreatedAtUtc: predictionEntity.CreatedAtUtc,
        PredictMaterials: request.PredictMaterials,
        PredictLabor: request.PredictLabor,
        MaterialesEstimados: request.PredictMaterials ? prediction.MaterialesEstimados : null,
        ManoObraRequeridaHorasPersona: request.PredictLabor ? prediction.ManoObraRequeridaHorasPersona : null);

    return Results.Ok(response);
})
.WithName("CreatePredictionForProject");

app.MapPost("/api/predictions/train", (TrainModelRequest? request, IModelTrainingService trainingService) =>
{
    var rows = request?.Rows ?? 3000;
    var result = trainingService.Train(rows);

    return Results.Ok(result);
})
.WithName("TrainPredictionModel");

app.MapPost("/api/predictions", async (PredictionRequest request, IPredictiveModelService predictiveModelService, IPredictionRepository repository, CancellationToken cancellationToken) =>
{
    if (request.ProjectId == Guid.Empty)
    {
        return Results.BadRequest(new { error = "ProjectId es obligatorio." });
    }

    if (request.AreaM2 <= 0)
    {
        return Results.BadRequest(new { error = "AreaM2 debe ser mayor que cero." });
    }

    if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Location))
    {
        return Results.BadRequest(new { error = "Type y Location son obligatorios." });
    }

    PredictionResult prediction;
    try
    {
        prediction = predictiveModelService.Predict(request);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    var predictionEntity = predictiveModelService.BuildPredictionEntity(request, prediction);
    await repository.AddAsync(predictionEntity, cancellationToken);

    return Results.Ok(prediction);
})
.WithName("CreatePrediction");

app.MapGet("/api/predictions/{id:guid}/pdf", async (
    Guid id,
    IPredictionRepository predictionRepository,
    IProjectRepository projectRepository,
    IFinancialPredictionRepository financialPredictionRepository,
    IEvmRepository evmRepository,
    ProjectPdfReportGenerator pdfGenerator,
    CancellationToken cancellationToken) =>
{
    var prediction = await predictionRepository.GetByIdAsync(id, cancellationToken);
    if (prediction is null)
    {
        return Results.NotFound(new { error = "No se encontro la prediccion solicitada." });
    }

    var project = await projectRepository.GetByIdAsync(prediction.ProjectId, cancellationToken);
    if (project is null)
    {
        return Results.NotFound(new { error = "No se encontro el proyecto asociado a la prediccion." });
    }

    var predictions = await predictionRepository.GetByProjectIdAsync(project.Id, cancellationToken);
    var financialPrediction = await financialPredictionRepository.GetByProjectIdAsync(project.Id, cancellationToken);
    var evmHistory = await evmRepository.GetByProjectIdAsync(project.Id, 24, cancellationToken);

    var pdf = pdfGenerator.GeneratePredictionReport(project, prediction, predictions, financialPrediction, evmHistory);
    var fileName = ProjectReportFileNameBuilder.BuildPdf(project.Location, project.Name);

    return Results.File(pdf, "application/pdf", fileName);
})
.WithName("DownloadPredictionPdf");

app.MapGet("/api/predictions/{id:guid}/excel", async (
    Guid id,
    IPredictionRepository predictionRepository,
    IProjectRepository projectRepository,
    IFinancialPredictionRepository financialPredictionRepository,
    IEvmRepository evmRepository,
    ProjectExcelReportGenerator excelGenerator,
    CancellationToken cancellationToken) =>
{
    var prediction = await predictionRepository.GetByIdAsync(id, cancellationToken);
    if (prediction is null)
    {
        return Results.NotFound(new { error = "No se encontro la prediccion solicitada." });
    }

    var project = await projectRepository.GetByIdAsync(prediction.ProjectId, cancellationToken);
    if (project is null)
    {
        return Results.NotFound(new { error = "No se encontro el proyecto asociado a la prediccion." });
    }

    var predictions = await predictionRepository.GetByProjectIdAsync(project.Id, cancellationToken);
    var financialPrediction = await financialPredictionRepository.GetByProjectIdAsync(project.Id, cancellationToken);
    var evmHistory = await evmRepository.GetByProjectIdAsync(project.Id, 24, cancellationToken);

    var excel = excelGenerator.GeneratePredictionReport(project, prediction, predictions, financialPrediction, evmHistory);
    var fileName = ProjectReportFileNameBuilder.BuildExcel(project.Location, project.Name);

    return Results.File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
})
.WithName("DownloadPredictionExcel");

app.MapGet("/api/predictions/{id:guid}", async (Guid id, IPredictionRepository repository, CancellationToken cancellationToken) =>
{
    var prediction = await repository.GetByIdAsync(id, cancellationToken);
    return prediction is null ? Results.NotFound() : Results.Ok(prediction);
})
.WithName("GetPredictionById");

app.MapGet("/api/predictions", async (
    int? take,
    IPredictionRepository repository,
    IProjectRepository projectRepository,
    CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 20, 1, 100);
    var predictions = await repository.GetRecentAsync(limit, cancellationToken);
    var projects = await projectRepository.GetAllAsync(cancellationToken);
    var projectNames = projects.ToDictionary(x => x.Id, x => x.Name);

    return Results.Ok(predictions.Select(prediction => new PredictionSummaryResponse(
        PredictionId: prediction.Id,
        ProjectId: prediction.ProjectId,
        ProjectName: projectNames.GetValueOrDefault(prediction.ProjectId, "Proyecto"),
        AreaM2: projects.FirstOrDefault(x => x.Id == prediction.ProjectId)?.AreaM2 ?? prediction.AreaM2,
        Type: prediction.Type,
        Location: prediction.Location,
        DurationMonths: (projects.FirstOrDefault(x => x.Id == prediction.ProjectId)?.DurationMonths) ?? (prediction.DurationDays / 30f),
        BaseCostCop: projects.FirstOrDefault(x => x.Id == prediction.ProjectId)?.BaseCostCop ?? 0m,
        PredictedMaterials: prediction.PredictedMaterials,
        PredictedLabor: prediction.PredictedLabor,
        EstimatedMaterialQuantity: prediction.EstimatedMaterialQuantity,
        EstimatedMaterialCostCop: prediction.EstimatedMaterialCostCop,
        RequiredLaborHours: prediction.RequiredLaborHours,
        CreatedAtUtc: prediction.CreatedAtUtc)));
})
.WithName("GetRecentPredictions");

app.MapGet("/api/evm/recent", async (
    int? take,
    IEvmRepository repository,
    IProjectRepository projectRepository,
    CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 20, 1, 100);
    var records = await repository.GetRecentAsync(limit, cancellationToken);
    var projects = await projectRepository.GetAllAsync(cancellationToken);
    var projectNames = projects.ToDictionary(x => x.Id, x => x.Name);

    return Results.Ok(records.Select(record => new EvmSummaryResponse(
        RecordId: record.Id,
        ProjectId: record.ProjectId,
        ProjectName: projectNames.GetValueOrDefault(record.ProjectId, "Proyecto"),
        AreaM2: projects.FirstOrDefault(x => x.Id == record.ProjectId)?.AreaM2 ?? 0f,
        Type: projects.FirstOrDefault(x => x.Id == record.ProjectId)?.Type ?? string.Empty,
        Location: projects.FirstOrDefault(x => x.Id == record.ProjectId)?.Location ?? string.Empty,
        DurationMonths: projects.FirstOrDefault(x => x.Id == record.ProjectId)?.DurationMonths ?? 0f,
        BaseCostCop: projects.FirstOrDefault(x => x.Id == record.ProjectId)?.BaseCostCop ?? 0m,
        PeriodDateUtc: record.PeriodDateUtc,
        PV: record.PV,
        EV: record.EV,
        AC: record.AC,
        CPI: record.CPI,
        SPI: record.SPI,
        CostInterpretation: record.CostInterpretation,
        ScheduleInterpretation: record.ScheduleInterpretation,
        CreatedAtUtc: record.CreatedAtUtc)));
})
.WithName("GetRecentEvm");

app.MapGet("/api/financial-predictions", async (
    int? take,
    IFinancialPredictionRepository repository,
    IProjectRepository projectRepository,
    CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 20, 1, 100);
    var items = await repository.GetRecentAsync(limit, cancellationToken);
    var projects = await projectRepository.GetAllAsync(cancellationToken);
    var projectMap = projects.ToDictionary(x => x.Id, x => x);

    return Results.Ok(items.Select(item =>
    {
        var project = projectMap.GetValueOrDefault(item.ProjectId);
        return new FinancialPredictionSummaryResponse(
            FinancialPredictionId: item.Id,
            ProjectId: item.ProjectId,
            ProjectName: project?.Name ?? "Proyecto",
            AreaM2: project?.AreaM2 ?? 0f,
            Type: project?.Type ?? string.Empty,
            Location: project?.Location ?? string.Empty,
            DurationMonths: project?.DurationMonths ?? 0f,
            BaseCostCop: project?.BaseCostCop ?? 0m,
            EstimatedTotalCostCop: item.EstimatedTotalCostCop,
            MinimumEstimatedCostCop: item.MinimumEstimatedCostCop,
            MaximumEstimatedCostCop: item.MaximumEstimatedCostCop,
            ConfidencePercentage: item.ConfidencePercentage,
            ConfidenceLevel: item.ConfidenceLevel,
            HistoricalAverageCostPerM2Cop: item.HistoricalAverageCostPerM2Cop,
            LocationTrendFactor: item.LocationTrendFactor,
            CreatedAtUtc: item.CreatedAtUtc);
    }));
})
.WithName("GetRecentFinancialPredictions");

app.MapPost("/api/evm/calculate", async (
    CalculateEvmRequest request,
    IEvmService evmService,
    IProjectRepository projectRepository,
    IPredictionRepository predictionRepository,
    IEvmRepository evmRepository,
    CancellationToken cancellationToken) =>
{
    if (request.ProjectId == Guid.Empty)
    {
        return Results.BadRequest(new { error = "ProjectId es obligatorio." });
    }

    try
    {
        var project = await projectRepository.GetByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var hasMaterialsPrediction = await predictionRepository.ExistsForProjectAsync(request.ProjectId, predictedMaterials: true, predictedLabor: false, cancellationToken);
        var hasLaborPrediction = await predictionRepository.ExistsForProjectAsync(request.ProjectId, predictedMaterials: false, predictedLabor: true, cancellationToken);

        if (!hasMaterialsPrediction || !hasLaborPrediction)
        {
            return Results.BadRequest(new { error = "Este proyecto necesita prediccion de materiales y mano de obra antes de calcular EVM." });
        }

        if (await evmRepository.ExistsForProjectAsync(request.ProjectId, cancellationToken))
        {
            return Results.BadRequest(new { error = "Este proyecto ya tiene un calculo EVM registrado." });
        }

        var result = await evmService.CalculateAndStoreAsync(request.ProjectId, request.PeriodDateUtc, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CalculateEvm");

app.MapGet("/api/evm/records/{recordId:guid}/pdf", async (
    Guid recordId,
    IEvmRepository evmRepository,
    IProjectRepository projectRepository,
    IPredictionRepository predictionRepository,
    IFinancialPredictionRepository financialPredictionRepository,
    ProjectPdfReportGenerator pdfGenerator,
    CancellationToken cancellationToken) =>
{
    var record = await evmRepository.GetByIdAsync(recordId, cancellationToken);
    if (record is null)
    {
        return Results.NotFound(new { error = "No se encontro el registro EVM solicitado." });
    }

    var project = await projectRepository.GetByIdAsync(record.ProjectId, cancellationToken);
    if (project is null)
    {
        return Results.NotFound(new { error = "No se encontro el proyecto asociado al registro EVM." });
    }

    var predictions = await predictionRepository.GetByProjectIdAsync(project.Id, cancellationToken);
    var financialPrediction = await financialPredictionRepository.GetByProjectIdAsync(project.Id, cancellationToken);
    var evmHistory = await evmRepository.GetByProjectIdAsync(project.Id, 24, cancellationToken);

    var pdf = pdfGenerator.GenerateEvmReport(project, record, predictions, financialPrediction, evmHistory);
    var fileName = ProjectReportFileNameBuilder.BuildPdf(project.Location, project.Name);

    return Results.File(pdf, "application/pdf", fileName);
})
.WithName("DownloadEvmPdf");

app.MapGet("/api/evm/records/{recordId:guid}/excel", async (
    Guid recordId,
    IEvmRepository evmRepository,
    IProjectRepository projectRepository,
    IPredictionRepository predictionRepository,
    IFinancialPredictionRepository financialPredictionRepository,
    ProjectExcelReportGenerator excelGenerator,
    CancellationToken cancellationToken) =>
{
    var record = await evmRepository.GetByIdAsync(recordId, cancellationToken);
    if (record is null)
    {
        return Results.NotFound(new { error = "No se encontro el registro EVM solicitado." });
    }

    var project = await projectRepository.GetByIdAsync(record.ProjectId, cancellationToken);
    if (project is null)
    {
        return Results.NotFound(new { error = "No se encontro el proyecto asociado al registro EVM." });
    }

    var predictions = await predictionRepository.GetByProjectIdAsync(project.Id, cancellationToken);
    var financialPrediction = await financialPredictionRepository.GetByProjectIdAsync(project.Id, cancellationToken);
    var evmHistory = await evmRepository.GetByProjectIdAsync(project.Id, 24, cancellationToken);

    var excel = excelGenerator.GenerateEvmReport(project, record, predictions, financialPrediction, evmHistory);
    var fileName = ProjectReportFileNameBuilder.BuildExcel(project.Location, project.Name);

    return Results.File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
})
.WithName("DownloadEvmExcel");

app.MapGet("/api/evm/{projectId:guid}/history", async (Guid projectId, int? take, IEvmService evmService, CancellationToken cancellationToken) =>
{
    try
    {
        var history = await evmService.GetHistoryAsync(projectId, take ?? 20, cancellationToken);
        return Results.Ok(history);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetEvmHistory");

app.Run();

static string BuildPredictionHistorySummary(Prediction prediction)
{
    var parts = new List<string>();

    if (prediction.PredictedMaterials)
    {
        parts.Add($"Materiales {prediction.EstimatedMaterialQuantity:N2}");
        parts.Add($"costo estimado {prediction.EstimatedMaterialCostCop:N0} COP");
    }

    if (prediction.PredictedLabor)
    {
        parts.Add($"mano de obra {prediction.RequiredLaborHours:N2} horas-persona");
    }

    return parts.Count > 0 ? string.Join(", ", parts) : "Prediccion registrada";
}

public sealed record TrainModelRequest(int Rows);
public sealed record CalculateEvmRequest(
    Guid ProjectId,
    DateTime? PeriodDateUtc);
