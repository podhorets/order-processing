using Contracts.Commands;
using FluentValidation;
using JasperFx.Core;
using JasperFx.Resources;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderService.Infrastructure.Http;
using OrderService.Infrastructure.Observability;
using OrderService.Infrastructure.Persistence;
using Serilog;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
var config  = builder.Configuration;

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// ── Observability: metrics + traces → OTel Collector → Prometheus / Tempo ─────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("order-service"))
    .WithMetrics(m => m
        .AddMeter("OrderService")
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithTracing(t => t
        .AddSource("Wolverine")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter());

builder.Services.AddSingleton<OrderMetrics>();

// ── Persistence (DbContext registered with Wolverine integration) ─────────────
builder.Services.AddPersistence(config);

// ── Wolverine ─────────────────────────────────────────────────────────────────
builder.UseWolverine(opts =>
{
    // RabbitMQ transport — auto-creates queues/exchanges on startup
    opts.UseRabbitMq(new Uri(config.GetConnectionString("RabbitMq")!))
        .AutoProvision();

    // Wolverine's own outbox / inbox / scheduled-message tables in PostgreSQL
    opts.PersistMessagesWithPostgresql(config.GetConnectionString("Database")!, "wolverine");

    // Wrap every handler in an EF Core transaction; writes outbox messages atomically
    opts.UseEntityFrameworkCoreTransactions();

    // Retry optimistic concurrency conflicts (e.g., two events hitting the same saga)
    opts.OnException<DbUpdateConcurrencyException>()
        .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds());

    opts.OnException<Exception>().MoveToErrorQueue();

    // ── Routing: outbound ────────────────────────────────────────────────────
    opts.PublishMessage<ReserveInventoryCommand>().ToRabbitQueue("inventory-service");
    opts.PublishMessage<ReleaseInventoryCommand>().ToRabbitQueue("inventory-service");
    opts.PublishMessage<FulfillInventoryCommand>().ToRabbitQueue("inventory-service");
    opts.PublishMessage<AddInventoryCommand>().ToRabbitQueue("inventory-service");
    opts.PublishMessage<ProcessPaymentCommand>().ToRabbitQueue("payment-service");

    opts.Policies.UseDurableInboxOnAllListeners();
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
    
    // ── Routing: inbound ─────────────────────────────────────────────────────
    // Events published by InventoryService and PaymentService; consumed by the saga
    opts.ListenToRabbitQueue("order-service-events");
});

// ── Wolverine resource setup (creates wolverine schema tables on startup) ─────
builder.Host.UseResourceSetupOnStartup();

// ── API / HTTP ────────────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.ApplyMigrations();
}

app.UseExceptionHandler();
app.MapHealthChecks("/health");
app.MapEndpoints();

app.Run();
