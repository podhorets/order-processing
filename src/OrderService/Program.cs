using FluentValidation;
using OpenTelemetry.Metrics;
using OrderService.Infrastructure.Http;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Observability;
using OrderService.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, config) =>
    config.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("OrderService")
        .AddPrometheusExporter());

builder.Services.AddSingleton<OrderMetrics>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddOutbox(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.ApplyMigrations();
}

app.UseExceptionHandler();
app.MapPrometheusScrapingEndpoint();
app.MapHealthChecks("/health");
app.MapEndpoints();

app.Run();
