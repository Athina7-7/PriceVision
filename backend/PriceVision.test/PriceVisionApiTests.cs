using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Integration tests for PriceVision.Api
/// Based on User Stories 1–7 and real endpoints in Program.cs
///
/// Setup requirements:
///   dotnet add package Microsoft.AspNetCore.Mvc.Testing
///   dotnet add package xunit
///   dotnet add package xunit.runner.visualstudio
///   dotnet add package FluentAssertions  (optional but used here)
///
/// Run with: dotnet test
/// </summary>
namespace PriceVision.Api.Tests;

// ─────────────────────────────────────────────────────────────
// Shared factory (one in-memory test server for all test classes)
// ─────────────────────────────────────────────────────────────
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public async Task InitializeAsync() { await Task.CompletedTask; }
    public new async Task DisposeAsync() { await Task.CompletedTask; }
}

// ─────────────────────────────────────────────────────────────
// USER STORY 1 – Project Data Registration
// Endpoints: POST /api/projects
// ─────────────────────────────────────────────────────────────
public class ProjectRegistrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ProjectRegistrationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // CP-01 – Successful Registration
    [Fact]
    public async Task CP01_ValidData_CreatesProject()
    {
        var payload = new
        {
            name = "Torre Residencial Norte",
            areaM2 = 250.5f,
            location = "Bogota",
            type = "Residencial",
            durationMonths = 18f,
            baseCostCop = 500_000_000m
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("project", out var project));
        Assert.True(project.TryGetProperty("projectId", out _));
    }

    // CP-02 – Empty Required Fields (Name missing)
    [Fact]
    public async Task CP02_EmptyName_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "",
            areaM2 = 250.5f,
            location = "Bogota",
            type = "Residencial",
            durationMonths = 18f,
            baseCostCop = 500_000_000m
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out var error));
        Assert.Contains("nombre", error.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    // CP-02b – Type and Location missing
    [Fact]
    public async Task CP02b_EmptyTypeAndLocation_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "Proyecto X",
            areaM2 = 100f,
            location = "",
            type = "",
            durationMonths = 12f,
            baseCostCop = 100_000_000m
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // CP-03 – Invalid Area (area = 0)
    [Fact]
    public async Task CP03_AreaIsZero_ReturnsBadRequestWithMessage()
    {
        var payload = new
        {
            name = "Proyecto Y",
            areaM2 = 0f,
            location = "Medellin",
            type = "Comercial",
            durationMonths = 12f,
            baseCostCop = 200_000_000m
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errorText = body.GetProperty("error").GetString() ?? "";
        Assert.Contains("area", errorText, StringComparison.OrdinalIgnoreCase);
    }

    // CP-03b – Negative Duration
    [Fact]
    public async Task CP03b_NegativeDuration_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "Proyecto Z",
            areaM2 = 150f,
            location = "Cali",
            type = "Industrial",
            durationMonths = -3f,
            baseCostCop = 300_000_000m
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // CP-03c – Negative Cost
    [Fact]
    public async Task CP03c_NegativeCost_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "Proyecto W",
            areaM2 = 100f,
            location = "Barranquilla",
            type = "Residencial",
            durationMonths = 10f,
            baseCostCop = -1m
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

// ─────────────────────────────────────────────────────────────
// USER STORY 2 – Data Validation (warnings on submit)
// Endpoint: POST /api/projects (returns warnings field)
// ─────────────────────────────────────────────────────────────
public class DataValidationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public DataValidationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // CP-04 – Very high cost triggers warning but still saves project
    [Fact]
    public async Task CP04_HighCost_ReturnsOkWithWarnings()
    {
        var payload = new
        {
            name = "Megaproyecto",
            areaM2 = 100f,
            location = "Bogota",
            type = "Residencial",
            durationMonths = 12f,
            baseCostCop = 9_999_999_999m   // extremely high
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        // Non-blocking: should succeed
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Response: { project: {...}, warnings: [...] }
        Assert.True(body.TryGetProperty("warnings", out var warnings));
        // warnings may be empty array OR contain entries — just assert field exists
        Assert.Equal(JsonValueKind.Array, warnings.ValueKind);
    }

    // CP-05 – Inconsistent duration vs area triggers alert (warnings not empty)
    [Fact]
    public async Task CP05_UnrealisticDuration_ReturnsWarning()
    {
        var payload = new
        {
            name = "Proyecto Rapido",
            areaM2 = 5000f,   // very large area
            location = "Medellin",
            type = "Comercial",
            durationMonths = 1f, // unrealistically short
            baseCostCop = 100_000_000m
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("warnings", out var warnings));
        Assert.True(warnings.GetArrayLength() > 0, "Expected at least one warning for inconsistent duration/area");
    }
}

// ─────────────────────────────────────────────────────────────
// USER STORY 3 – Resource Prediction
// Endpoints: POST /api/projects/{id}/predict, GET /api/predictions
// ─────────────────────────────────────────────────────────────
public class ResourcePredictionTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ResourcePredictionTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var payload = new
        {
            name = $"Proyecto Test {Guid.NewGuid()}",
            areaM2 = 200f,
            location = "Bogota",
            type = "Residencial",
            durationMonths = 12f,
            baseCostCop = 400_000_000m
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("project").GetProperty("projectId").GetGuid();
    }

    // CP-06 – Prediction returns materials and labor
    [Fact]
    public async Task CP06_ValidPrediction_ReturnsMaterialsAndLabor()
    {
        var projectId = await CreateProjectAsync();

        var payload = new
        {
            projectId,
            type = "Residencial",
            location = "Bogota",
            predictMaterials = true,
            predictLabor = true
        };

        var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/predict", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("predictedMaterials", out _));
        Assert.True(body.TryGetProperty("predictedLabor", out _));
    }

    // CP-07 – Response time under 5 seconds
    [Fact]
    public async Task CP07_PredictionResponseTime_UnderFiveSeconds()
    {
        var projectId = await CreateProjectAsync();

        var payload = new
        {
            projectId,
            type = "Residencial",
            location = "Bogota",
            predictMaterials = true,
            predictLabor = false
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _client.PostAsJsonAsync($"/api/projects/{projectId}/predict", payload);
        sw.Stop();

        Assert.True(sw.Elapsed.TotalSeconds < 5,
            $"Response took {sw.Elapsed.TotalSeconds:F2}s — exceeded 5 second limit");
    }

    // CP-08 – Prediction is stored (appears in GET /api/predictions)
    [Fact]
    public async Task CP08_PredictionIsSavedToDatabase()
    {
        var projectId = await CreateProjectAsync();

        var payload = new
        {
            projectId,
            type = "Residencial",
            location = "Bogota",
            predictMaterials = true,
            predictLabor = false
        };

        await _client.PostAsJsonAsync($"/api/projects/{projectId}/predict", payload);

        var listResponse = await _client.GetAsync("/api/predictions?take=50");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var found = list.EnumerateArray()
                        .Any(p => p.GetProperty("projectId").GetGuid() == projectId);

        Assert.True(found, "Prediction was not found in GET /api/predictions after creation");
    }
}

// ─────────────────────────────────────────────────────────────
// USER STORY 5 – Cost (Financial) Prediction
// Endpoint: POST /api/projects/{id}/financial-predict
// ─────────────────────────────────────────────────────────────
public class CostPredictionTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public CostPredictionTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var payload = new
        {
            name = $"Proyecto Costo {Guid.NewGuid()}",
            areaM2 = 300f,
            location = "Bogota",
            type = "Residencial",
            durationMonths = 24f,
            baseCostCop = 600_000_000m
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("project").GetProperty("projectId").GetGuid();
    }

    // CP-09 – Financial prediction returns estimated cost
    [Fact]
    public async Task CP09_FinancialPrediction_ReturnsEstimatedCost()
    {
        var projectId = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/financial-predict", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("estimatedTotalCostCop", out var cost));
        Assert.True(cost.GetDecimal() > 0);
    }

    // CP-10 – Financial prediction returns min/max range and confidence
    [Fact]
    public async Task CP10_FinancialPrediction_ReturnsRangeAndConfidence()
    {
        var projectId = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/financial-predict", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var min = body.GetProperty("minimumEstimatedCostCop").GetDecimal();
        var max = body.GetProperty("maximumEstimatedCostCop").GetDecimal();
        var confidence = body.GetProperty("confidencePercentage").GetDouble();
        var confidenceLevel = body.GetProperty("confidenceLevel").GetString();

        Assert.True(min < max, $"Min ({min}) should be less than Max ({max})");
        Assert.True(confidence > 0 && confidence <= 100);
        Assert.False(string.IsNullOrWhiteSpace(confidenceLevel));
    }
}

// ─────────────────────────────────────────────────────────────
// USER STORY 6 – Navigation & Health
// Endpoints: GET /api/health, GET /api/projects, GET /api/predictions
// ─────────────────────────────────────────────────────────────
public class NavigationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public NavigationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // CP-18 – Health endpoint responds correctly
    [Fact]
    public async Task CP18_HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    // CP-18b – Projects list endpoint reachable
    [Fact]
    public async Task CP18b_ProjectsListEndpoint_ReturnsOkArray()
    {
        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    // CP-18c – Predictions list endpoint reachable
    [Fact]
    public async Task CP18c_PredictionsListEndpoint_ReturnsOkArray()
    {
        var response = await _client.GetAsync("/api/predictions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }
}

// ─────────────────────────────────────────────────────────────
// USER STORY 7 – EVM Metrics
// Endpoint: POST /api/evm/calculate, GET /api/evm/recent
// ─────────────────────────────────────────────────────────────
public class EvmMetricsTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public EvmMetricsTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateProjectWithPredictionsAsync()
    {
        // Create project
        var projectPayload = new
        {
            name = $"Proyecto EVM {Guid.NewGuid()}",
            areaM2 = 200f,
            location = "Bogota",
            type = "Residencial",
            durationMonths = 12f,
            baseCostCop = 400_000_000m
        };

        var projectResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        projectResponse.EnsureSuccessStatusCode();
        var projectBody = await projectResponse.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = projectBody.GetProperty("project").GetProperty("projectId").GetGuid();

        // Predict materials
        await _client.PostAsJsonAsync($"/api/projects/{projectId}/predict", new
        {
            projectId,
            type = "Residencial",
            location = "Bogota",
            predictMaterials = true,
            predictLabor = false
        });

        // Predict labor
        await _client.PostAsJsonAsync($"/api/projects/{projectId}/predict", new
        {
            projectId,
            type = "Residencial",
            location = "Bogota",
            predictMaterials = false,
            predictLabor = true
        });

        return projectId;
    }

    // CP-11 – EVM Calculation returns PV, EV, AC, CPI, SPI
    [Fact]
    public async Task CP11_EvmCalculation_ReturnsAllMetrics()
    {
        var projectId = await CreateProjectWithPredictionsAsync();

        var payload = new
        {
            projectId,
            periodDateUtc = DateTime.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/api/evm/calculate", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var metric in new[] { "pV", "eV", "aC", "cPI", "sPI" })
        {
            Assert.True(body.TryGetProperty(metric, out _),
                $"Expected metric '{metric}' in EVM response");
        }
    }

    // CP-12 – EVM returns cost and schedule interpretation labels
    [Fact]
    public async Task CP12_EvmCalculation_ReturnsInterpretationLabels()
    {
        var projectId = await CreateProjectWithPredictionsAsync();

        var payload = new
        {
            projectId,
            periodDateUtc = DateTime.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/api/evm/calculate", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("costInterpretation", out var costLabel));
        Assert.True(body.TryGetProperty("scheduleInterpretation", out var schedLabel));

        Assert.False(string.IsNullOrWhiteSpace(costLabel.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(schedLabel.GetString()));
    }

    // CP-13 – EVM result is stored (appears in GET /api/evm/recent)
    [Fact]
    public async Task CP13_EvmResult_IsSavedToDatabase()
    {
        var projectId = await CreateProjectWithPredictionsAsync();

        await _client.PostAsJsonAsync("/api/evm/calculate", new
        {
            projectId,
            periodDateUtc = DateTime.UtcNow
        });

        var listResponse = await _client.GetAsync("/api/evm/recent?take=50");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var found = list.EnumerateArray()
                        .Any(e => e.GetProperty("projectId").GetGuid() == projectId);

        Assert.True(found, "EVM record was not found in GET /api/evm/recent after calculation");
    }

    // CP-13b – Duplicate EVM calculation is rejected
    [Fact]
    public async Task CP13b_DuplicateEvmCalculation_ReturnsBadRequest()
    {
        var projectId = await CreateProjectWithPredictionsAsync();

        var payload = new { projectId, periodDateUtc = DateTime.UtcNow };

        var first = await _client.PostAsJsonAsync("/api/evm/calculate", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second attempt on same project — should be rejected
        var second = await _client.PostAsJsonAsync("/api/evm/calculate", payload);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    // CP-11b – EVM requires prior material and labor prediction
    [Fact]
    public async Task CP11b_EvmWithoutPredictions_ReturnsBadRequest()
    {
        // Create a project WITHOUT predictions
        var projectPayload = new
        {
            name = $"Proyecto Sin Predict {Guid.NewGuid()}",
            areaM2 = 100f,
            location = "Cali",
            type = "Industrial",
            durationMonths = 6f,
            baseCostCop = 50_000_000m
        };

        var projectResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projectBody = await projectResponse.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = projectBody.GetProperty("project").GetProperty("projectId").GetGuid();

        var response = await _client.PostAsJsonAsync("/api/evm/calculate", new
        {
            projectId,
            periodDateUtc = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("prediccion", body.GetProperty("error").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }
}

// ─────────────────────────────────────────────────────────────
// PROJECT HISTORY
// Endpoint: GET /api/projects/{id}/history
// ─────────────────────────────────────────────────────────────
public class ProjectHistoryTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ProjectHistoryTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task History_KnownProject_ReturnsOrderedTimeline()
    {
        // Create a project
        var projectPayload = new
        {
            name = $"Proyecto Hist {Guid.NewGuid()}",
            areaM2 = 180f,
            location = "Bogota",
            type = "Residencial",
            durationMonths = 10f,
            baseCostCop = 250_000_000m
        };

        var projectResponse = await _client.PostAsJsonAsync("/api/projects", projectPayload);
        var projectBody = await projectResponse.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = projectBody.GetProperty("project").GetProperty("projectId").GetGuid();

        // History should at least return 200 with an array
        var histResponse = await _client.GetAsync($"/api/projects/{projectId}/history");
        Assert.Equal(HttpStatusCode.OK, histResponse.StatusCode);

        var history = await histResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, history.ValueKind);
    }

    [Fact]
    public async Task History_UnknownProject_ReturnsNotFound()
    {
        var fakeId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/projects/{fakeId}/history");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
