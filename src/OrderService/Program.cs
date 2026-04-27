using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Metrics;
using OrderService.Domain.Entities;
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
builder.Services.AddScoped<AddInventoryHandler>();

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
app.MapPost("/inventories", (AddInventory inventory, [FromServices] AddInventoryHandler handler, CancellationToken ct) => handler.Handle(inventory, ct));

app.Run();

public sealed record AddInventory(string Sku, int OnHand);

public class AddInventoryHandler(OrderDbContext ctx)
{
    public async Task<IResult> Handle(AddInventory request, CancellationToken cancellationToken)
    {
        var inventory = new Inventory(request.Sku, request.OnHand);
        ctx.Inventories.Add(inventory);
        await ctx.SaveChangesAsync(cancellationToken);
        return Results.Created($"/inventories/{inventory.Id}", new { inventory.Id });
    }
}
