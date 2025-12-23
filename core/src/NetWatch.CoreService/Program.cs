using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using NetWatch.CoreService.Models;
using NetWatch.CoreService.Services.Agent;
using NetWatch.CoreService.Services;
using NetWatch.CoreService.Services.Connectors;
using NetWatch.CoreService.Services.Classification;
using NetWatch.CoreService.Services.Discovery;
using NetWatch.CoreService.Services.Logging;
using NetWatch.CoreService.Services.Analytics;
using NetWatch.CoreService.Services.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Host.UseWindowsService();

var dataRoot = builder.Configuration.GetValue<string>("Data:Path");
if (string.IsNullOrWhiteSpace(dataRoot))
{
    dataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "NetWatch");
}
Directory.CreateDirectory(dataRoot);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var logPath = builder.Configuration.GetValue<string>("Logging:File:Path") ??
              Path.Combine(dataRoot, "logs", "netwatch.log");
Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
var minLevel = Enum.TryParse(builder.Configuration.GetValue<string>("Logging:File:MinLevel"), out LogLevel parsed)
    ? parsed
    : LogLevel.Information;
var maxBytes = builder.Configuration.GetValue<long?>("Logging:File:MaxBytes") ?? 5_000_000;
var maxFiles = builder.Configuration.GetValue<int?>("Logging:File:MaxFiles") ?? 5;
builder.Logging.AddProvider(new JsonFileLoggerProvider(logPath, minLevel, maxBytes, maxFiles));

var ouiPath = builder.Configuration.GetValue<string>("Classification:OuiPath");
if (string.IsNullOrWhiteSpace(ouiPath))
{
    var candidate = Path.Combine(AppContext.BaseDirectory, "oui.csv");
    if (!File.Exists(candidate))
    {
        candidate = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "..", "shared", "oui.csv"));
    }
    ouiPath = candidate;
}

builder.Services.AddSingleton(new OuiVendorLookup(ouiPath));
builder.Services.AddSingleton<DeviceClassifier>();
builder.Services.AddSingleton<AnalyticsService>();
builder.Services.AddSingleton<RiskScorer>();
builder.Services.AddSingleton<EventHub>();
builder.Services.AddSingleton<DeviceStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DeviceStore>>();
    var hub = sp.GetRequiredService<EventHub>();
    var classifier = sp.GetRequiredService<DeviceClassifier>();
    var dbPath = Path.Combine(dataRoot, "netwatch.db");
    return new DeviceStore(dbPath, logger, hub, classifier);
});
builder.Services.AddSingleton<SettingsStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SettingsStore>>();
    var defaults = sp.GetRequiredService<IOptions<DiscoveryOptions>>().Value;
    var settingsPath = Path.Combine(dataRoot, "netwatch.settings.json");
    return new SettingsStore(settingsPath, defaults, logger);
});
builder.Services.AddSingleton<UpnpIgdConnector>();
builder.Services.AddSingleton<SnmpConnector>();
builder.Services.AddSingleton<DhcpHttpConnector>();
builder.Services.AddSingleton<UnifiControllerConnector>();
builder.Services.AddSingleton<IRouterConnector>(sp => sp.GetRequiredService<UpnpIgdConnector>());
builder.Services.AddSingleton<IRouterConnector>(sp => sp.GetRequiredService<SnmpConnector>());
builder.Services.AddSingleton<IRouterConnector>(sp => sp.GetRequiredService<DhcpHttpConnector>());
builder.Services.AddSingleton<IRouterConnector>(sp => sp.GetRequiredService<UnifiControllerConnector>());
builder.Services.AddSingleton<IRouterConnector>(sp =>
    new LeaseHttpConnector("tplink", "tplink", s => s.TpLink,
        sp.GetRequiredService<DeviceStore>(),
        sp.GetRequiredService<ILogger<LeaseHttpConnector>>(),
        sp.GetRequiredService<CredentialVault>()));
builder.Services.AddSingleton<IRouterConnector>(sp =>
    new LeaseHttpConnector("netgear", "netgear", s => s.Netgear,
        sp.GetRequiredService<DeviceStore>(),
        sp.GetRequiredService<ILogger<LeaseHttpConnector>>(),
        sp.GetRequiredService<CredentialVault>()));
builder.Services.AddSingleton<IRouterConnector>(sp =>
    new LeaseHttpConnector("orbi", "netgear-orbi", s => s.Orbi,
        sp.GetRequiredService<DeviceStore>(),
        sp.GetRequiredService<ILogger<LeaseHttpConnector>>(),
        sp.GetRequiredService<CredentialVault>()));
builder.Services.AddSingleton<IRouterConnector>(sp =>
    new LeaseHttpConnector("omada", "omada", s => s.Omada,
        sp.GetRequiredService<DeviceStore>(),
        sp.GetRequiredService<ILogger<LeaseHttpConnector>>(),
        sp.GetRequiredService<CredentialVault>()));
builder.Services.AddSingleton<IRouterConnector>(sp =>
    new LeaseHttpConnector("asus", "asus", s => s.Asus,
        sp.GetRequiredService<DeviceStore>(),
        sp.GetRequiredService<ILogger<LeaseHttpConnector>>(),
        sp.GetRequiredService<CredentialVault>()));
builder.Services.AddSingleton<ConnectorStatusStore>();
builder.Services.AddSingleton<ConnectorRegistry>();
builder.Services.AddSingleton<RouterFingerprintStore>();
builder.Services.AddSingleton<ConnectorSettingsStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ConnectorSettingsStore>>();
    var path = Path.Combine(dataRoot, "netwatch.connectors.json");
    return new ConnectorSettingsStore(path, logger);
});
builder.Services.AddSingleton<CredentialVault>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CredentialVault>>();
    var path = Path.Combine(dataRoot, "netwatch.credentials.json");
    return new CredentialVault(path, logger);
});
builder.Services.AddSingleton<AgentTokenStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<AgentTokenStore>>();
    var path = Path.Combine(dataRoot, "netwatch.agent.tokens.json");
    return new AgentTokenStore(path, logger);
});
builder.Services.AddSingleton<AgentAuthService>();
builder.Services.AddHostedService<LeaveDetectorService>();
builder.Services.AddHostedService<ActiveDiscoveryService>();
builder.Services.AddHostedService<PassiveDiscoveryService>();
builder.Services.AddHostedService<RouterConnectorService>();
builder.Services.AddHostedService<RouterFingerprintService>();
builder.Services.Configure<DiscoveryOptions>(builder.Configuration.GetSection("Discovery"));
builder.Services.Configure<ConnectorOptions>(builder.Configuration.GetSection("Connectors"));
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

var uiPath = builder.Configuration.GetValue<string>("Ui:Path");
if (string.IsNullOrWhiteSpace(uiPath))
{
    uiPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "..", "ui-web"));
}
if (Directory.Exists(uiPath))
{
    var provider = new PhysicalFileProvider(uiPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/devices", (DeviceStore store) => Results.Ok(store.GetAll()));

app.MapGet("/api/events", (DeviceStore store) => Results.Ok(store.GetEvents()));

app.MapGet("/api/settings", (SettingsStore store) => Results.Ok(store.Get()));

app.MapPost("/api/settings", (SettingsStore store, DiscoveryOptions options) =>
{
    var updated = store.Update(options);
    return Results.Ok(updated);
});

app.MapGet("/api/connectors", (ConnectorSettingsStore store) => Results.Ok(store.Get()));

app.MapPost("/api/connectors", (ConnectorSettingsStore store, ConnectorSettings settings) =>
{
    var updated = store.Update(settings);
    return Results.Ok(updated);
});

app.MapGet("/api/connectors/status", (ConnectorRegistry registry) => Results.Ok(registry.GetStatus()));

app.MapGet("/api/connectors/fingerprints", (RouterFingerprintStore store) =>
{
    return Results.Ok(new { lastScanUtc = store.LastScanUtc, items = store.GetAll() });
});

app.MapGet("/api/ops/diagnostics", (DeviceStore store, RouterFingerprintStore fingerprints, ConnectorRegistry registry, IConfiguration config) =>
{
    var startUtc = AppStart.StartedAtUtc;
    var uptime = DateTime.UtcNow - startUtc;
    var logFile = config.GetValue<string>("Logging:File:Path") ?? Path.Combine(AppContext.BaseDirectory, "logs", "netwatch.log");
    var uiPathLocal = config.GetValue<string>("Ui:Path") ?? string.Empty;

    return Results.Ok(new
    {
        startedAtUtc = startUtc,
        uptimeSeconds = (int)uptime.TotalSeconds,
        deviceCount = store.GetAll().Count,
        lastFingerprintScanUtc = fingerprints.LastScanUtc,
        connectorStatus = registry.GetStatus(),
        logFilePath = logFile,
        uiPath = uiPathLocal
    });
});

app.MapGet("/api/ops/export", (DeviceStore store, int? observationLimit) =>
{
    var limit = observationLimit ?? 500;
    return Results.Ok(new
    {
        generatedAtUtc = DateTime.UtcNow,
        devices = store.GetAll(),
        events = store.GetEvents(500),
        observations = store.GetRecentObservations(limit)
    });
});

app.MapGet("/api/analytics/summary", (AnalyticsService analytics, int? hours) =>
{
    var window = TimeSpan.FromHours(hours ?? 24);
    return Results.Ok(analytics.GetSummary(window));
});

app.MapGet("/api/devices/{id}/summary", (AnalyticsService analytics, string id, int? hours) =>
{
    var window = TimeSpan.FromHours(hours ?? 24);
    return Results.Ok(analytics.GetDeviceSummary(id, window));
});

app.MapGet("/api/credentials", (CredentialVault vault) => Results.Ok(vault.List()));

app.MapPost("/api/credentials", (CredentialVault vault, CredentialCreateRequest request) =>
{
    var info = vault.Create(request.Name, request.Purpose, request.Secret);
    return Results.Ok(info);
});

app.MapDelete("/api/credentials/{id}", (CredentialVault vault, string id) =>
{
    return vault.Delete(id) ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/credentials/test", (CredentialVault vault, CredentialTestRequest request) =>
{
    return vault.TryGetSecret(request.Id, out _) ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/observations", async (DeviceStore store, HttpRequest request, AgentAuthService auth) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(body))
    {
        return Results.BadRequest();
    }

    if (!auth.Validate(request, body))
    {
        return Results.Unauthorized();
    }

    var observation = JsonSerializer.Deserialize<Observation>(body, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });
    if (observation is null)
    {
        return Results.BadRequest();
    }

    var device = store.UpsertFromObservation(observation);
    return Results.Ok(device);
});

app.MapPost("/api/observations/batch", async (DeviceStore store, HttpRequest request, AgentAuthService auth) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(body))
    {
        return Results.BadRequest();
    }

    if (!auth.Validate(request, body))
    {
        return Results.Unauthorized();
    }

    var observations = JsonSerializer.Deserialize<List<Observation>>(body, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });
    if (observations is null)
    {
        return Results.BadRequest();
    }

    foreach (var observation in observations)
    {
        if (observation is null)
        {
            continue;
        }
        store.UpsertFromObservation(observation);
    }

    return Results.Ok(new { count = observations.Count });
});

app.MapPost("/api/agent/register", (AgentTokenStore tokenStore, IConfiguration config, AgentRegisterRequest request) =>
{
    var enabled = config.GetValue<bool?>("Agent:RegistrationEnabled") ?? false;
    var expected = config.GetValue<string>("Agent:RegistrationCode");
    if (!enabled || string.IsNullOrWhiteSpace(expected))
    {
        return Results.NotFound();
    }

    if (!string.Equals(expected, request.Code, StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    var token = tokenStore.Add(request.Name ?? "agent");
    return Results.Ok(new AgentRegisterResponse(token.Token, "token"));
});

app.MapGet("/api/stream", async (HttpContext context, EventHub hub) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    await foreach (var msg in hub.Stream(context.RequestAborted))
    {
        await context.Response.WriteAsync($"event: {msg.Type}\n");
        await context.Response.WriteAsync($"data: {msg.Data}\n\n");
        await context.Response.Body.FlushAsync();
    }
});

app.MapGet("/api/devices/{id}/observations", (DeviceStore store, string id, int? limit) =>
{
    var rows = store.GetObservationsForDevice(id, limit ?? 50);
    return Results.Ok(rows);
});

app.MapGet("/api/devices/{id}/classification", (DeviceStore store, DeviceClassifier classifier, string id) =>
{
    var observations = store.GetObservationsForDevice(id, 100);
    var device = store.GetAll().FirstOrDefault(d => d.DeviceId == id);
    if (device is null)
    {
        return Results.NotFound();
    }

    var result = classifier.Classify(device, observations);
    return Results.Ok(result);
});

app.MapGet("/api/devices/{id}/risk", (DeviceStore store, RiskScorer scorer, string id) =>
{
    var device = store.GetAll().FirstOrDefault(d => d.DeviceId == id);
    if (device is null)
    {
        return Results.NotFound();
    }
    var observations = store.GetObservationsForDevice(id, 100);
    var risk = scorer.Score(device, observations);
    return Results.Ok(risk);
});

app.Run();
