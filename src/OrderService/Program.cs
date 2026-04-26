using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Extensions;
using OrderService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddBackgroundJobs(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddScoped<AddInventoryHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHangfireDashboard(options: new DashboardOptions
    {
        Authorization = []
    });
    app.ApplyMigrations();
}

app.MapHealthChecks("/health");
app.MapEndpoints();
app.MapPost("/inventories", (AddInventory inventory, [FromServices] AddInventoryHandler handler, CancellationToken ct) => handler.Handle(inventory, ct));

app.UseBackgroundJobs();

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
