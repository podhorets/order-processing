using Contracts.Commands;
using Contracts.Events;
using JasperFx.Core;
using JasperFx.Resources;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using PaymentService.Infrastructure.Http;
using PaymentService.Infrastructure.Observability;
using PaymentService.Infrastructure.Persistence;
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
        .AddMeter("PaymentService")
        .AddPrometheusExporter());

builder.Services.AddSingleton<PaymentMetrics>();

// ── Persistence ───────────────────────────────────────────────────────────────
builder.Services.AddPersistence(config);

// ── Wolverine ─────────────────────────────────────────────────────────────────
builder.UseWolverine(opts =>
{
    opts.UseRabbitMq(new Uri(config.GetConnectionString("RabbitMq")!))
        .AutoProvision();

    opts.PersistMessagesWithPostgresql(config.GetConnectionString("Database")!, "wolverine");
    opts.UseEntityFrameworkCoreTransactions();

    opts.OnException<DbUpdateConcurrencyException>()
        .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds());

    opts.OnException<Exception>().MoveToErrorQueue();

    // Receive payment commands from OrderService
    opts.ListenToRabbitQueue("payment-service");

    // Publish results back to OrderService saga
    opts.PublishMessage<PaymentSuccessfulEvent>().ToRabbitQueue("order-service-events");
    opts.PublishMessage<PaymentFailedEvent>().ToRabbitQueue("order-service-events");
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

app.Run();
