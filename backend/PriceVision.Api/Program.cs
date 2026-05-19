using PriceVision.Api.Reports;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;
using PriceVision.Domain.Entities;
using PriceVision.Infrastructure;
using PriceVision.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ProjectPdfReportGenerator>();
builder.Services.AddSingleton<ProjectExcelReportGenerator>();

// JWT Auth Configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PriceVisionApi",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PriceVisionClient",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "V3ryS3cr3tK3yForJWTAuth1234567890!"))
        };
    });

builder.Services.AddAuthorization(options => {
    options.AddPolicy("RequireAdminOrPM", policy => policy.RequireRole("Admin", "ProjectManager"));
    options.AddPolicy("RequireAdminOrFA", policy => policy.RequireRole("Admin", "FinancialAnalyst"));
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
});
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var corsPolicyName = "AllowFrontend";
var defaultOrigins = "http://localhost:4200,https://localhost:4200";
var allowedOrigins = builder.Configuration["AllowedOrigins"] ?? defaultOrigins;
var originList = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;

                // Always allow explicit origins from config (comma-separated).
                if (originList.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase)))
                    return true;

                // Allow any Vercel preview/prod domain (e.g. *.vercel.app).
                if (string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) &&
                    uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            })
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
    
    // Optimizacion de indices en la base de datos para operaciones frecuentes.
    var db = dbContext.Database;
    await db.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Projects_CreatedAtUtc ON Projects(CreatedAtUtc DESC);");
    await db.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Predictions_ProjectId ON Predictions(ProjectId);");
    await db.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Predictions_CreatedAtUtc ON Predictions(CreatedAtUtc DESC);");
    await db.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FinancialPredictions_ProjectId ON FinancialPredictions(ProjectId);");
    await db.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FinancialPredictions_CreatedAtUtc ON FinancialPredictions(CreatedAtUtc DESC);");
    await db.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_EVM_Records_ProjectId ON EVM_Records(ProjectId);");
    await db.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_EVM_Records_PeriodDateUtc ON EVM_Records(PeriodDateUtc DESC);");
    await db.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_EVM_Records_CreatedAtUtc ON EVM_Records(CreatedAtUtc DESC);");

    // Seeding inicial de base de datos de usuarios
    await db.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS Users (Id TEXT PRIMARY KEY, Username TEXT, Email TEXT, PasswordHash TEXT, Role TEXT, CreatedAtUtc TEXT);");
    var conn = db.GetDbConnection();
    await conn.OpenAsync();
    using var cmdCheck = conn.CreateCommand();
    cmdCheck.CommandText = "SELECT COUNT(*) FROM Users";
    var userCount = (long)await cmdCheck.ExecuteScalarAsync();
    if (userCount == 0)
    {
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var pAdmin = hasher.HashPassword(null!, "Admin123!");
        var pPm = hasher.HashPassword(null!, "Pm123!");
        var pFa = hasher.HashPassword(null!, "Fa123!");
        var now = DateTime.UtcNow.ToString("O");

        using var cmdInsert = conn.CreateCommand();
        cmdInsert.CommandText = $@"
            INSERT INTO Users (Id, Username, Email, PasswordHash, Role, CreatedAtUtc) VALUES 
            ('{Guid.NewGuid()}', 'admin', 'admin@pricevision.com', '{pAdmin}', 'Admin', '{now}'),
            ('{Guid.NewGuid()}', 'pm', 'pm@pricevision.com', '{pPm}', 'ProjectManager', '{now}'),
            ('{Guid.NewGuid()}', 'fa', 'fa@pricevision.com', '{pFa}', 'FinancialAnalyst', '{now}')";
        await cmdInsert.ExecuteNonQueryAsync();
    }
    await conn.CloseAsync();
}

app.UseCors(corsPolicyName);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
   .WithName("HealthCheck");

app.MapPost("/api/auth/login", async (LoginRequest request, PriceVisionDbContext dbContext, IPasswordHasher<User> hasher, IConfiguration config) => {
    var conn = dbContext.Database.GetDbConnection();
    await conn.OpenAsync();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT Id, Username, Email, PasswordHash, Role FROM Users WHERE Username = @u";
    var param = cmd.CreateParameter(); param.ParameterName = "@u"; param.Value = request.Username;
    cmd.Parameters.Add(param);
    using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) { await conn.CloseAsync(); return Results.Unauthorized(); }
    var user = new User { Id = reader.GetGuid(0), Username = reader.GetString(1), Email = reader.GetString(2), PasswordHash = reader.GetString(3), Role = reader.GetString(4) };
    await conn.CloseAsync();

    var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
    if (result == PasswordVerificationResult.Failed) return Results.Unauthorized();

    var key = Encoding.UTF8.GetBytes(config["Jwt:Secret"] ?? "V3ryS3cr3tK3yForJWTAuth1234567890!");
    var tokenDescriptor = new SecurityTokenDescriptor {
        Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Username), new Claim(ClaimTypes.Role, user.Role) }),
        Expires = DateTime.UtcNow.AddHours(8),
        Issuer = config["Jwt:Issuer"] ?? "PriceVisionApi",
        Audience = config["Jwt:Audience"] ?? "PriceVisionClient",
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    var tokenHandler = new JwtSecurityTokenHandler();
    return Results.Ok(new LoginResponse(tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor)), user.Username, user.Role));
}).WithName("Login").AllowAnonymous();

app.MapGet("/api/projects", async (
    int? take,
    PriceVisionDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 20, 1, 100);
    var projects = await dbContext.Set<Project>()
        .AsNoTracking()
        .OrderByDescending(p => p.CreatedAtUtc)
        .Take(limit)
        .ToListAsync(cancellationToken);

    var projectIds = projects.Select(p => p.Id).ToList();
    
    var predictions = await dbContext.Set<Prediction>()
        .AsNoTracking()
        .Where(p => projectIds.Contains(p.ProjectId))
        .Select(p => new { p.ProjectId, p.PredictedMaterials, p.PredictedLabor })
        .ToListAsync(cancellationToken);
        
    var financialPredictions = await dbContext.Set<FinancialPrediction>()
        .AsNoTracking()
        .Where(fp => projectIds.Contains(fp.ProjectId))
        .Select(fp => fp.ProjectId)
        .Distinct()
        .ToListAsync(cancellationToken);
        
    var evmRecords = await dbContext.Set<EvmRecord>()
        .AsNoTracking()
        .Where(e => projectIds.Contains(e.ProjectId))
        .Select(e => e.ProjectId)
        .Distinct()
        .ToListAsync(cancellationToken);

    var response = projects.Select(project => {
        var projPredictions = predictions.Where(p => p.ProjectId == project.Id).ToList();
        var hasPrediction = projPredictions.Count > 0;
        var hasMaterialsPrediction = projPredictions.Any(p => p.PredictedMaterials && !p.PredictedLabor);
        var hasLaborPrediction = projPredictions.Any(p => !p.PredictedMaterials && p.PredictedLabor);
        var hasFinancialPrediction = financialPredictions.Contains(project.Id);
        var hasEvm = evmRecords.Contains(project.Id);
        
        return new ProjectSummaryResponse(
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
            HasEvm: hasEvm);
    }).ToList();
    
    return Results.Ok(response);
})
.WithName("GetRecentProjects")
.RequireAuthorization();

app.MapGet("/api/projects/{projectId:guid}/similar", async (
    Guid projectId,
    PriceVisionDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var target = await dbContext.Set<Project>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
    if (target is null)
    {
        return Results.NotFound(new { error = "Proyecto no encontrado." });
    }

    var candidates = await dbContext.Set<Project>().AsNoTracking().Where(p => p.Id != projectId).ToListAsync(cancellationToken);
    var similarProjects = new List<SimilarProjectResponse>();

    foreach (var candidate in candidates)
    {
        decimal typeScore = target.Type.Equals(candidate.Type, StringComparison.OrdinalIgnoreCase) ? 1m : 0m;
        decimal locationScore = target.Location.Equals(candidate.Location, StringComparison.OrdinalIgnoreCase) ? 1m : 0m;

        float areaMax = Math.Max(target.AreaM2, Math.Max(candidate.AreaM2, 1f));
        decimal areaScore = 1m - (decimal)(Math.Abs(target.AreaM2 - candidate.AreaM2) / areaMax);

        float durationMax = Math.Max(target.DurationMonths, Math.Max(candidate.DurationMonths, 1f));
        decimal durationScore = 1m - (decimal)(Math.Abs(target.DurationMonths - candidate.DurationMonths) / durationMax);

        decimal costMax = Math.Max(target.BaseCostCop, Math.Max(candidate.BaseCostCop, 1m));
        decimal costScore = 1m - (Math.Abs(target.BaseCostCop - candidate.BaseCostCop) / costMax);

        // Pesos de similitud (30% costo, 20% duracion, 20% area, 15% tipo, 15% ubicacion)
        decimal similarity = (costScore * 0.30m) + (durationScore * 0.20m) + (areaScore * 0.20m) + (typeScore * 0.15m) + (locationScore * 0.15m);

        decimal costDiffPercent = target.BaseCostCop > 0m 
            ? ((candidate.BaseCostCop - target.BaseCostCop) / target.BaseCostCop) * 100m 
            : 0m;

        decimal durationDiffPercent = target.DurationMonths > 0f 
            ? ((decimal)(candidate.DurationMonths - target.DurationMonths) / (decimal)target.DurationMonths) * 100m 
            : 0m;

        similarProjects.Add(new SimilarProjectResponse(
            ProjectId: candidate.Id,
            ProjectName: candidate.Name,
            Type: candidate.Type,
            Location: candidate.Location,
            AreaM2: candidate.AreaM2,
            DurationMonths: candidate.DurationMonths,
            BaseCostCop: candidate.BaseCostCop,
            SimilarityPercentage: similarity * 100m,
            CostDifferencePercentage: costDiffPercent,
            DurationDifferencePercentage: durationDiffPercent,
            CreatedAtUtc: candidate.CreatedAtUtc));
    }

    return Results.Ok(similarProjects.OrderByDescending(p => p.SimilarityPercentage).Take(5).ToList());
})
.WithName("GetSimilarProjects")
.RequireAuthorization();

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
            Summary: $"Costo total {financialPrediction.EstimatedTotalCostCop:N0} COP, intervalo {financialPrediction.ConfidenceIntervalLower:N0} - {financialPrediction.ConfidenceIntervalUpper:N0} COP, confianza {financialPrediction.ConfidencePercentage:N0}%"));
    }

    history.AddRange(evmRecords.Select(record => new ProjectActionHistoryItem(
        ActionType: "evm",
        OccurredAtUtc: record.CreatedAtUtc,
        Title: "Calculo EVM guardado",
        Summary: $"PV {record.PV:N0}, EV {record.EV:N0}, AC {record.AC:N0}, CPI {record.CPI:N2}, SPI {record.SPI:N2}")));

    return Results.Ok(history.OrderByDescending(x => x.OccurredAtUtc));
})
.WithName("GetProjectActionHistory")
.RequireAuthorization();

app.MapPost("/api/projects", async (
    CreateProjectRequest request,
    PriceVisionDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    try
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

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            AreaM2 = request.AreaM2,
            Location = request.Location.Trim(),
            Type = request.Type.Trim(),
            DurationMonths = request.DurationMonths,
            BaseCostCop = request.BaseCostCop,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Set<Project>().Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        var warnings = new List<ProjectValidationWarningResponse>();

        return Results.Ok(new CreateProjectResponse(projectSummary, warnings));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateProject")
.RequireAuthorization("RequireAdminOrPM");

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
.WithName("CreateFinancialPredictionForProject")
.RequireAuthorization("RequireAdminOrFA");

app.MapPost("/api/projects/{projectId:guid}/simulate", async (
    Guid projectId,
    SimulationRequest request,
    IFinancialSimulationService simulationService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await simulationService.SimulateAsync(projectId, request, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("SimulateProjectScenario")
.RequireAuthorization("RequireAdminOrFA");

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
        await Task.Run(() => trainingService.Train(3000));
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
        ModelType: predictionEntity.ModelType,
        ModelVersion: predictionEntity.ModelVersion,
        MaterialesEstimados: request.PredictMaterials ? prediction.MaterialesEstimados : null,
        ManoObraRequeridaHorasPersona: request.PredictLabor ? prediction.ManoObraRequeridaHorasPersona : null);

    return Results.Ok(response);
})
.WithName("CreatePredictionForProject")
.RequireAuthorization("RequireAdminOrPM");

app.MapPost("/api/predictions/train", async (TrainModelRequest? request, IModelTrainingService trainingService) =>
{
    var rows = request?.Rows ?? 3000;
    var result = await Task.Run(() => trainingService.Train(rows));

    return Results.Ok(result);
})
.WithName("TrainPredictionModel")
.RequireAuthorization("RequireAdmin");

app.MapGet("/api/predictions/variable-importance", (IVariableImportanceService variableImportanceService) =>
{
    return Results.Ok(variableImportanceService.GetCostVariableImportance());
})
.WithName("GetPredictionVariableImportance")
.RequireAuthorization();

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

    return Results.Ok(new
    {
        prediction.MaterialesEstimados,
        prediction.ManoObraRequeridaHorasPersona,
        predictionEntity.ModelType,
        predictionEntity.ModelVersion
    });
})
.WithName("CreatePrediction")
.RequireAuthorization("RequireAdminOrPM");

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

    var pdf = await Task.Run(() => pdfGenerator.GeneratePredictionReport(project, prediction, predictions, financialPrediction, evmHistory));
    var fileName = ProjectReportFileNameBuilder.BuildPdf(project.Location, project.Name);

    return Results.File(pdf, "application/pdf", fileName);
})
.WithName("DownloadPredictionPdf")
.RequireAuthorization();

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

    var excel = await Task.Run(() => excelGenerator.GeneratePredictionReport(project, prediction, predictions, financialPrediction, evmHistory));
    var fileName = ProjectReportFileNameBuilder.BuildExcel(project.Location, project.Name);

    return Results.File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
})
.WithName("DownloadPredictionExcel")
.RequireAuthorization();

app.MapGet("/api/predictions/{id:guid}", async (Guid id, IPredictionRepository repository, CancellationToken cancellationToken) =>
{
    var prediction = await repository.GetByIdAsync(id, cancellationToken);
    return prediction is null ? Results.NotFound() : Results.Ok(prediction);
})
.WithName("GetPredictionById")
.RequireAuthorization();

app.MapGet("/api/predictions", async (
    int? take,
    PriceVisionDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 20, 1, 100);
    var predictions = await dbContext.Set<Prediction>()
        .AsNoTracking()
        .OrderByDescending(p => p.CreatedAtUtc)
        .Take(limit)
        .ToListAsync(cancellationToken);
        
    var projectIds = predictions.Select(p => p.ProjectId).Distinct().ToList();
    var projects = await dbContext.Set<Project>()
        .AsNoTracking()
        .Where(p => projectIds.Contains(p.Id))
        .ToDictionaryAsync(p => p.Id, cancellationToken);

    return Results.Ok(predictions.Select(prediction => new PredictionSummaryResponse(
        PredictionId: prediction.Id,
        ProjectId: prediction.ProjectId,
        ProjectName: projects.GetValueOrDefault(prediction.ProjectId)?.Name ?? "Proyecto",
        AreaM2: projects.GetValueOrDefault(prediction.ProjectId)?.AreaM2 ?? prediction.AreaM2,
        Type: prediction.Type,
        Location: prediction.Location,
        DurationMonths: projects.GetValueOrDefault(prediction.ProjectId)?.DurationMonths ?? (prediction.DurationDays / 30f),
        BaseCostCop: projects.GetValueOrDefault(prediction.ProjectId)?.BaseCostCop ?? 0m,
        PredictedMaterials: prediction.PredictedMaterials,
        PredictedLabor: prediction.PredictedLabor,
        EstimatedMaterialQuantity: prediction.EstimatedMaterialQuantity,
        EstimatedMaterialCostCop: prediction.EstimatedMaterialCostCop,
        RequiredLaborHours: prediction.RequiredLaborHours,
        ModelType: prediction.ModelType,
        ModelVersion: prediction.ModelVersion,
        CreatedAtUtc: prediction.CreatedAtUtc)));
})
.WithName("GetRecentPredictions")
.RequireAuthorization();

app.MapGet("/api/evm/recent", async (
    int? take,
    PriceVisionDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 20, 1, 100);
    var records = await dbContext.Set<EvmRecord>()
        .AsNoTracking()
        .OrderByDescending(r => r.PeriodDateUtc)
        .Take(limit)
        .ToListAsync(cancellationToken);
        
    var projectIds = records.Select(r => r.ProjectId).Distinct().ToList();
    var projects = await dbContext.Set<Project>()
        .AsNoTracking()
        .Where(p => projectIds.Contains(p.Id))
        .ToDictionaryAsync(p => p.Id, cancellationToken);

    return Results.Ok(records.Select(record => {
        projects.TryGetValue(record.ProjectId, out var project);
        return new EvmSummaryResponse(
            RecordId: record.Id,
            ProjectId: record.ProjectId,
            ProjectName: project?.Name ?? "Proyecto",
            AreaM2: project?.AreaM2 ?? 0f,
            Type: project?.Type ?? string.Empty,
            Location: project?.Location ?? string.Empty,
            DurationMonths: project?.DurationMonths ?? 0f,
            BaseCostCop: project?.BaseCostCop ?? 0m,
            PeriodDateUtc: record.PeriodDateUtc,
            PV: record.PV,
        EV: record.EV,
        AC: record.AC,
        CPI: record.CPI,
        SPI: record.SPI,
        CostInterpretation: record.CostInterpretation,
        ScheduleInterpretation: record.ScheduleInterpretation,
            CreatedAtUtc: record.CreatedAtUtc);
    }));
})
.WithName("GetRecentEvm")
.RequireAuthorization();

app.MapGet("/api/financial-predictions", async (
    int? take,
    PriceVisionDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 20, 1, 100);
    var items = await dbContext.Set<FinancialPrediction>()
        .AsNoTracking()
        .OrderByDescending(f => f.CreatedAtUtc)
        .Take(limit)
        .ToListAsync(cancellationToken);
        
    var projectIds = items.Select(i => i.ProjectId).Distinct().ToList();
    var projectMap = await dbContext.Set<Project>()
        .AsNoTracking()
        .Where(p => projectIds.Contains(p.Id))
        .ToDictionaryAsync(p => p.Id, cancellationToken);

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
            StandardError: item.StandardError,
            ConfidenceIntervalLower: item.ConfidenceIntervalLower,
            ConfidenceIntervalUpper: item.ConfidenceIntervalUpper,
            ConfidenceExplanation: item.ConfidenceExplanation,
            HistoricalAverageCostPerM2Cop: item.HistoricalAverageCostPerM2Cop,
            LocationTrendFactor: item.LocationTrendFactor,
            ModelType: item.ModelType,
            ModelVersion: item.ModelVersion,
            CreatedAtUtc: item.CreatedAtUtc);
    }));
})
.WithName("GetRecentFinancialPredictions")
.RequireAuthorization();

app.MapGet("/api/financial-predictions/history", async (
    DateTime? startDate,
    DateTime? endDate,
    Guid? projectId,
    PriceVisionDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        return Results.BadRequest(new { error = "La fecha inicial no puede ser mayor a la fecha final." });

    var query = dbContext.Set<FinancialPrediction>().AsNoTracking();

    if (projectId.HasValue && projectId != Guid.Empty)
        query = query.Where(x => x.ProjectId == projectId.Value);

    if (startDate.HasValue)
        query = query.Where(x => x.CreatedAtUtc >= startDate.Value.ToUniversalTime());

    if (endDate.HasValue)
        query = query.Where(x => x.CreatedAtUtc <= endDate.Value.ToUniversalTime());

    var items = await query.OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(cancellationToken);

    var projectIds = items.Select(i => i.ProjectId).Distinct().ToList();
    var projectMap = await dbContext.Set<Project>().AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

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
            StandardError: item.StandardError,
            ConfidenceIntervalLower: item.ConfidenceIntervalLower,
            ConfidenceIntervalUpper: item.ConfidenceIntervalUpper,
            ConfidenceExplanation: item.ConfidenceExplanation,
            HistoricalAverageCostPerM2Cop: item.HistoricalAverageCostPerM2Cop,
            LocationTrendFactor: item.LocationTrendFactor,
            ModelType: item.ModelType,
            ModelVersion: item.ModelVersion,
            CreatedAtUtc: item.CreatedAtUtc);
    }));
})
.WithName("GetFinancialPredictionHistory")
.RequireAuthorization();

app.MapGet("/api/projects/{projectId:guid}/executive-dashboard", async (
    Guid projectId,
    IProjectRepository projectRepository,
    IFinancialPredictionRepository financialPredictionRepository,
    IEvmRepository evmRepository,
    CancellationToken cancellationToken) =>
{
    var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
    if (project is null)
    {
        return Results.NotFound(new { error = "No se encontro el proyecto." });
    }

    var financialPrediction = await financialPredictionRepository.GetByProjectIdAsync(projectId, cancellationToken);
    var evmRecords = await evmRepository.GetByProjectIdAsync(projectId, 1, cancellationToken);
    var latestEvm = evmRecords.OrderByDescending(r => r.PeriodDateUtc).FirstOrDefault();

    decimal estimatedTotalCost = financialPrediction?.EstimatedTotalCostCop ?? project.BaseCostCop;
    decimal projectedDeviation = estimatedTotalCost - project.BaseCostCop;
    decimal projectedDeviationPercentage = project.BaseCostCop > 0 ? (projectedDeviation / project.BaseCostCop) * 100m : 0m;

    string riskLevel = "Medio";
    if (projectedDeviationPercentage <= 5m && (latestEvm?.CPI ?? 1m) >= 0.95m)
        riskLevel = "Bajo";
    else if (projectedDeviationPercentage > 15m || (latestEvm?.CPI ?? 1m) < 0.85m)
        riskLevel = "Alto";

    string riskDescription = riskLevel switch
    {
        "Bajo" => "Riesgo bajo: la desviación proyectada está controlada.",
        "Medio" => "Riesgo medio: requiere seguimiento.",
        _ => "Riesgo alto: requiere acción correctiva."
    };

    var response = new ExecutiveDashboardResponse(
        ProjectId: project.Id,
        ProjectName: project.Name,
        EstimatedTotalCostCop: estimatedTotalCost,
        RiskLevel: riskLevel,
        RiskDescription: riskDescription,
        CPI: latestEvm?.CPI,
        SPI: latestEvm?.SPI,
        ProjectedDeviationCop: projectedDeviation,
        ProjectedDeviationPercentage: projectedDeviationPercentage,
        LastUpdatedUtc: DateTime.UtcNow
    );

    return Results.Ok(response);
})
.WithName("GetExecutiveDashboard")
.RequireAuthorization();

app.MapGet("/api/projects/{projectId:guid}/executive-dashboard/pdf", async (
    Guid projectId,
    IProjectRepository projectRepository,
    IFinancialPredictionRepository financialPredictionRepository,
    IEvmRepository evmRepository,
    ProjectPdfReportGenerator pdfGenerator,
    CancellationToken cancellationToken) =>
{
    var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
    if (project is null)
    {
        return Results.NotFound(new { error = "No se encontro el proyecto." });
    }

    var financialPrediction = await financialPredictionRepository.GetByProjectIdAsync(projectId, cancellationToken);
    var evmRecords = await evmRepository.GetByProjectIdAsync(projectId, 1, cancellationToken);
    var latestEvm = evmRecords.OrderByDescending(r => r.PeriodDateUtc).FirstOrDefault();

    decimal estimatedTotalCost = financialPrediction?.EstimatedTotalCostCop ?? project.BaseCostCop;
    decimal projectedDeviation = estimatedTotalCost - project.BaseCostCop;
    decimal projectedDeviationPercentage = project.BaseCostCop > 0 ? (projectedDeviation / project.BaseCostCop) * 100m : 0m;

    string riskLevel = "Medio";
    if (projectedDeviationPercentage <= 5m && (latestEvm?.CPI ?? 1m) >= 0.95m)
        riskLevel = "Bajo";
    else if (projectedDeviationPercentage > 15m || (latestEvm?.CPI ?? 1m) < 0.85m)
        riskLevel = "Alto";

    string riskDescription = riskLevel switch
    {
        "Bajo" => "Riesgo bajo: la desviación proyectada está controlada.",
        "Medio" => "Riesgo medio: requiere seguimiento.",
        _ => "Riesgo alto: requiere acción correctiva."
    };

    var response = new ExecutiveDashboardResponse(
        ProjectId: project.Id,
        ProjectName: project.Name,
        EstimatedTotalCostCop: estimatedTotalCost,
        RiskLevel: riskLevel,
        RiskDescription: riskDescription,
        CPI: latestEvm?.CPI,
        SPI: latestEvm?.SPI,
        ProjectedDeviationCop: projectedDeviation,
        ProjectedDeviationPercentage: projectedDeviationPercentage,
        LastUpdatedUtc: DateTime.UtcNow
    );

    var pdf = await Task.Run(() => pdfGenerator.GenerateExecutiveDashboardReport(project, response));
    var fileName = ProjectReportFileNameBuilder.BuildPdf(project.Location, project.Name + " - Dashboard");

    return Results.File(pdf, "application/pdf", fileName);
})
.WithName("DownloadExecutiveDashboardPdf")
.RequireAuthorization();

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
.WithName("CalculateEvm")
.RequireAuthorization("RequireAdminOrPM");

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

    var pdf = await Task.Run(() => pdfGenerator.GenerateEvmReport(project, record, predictions, financialPrediction, evmHistory));
    var fileName = ProjectReportFileNameBuilder.BuildPdf(project.Location, project.Name);

    return Results.File(pdf, "application/pdf", fileName);
})
.WithName("DownloadEvmPdf")
.RequireAuthorization();

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

    var excel = await Task.Run(() => excelGenerator.GenerateEvmReport(project, record, predictions, financialPrediction, evmHistory));
    var fileName = ProjectReportFileNameBuilder.BuildExcel(project.Location, project.Name);

    return Results.File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
})
.WithName("DownloadEvmExcel")
.RequireAuthorization();

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
