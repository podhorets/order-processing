using InventoryService.Domain.Entities;
using InventoryService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<AddInventoryHandler>();
builder.Services.AddDbContext<InventoryDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

var app = builder.Build();

// for production-grade: apply migrations as a separate CI/CD step
await ApplyDatabaseMigrations(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapPost("/inventories", (AddInventory inventory, [FromServices] AddInventoryHandler handler, CancellationToken ct) => handler.Handle(inventory, ct));
app.Run();
async Task ApplyDatabaseMigrations(WebApplication webApplication)
{
    using var scope = webApplication.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    var pending = await db.Database.GetPendingMigrationsAsync();
    if (pending.Any())
    {
        await db.Database.MigrateAsync();
    }
}

public sealed record AddInventory(string Sku, int OnHand);

public class AddInventoryHandler(InventoryDbContext ctx)
{
    public async Task<IResult> Handle(AddInventory request, CancellationToken cancellationToken)
    {
        var inventory = new Inventory(request.Sku, request.OnHand);
        ctx.Inventories.Add(inventory);
        await ctx.SaveChangesAsync(cancellationToken);
        return Results.Created($"/inventories/{inventory.Id}", new { inventory.Id });
    }
}