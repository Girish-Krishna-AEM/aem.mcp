using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LightningMcpServer.DomainModules;
using LightningMcpServer.ExternalApis;
using LightningMcpServer.Mcp;
using System;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Read configuration from environment variables
var listenPort = builder.Configuration["LISTEN_PORT"] ?? "8000";
var logLevel = builder.Configuration["LOG_LEVEL"] ?? "INFO";

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

// Lightning pulse external API configuration (configurable base URL and API key)
builder.Services.Configure<LightningPulseApiOptions>(options =>
{
    options.BaseUrl = builder.Configuration["LIGHTNING_PULSE_API_BASE_URL"] ?? options.BaseUrl;
    options.ApiKey = builder.Configuration["LIGHTNING_PULSE_API_KEY"] ?? string.Empty;
});
builder.Services.AddHttpClient<ILightningPulseApiClient, LightningPulseApiClient>();

// Register domain modules
builder.Services.AddScoped<IStrikeDetectionModule, StrikeDetectionModule>();
builder.Services.AddScoped<IWeatherForecastModule, WeatherForecastModule>();
builder.Services.AddScoped<ISensorDiagnosticsModule, SensorDiagnosticsModule>();
builder.Services.AddScoped<IInformerStatusModule, InformerStatusModule>();

// Register MCP components
builder.Services.AddScoped<IToolRegistry, ToolRegistry>();

// Register spec-compliant MCP server (additive — existing /mcp/messages controller is untouched)
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

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
app.MapMcp("/mcp");

// Log startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Lightning MCP Server starting on port {ListenPort} with log level {LogLevel}",
    listenPort, logLevel);

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    logger.LogInformation("Lightning MCP Server started successfully");
});

lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation("Lightning MCP Server shutting down gracefully");
});

await app.RunAsync();
