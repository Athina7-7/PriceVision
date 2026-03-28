using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

/// <summary>
/// Tests de integracion para PriceVision.Api
/// Adaptados a los endpoints reales definidos en Program.cs
///
/// Endpoints reales disponibles:
///   GET  /api/health
///   POST /api/predictions/train
///   POST /api/predictions
///   GET  /api/predictions/{id}
///   GET  /api/predictions
///   POST /api/projects
///   GET  /api/projects
///   POST /api/predict/resources
///   POST /api/predict/cost
///   POST /api/evm
/// </summary>
namespace PriceVision.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public async Task InitializeAsync() { await Task.CompletedTask; }
    public new async Task DisposeAsync() { await Task.CompletedTask; }
}

// ─────────────────────────────────────────────
// USER STORY 1 – Project Data Registration
// Endpoint: POST /api/projects
// ─────────────────────────────────────────────
public class ProjectRegistrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ProjectRegistrationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // CP-01 – Registro exitoso
    [Fact]
    public async Task CP01_ValidData_Returns200WithIdAndMessage()
    {
        var payload = new
        {
            name = "Torre Residencial Norte",
            area = 250.5,
            duration = 18,
            cost = 500000.0,
            type = "Residencial",
            location = "Bogota"
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Project created successfully", body.GetProperty("message").GetString());
        Assert.True(body.TryGetProperty("id", out _));
    }

    // CP-02 – Nombre vacío
    [Fact]
    public async Task CP02_EmptyName_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "",
            area = 250.5,
            duration = 18,
            cost = 500000.0,
            type = "Residencial",
            location = "Bogota"
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Name is required", body.GetProperty("error").GetString());
    }

    // CP-02b – Type y Location vacíos
    [Fact]
    public async Task CP02b_EmptyTypeAndLocation_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "Proyecto X",
            area = 100.0,
            duration = 12,
            cost = 100000.0,
            type = "",
            location = ""
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Type and Location are required", body.GetProperty("error").GetString());
    }

    // CP-03 – Área igual a 0
    [Fact]
    public async Task CP03_AreaIsZero_ReturnsBadRequestWithMessage()
    {
        var payload = new
        {
            name = "Proyecto Y",
            area = 0.0,
            duration = 12,
            cost = 200000.0,
            type = "Comercial",
            location = "Medellin"
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Area must be greater than 0", body.GetProperty("error").GetString());
    }

    // CP-03b – Duración negativa
    [Fact]
    public async Task CP03b_NegativeDuration_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "Proyecto Z",
            area = 150.0,
            duration = -3,
            cost = 300000.0,
            type = "Industrial",
            location = "Cali"
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Duration must be greater than 0", body.GetProperty("error").GetString());
    }

    // CP-03c – Costo negativo
    [Fact]
    public async Task CP03c_NegativeCost_ReturnsBadRequest()
    {
        var payload = new
        {
            name = "Proyecto W",
            area = 100.0,
            duration = 10,
            cost = -1.0,
            type = "Residencial",
            location = "Barranquilla"
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cost cannot be negative", body.GetProperty("error").GetString());
    }
}


// ─────────────────────────────────────────────
// USER STORY 2 – Data Validation (warnings)
// Endpoint: POST /api/projects → campo warnings
// ─────────────────────────────────────────────
public class DataValidationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public DataValidationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // CP-04 – Costo muy alto genera warning pero deja guardar
    [Fact]
    public async Task CP04_HighCost_ReturnsOkWithWarning()
    {
        var payload = new
        {
            name = "Megaproyecto",
            area = 100.0,
            duration = 12,
            cost = 9_999_999_999.0,
            type = "Residencial",
            location = "Bogota"
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("warnings", out var warnings));
        Assert.True(warnings.GetArrayLength() > 0, "Se esperaba al menos un warning por costo alto");
    }

    // CP-05 – Duración inconsistente con área genera warning
    [Fact]
    public async Task CP05_UnrealisticDuration_ReturnsWarning()
    {
        var payload = new
        {
            name = "Proyecto Rapido",
            area = 5000.0,
            duration = 1,
            cost = 100000.0,
            type = "Comercial",
            location = "Medellin"
        };

        var response = await _client.PostAsJsonAsync("/api/projects", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("warnings", out var warnings));
        Assert.True(warnings.GetArrayLength() > 0, "Se esperaba warning por duracion inconsistente con el area");
    }
}


// ─────────────────────────────────────────────
// USER STORY 3 – Resource Prediction
// Endpoint: POST /api/predict/resources
// ─────────────────────────────────────────────
public class ResourcePredictionTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ResourcePredictionTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // CP-06 – Prediccion retorna materiales y labor
    [Fact]
    public async Task CP06_ResourcePrediction_ReturnsMaterialsAndLabor()
    {
        var payload = new
        {
            area = 200.0,
            duration = 12,
            type = "Residencial",
            location = "Bogota"
        };

        var response = await _client.PostAsJsonAsync("/api/predict/resources", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("materials", out var materials));
        Assert.True(body.TryGetProperty("labor", out var labor));
        Assert.True(materials.GetDouble() > 0);
        Assert.True(labor.GetDouble() > 0);
    }

    // CP-07 – Tiempo de respuesta menor a 5 segundos
    [Fact]
    public async Task CP07_ResourcePrediction_RespondsUnderFiveSeconds()
    {
        var payload = new { area = 200.0, duration = 12, type = "Residencial", location = "Bogota" };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _client.PostAsJsonAsync("/api/predict/resources", payload);
        sw.Stop();

        Assert.True(sw.Elapsed.TotalSeconds < 5,
            $"La respuesta tardo {sw.Elapsed.TotalSeconds:F2}s — supera el limite de 5 segundos");
    }

    // CP-08 – El endpoint responde correctamente (simula almacenamiento)
    [Fact]
    public async Task CP08_ResourcePrediction_ReturnsSuccessResponse()
    {
        var payload = new { area = 200.0, duration = 12, type = "Residencial", location = "Bogota" };

        var response = await _client.PostAsJsonAsync("/api/predict/resources", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("materials", out _));
        Assert.True(body.TryGetProperty("labor", out _));
    }
}


// ─────────────────────────────────────────────
// USER STORY 5 – Cost Prediction
// Endpoint: POST /api/predict/cost
// ─────────────────────────────────────────────
public class CostPredictionTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public CostPredictionTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // CP-09 – Retorna costo estimado
    [Fact]
    public async Task CP09_CostPrediction_ReturnsEstimatedCost()
    {
        var payload = new { area = 300.0, duration = 24, type = "Residencial", location = "Bogota" };

        var response = await _client.PostAsJsonAsync("/api/predict/cost", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("estimatedCost", out var cost));
        Assert.True(cost.GetDouble() > 0);
    }

    // CP-10 – Retorna rango min/max y nivel de confianza
    [Fact]
    public async Task CP10_CostPrediction_ReturnsRangeAndConfidence()
    {
        var payload = new { area = 300.0, duration = 24, type = "Residencial", location = "Bogota" };

        var response = await _client.PostAsJsonAsync("/api/predict/cost", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var range = body.GetProperty("range");
        var min = range.GetProperty("min").GetDouble();
        var max = range.GetProperty("max").GetDouble();
        var confidence = body.GetProperty("confidence").GetDouble();

        Assert.True(min < max, $"Min ({min}) debe ser menor que Max ({max})");
        Assert.True(confidence > 0 && confidence <= 1,
            $"Confidence ({confidence}) debe estar entre 0 y 1");
    }
}


// ─────────────────────────────────────────────
// USER STORY 6 – Navigation / Health
// Endpoints: GET /api/health, GET /api/projects, GET /api/predictions
// ─────────────────────────────────────────────
public class NavigationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public NavigationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // CP-18 – Health check responde OK
    [Fact]
    public async Task CP18_HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    // CP-18b – Lista de proyectos responde
    [Fact]
    public async Task CP18b_ProjectsListEndpoint_ReturnsOkArray()
    {
        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    // CP-18c – Lista de predicciones responde
    [Fact]
    public async Task CP18c_PredictionsListEndpoint_ReturnsOkArray()
    {
        var response = await _client.GetAsync("/api/predictions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }
}


// ─────────────────────────────────────────────
// USER STORY 7 – EVM Metrics
// Endpoint: POST /api/evm
// ─────────────────────────────────────────────
public class EvmMetricsTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public EvmMetricsTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // CP-11 – Retorna PV, EV, AC, CPI, SPI
    [Fact]
    public async Task CP11_EvmCalculation_ReturnsAllMetrics()
    {
        var response = await _client.PostAsJsonAsync("/api/evm", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var metric in new[] { "PV", "EV", "AC", "CPI", "SPI" })
        {
            Assert.True(body.TryGetProperty(metric, out _),
                $"Falta la metrica '{metric}' en la respuesta EVM");
        }
    }

    // CP-12 – Retorna interpretación de desempeño
    [Fact]
    public async Task CP12_EvmCalculation_ReturnsStatusInterpretation()
    {
        var response = await _client.PostAsJsonAsync("/api/evm", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("status", out var status));

        var statusText = status.GetString() ?? "";
        Assert.False(string.IsNullOrWhiteSpace(statusText),
            "El campo 'status' no debe estar vacio");

        var validStatuses = new[] { "Under budget", "Over budget", "On budget" };
        Assert.Contains(statusText, validStatuses);
    }

    // CP-13 – Valores CPI y SPI son numericos válidos
    [Fact]
    public async Task CP13_EvmCalculation_CpiAndSpiAreValid()
    {
        var response = await _client.PostAsJsonAsync("/api/evm", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var cpi = body.GetProperty("CPI").GetDouble();
        var spi = body.GetProperty("SPI").GetDouble();

        Assert.True(cpi > 0, $"CPI ({cpi}) debe ser mayor que 0");
        Assert.True(spi > 0, $"SPI ({spi}) debe ser mayor que 0");
    }
}


// ─────────────────────────────────────────────
// PREDICTIONS REALES
// Endpoints: POST /api/predictions, GET /api/predictions/{id}
// ─────────────────────────────────────────────
public class PredictionTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public PredictionTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Prediccion con area invalida
    [Fact]
    public async Task Prediction_InvalidArea_ReturnsBadRequest()
    {
        var payload = new
        {
            areaM2 = 0.0,
            type = "Residencial",
            location = "Bogota"
        };

        var response = await _client.PostAsJsonAsync("/api/predictions", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AreaM2 debe ser mayor que cero.", body.GetProperty("error").GetString());
    }

    // Prediccion sin type y location
    [Fact]
    public async Task Prediction_MissingTypeAndLocation_ReturnsBadRequest()
    {
        var payload = new
        {
            areaM2 = 200.0,
            type = "",
            location = ""
        };

        var response = await _client.PostAsJsonAsync("/api/predictions", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Type y Location son obligatorios.", body.GetProperty("error").GetString());
    }

    // GET prediccion inexistente retorna 404
    [Fact]
    public async Task GetPrediction_UnknownId_ReturnsNotFound()
    {
        var fakeId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/predictions/{fakeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}