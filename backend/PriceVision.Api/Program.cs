using PriceVision.Application.Abstractions;
using PriceVision.Application.Contracts;
using PriceVision.Infrastructure;
using PriceVision.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

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
    dbContext.Database.EnsureCreated();
}

app.UseCors(corsPolicyName);
app.UseHttpsRedirection();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
   .WithName("HealthCheck");

app.MapPost("/api/predictions/train", (TrainModelRequest? request, IModelTrainingService trainingService) =>
{
    var rows = request?.Rows ?? 3000;
    var result = trainingService.Train(rows);

    return Results.Ok(result);
})
.WithName("TrainPredictionModel");

app.MapPost("/api/predictions", async (PredictionRequest request, IPredictiveModelService predictiveModelService, IPredictionRepository repository, CancellationToken cancellationToken) =>
{
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

app.MapGet("/api/predictions/{id:guid}", async (Guid id, IPredictionRepository repository, CancellationToken cancellationToken) =>
{
    var prediction = await repository.GetByIdAsync(id, cancellationToken);
    return prediction is null ? Results.NotFound() : Results.Ok(prediction);
})
.WithName("GetPredictionById");

app.MapGet("/api/predictions", async (int? take, IPredictionRepository repository, CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 20, 1, 100);
    var predictions = await repository.GetRecentAsync(limit, cancellationToken);
    return Results.Ok(predictions);
})
.WithName("GetRecentPredictions");

app.Run();

public sealed record TrainModelRequest(int Rows);
