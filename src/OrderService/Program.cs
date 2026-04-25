using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<SubmitOrderHandler>();
builder.Services.AddDbContext<OrderDbContext>(opts =>
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
app.MapPost("/order", (SubmitOrder order, [FromServices] SubmitOrderHandler handler, CancellationToken ct) => handler.Handle(order, ct));
app.Run();
async Task ApplyDatabaseMigrations(WebApplication webApplication)
{
    using var scope = webApplication.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    var pending = await db.Database.GetPendingMigrationsAsync();
    if (pending.Any())
    {
        await db.Database.MigrateAsync();
    }
}

public sealed record SubmitOrder(Guid CustomerId, List<OrderItemDto> OrderItems);
public sealed record OrderItemDto(string Sku, int Quantity, decimal UnitPrice);

public class SubmitOrderHandler(OrderDbContext ctx)
{
    public async Task<IResult> Handle(SubmitOrder request, CancellationToken cancellationToken)
    {
        var order = Order.Submit(request.CustomerId, request.OrderItems);
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync(cancellationToken);
        return Results.Created($"/orders/{order.Id}", new { order.Id });
    }
}