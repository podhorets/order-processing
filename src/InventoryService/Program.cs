using Contracts.Commands;
using Contracts.Events;
using InventoryService.Infrastructure.Http;
using InventoryService.Infrastructure.Observability;
using InventoryService.Infrastructure.Persistence;
using JasperFx.Core;
using JasperFx.Resources;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using Serilog;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
var config  = builder.Configuration;

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// ── Metrics ───────────────────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .AddMeter("InventoryService")
        .AddPrometheusExporter());

builder.Services.AddSingleton<InventoryMetrics>();

// ── Persistence ───────────────────────────────────────────────────────────────
builder.Services.AddPersistence(config);

// ── Wolverine ─────────────────────────────────────────────────────────────────
builder.UseWolverine(opts =>
{
    opts.UseRabbitMq(new Uri(config.GetConnectionString("RabbitMq")!))
        .AutoProvision();

    opts.PersistMessagesWithPostgresql(config.GetConnectionString("Database")!, "wolverine");
    opts.UseEntityFrameworkCoreTransactions();

    // Retry optimistic concurrency conflicts from xmin token on Inventory rows
    opts.OnException<DbUpdateConcurrencyException>()
        .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds());

    opts.OnException<Exception>().MoveToErrorQueue();

    // Receive commands from OrderService
    opts.ListenToRabbitQueue("inventory-service");

    // Publish result events back to OrderService saga
    opts.PublishMessage<InventoryReservedEvent>().ToRabbitQueue("order-service-events");
    opts.PublishMessage<InventoryReservationFailedEvent>().ToRabbitQueue("order-service-events");
    opts.PublishMessage<InventoryReleasedEvent>().ToRabbitQueue("order-service-events");
    opts.PublishMessage<InventoryFulfilledEvent>().ToRabbitQueue("order-service-events");
});

builder.Host.UseResourceSetupOnStartup();

// ── API / HTTP ────────────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

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
