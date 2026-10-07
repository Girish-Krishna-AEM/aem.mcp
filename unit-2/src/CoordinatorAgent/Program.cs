using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoordinatorAgent.Services;
using System;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Read configuration from environment variables
var listenPort = builder.Configuration["LISTEN_PORT"] ?? "8001";
var logLevel = builder.Configuration["LOG_LEVEL"] ?? "INFO";
var mcpServerUrl = builder.Configuration["MCP_SERVER_URL"] ?? "http://lightning-mcp-server:8000";

// Configure logging: structured JSON to stdout only
builder.Logging.ClearProviders();
builder.Logging
    .AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";
        options.UseUtcTimestamp = true;
    });

var parsedLogLevel = logLevel.ToUpperInvariant() switch
{
    "TRACE" => LogLevel.Trace,
    "DEBUG" => LogLevel.Debug,
    "INFO" or "INFORMATION" => LogLevel.Information,
    "WARN" or "WARNING" => LogLevel.Warning,
    "ERROR" => LogLevel.Error,
    "CRITICAL" => LogLevel.Critical,
    "NONE" => LogLevel.None,
    _ => LogLevel.Information
};
builder.Logging.SetMinimumLevel(parsedLogLevel);

// Configure Kestrel to listen on specified port
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(listenPort));
});

// Register Coordinator services
builder.Services.AddSingleton<IIntentClassifier, IntentClassifier>();
builder.Services.AddSingleton<IParameterExtractor, ParameterExtractor>();
builder.Services.AddScoped<IMcpClient, McpClient>();

// Register the named HTTP client used to call the Lightning MCP Server
builder.Services.AddHttpClient(McpClient.HttpClientName, client =>
{
    client.BaseAddress = new Uri(mcpServerUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Register the Location Geocoding Utility (FR-4): in-memory cache + Census Geocoder (primary) + Nominatim (fallback)
builder.Services.AddMemoryCache();
builder.Services.Configure<CensusGeocoderOptions>(options =>
{
    options.BaseUrl = builder.Configuration["CENSUS_GEOCODER_BASE_URL"] ?? options.BaseUrl;
    options.TimeoutSeconds = int.TryParse(builder.Configuration["CENSUS_GEOCODER_TIMEOUT_SECONDS"], out var timeoutSeconds)
        ? timeoutSeconds
        : options.TimeoutSeconds;
});
builder.Services.AddHttpClient<ILocationGeocoder, LocationGeocoder>();

// Nominatim is used only as a fallback when the Census Geocoder finds no match
// (e.g. City+State or bare ZIP input, which Census's address-range-only endpoint cannot resolve).
var nominatimOptions = new NominatimOptions
{
    BaseUrl = builder.Configuration["NOMINATIM_BASE_URL"] ?? new NominatimOptions().BaseUrl,
    TimeoutSeconds = int.TryParse(builder.Configuration["NOMINATIM_TIMEOUT_SECONDS"], out var nominatimTimeoutSeconds)
        ? nominatimTimeoutSeconds
        : new NominatimOptions().TimeoutSeconds,
    UserAgent = builder.Configuration["NOMINATIM_USER_AGENT"] ?? new NominatimOptions().UserAgent
};
builder.Services.Configure<NominatimOptions>(options =>
{
    options.BaseUrl = nominatimOptions.BaseUrl;
    options.TimeoutSeconds = nominatimOptions.TimeoutSeconds;
    options.UserAgent = nominatimOptions.UserAgent;
});
builder.Services.AddHttpClient(LocationGeocoder.NominatimHttpClientName, client =>
{
    client.BaseAddress = new Uri(nominatimOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(nominatimOptions.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(nominatimOptions.UserAgent);
});

// Add controllers
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = false;
});

var app = builder.Build();

// Configure middleware
app.UseRouting();
app.MapControllers();

// Log startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Coordinator Agent starting on port {ListenPort} with log level {LogLevel}, MCP Server at {McpServerUrl}",
    listenPort, logLevel, mcpServerUrl);

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    logger.LogInformation("Coordinator Agent started successfully");
});

lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation("Coordinator Agent shutting down gracefully");
});

await app.RunAsync();
