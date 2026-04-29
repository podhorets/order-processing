# OrderProcessing v2 — Full Implementation Plan

> **For the coding agent:** This document is self-contained. Implement everything described here from scratch. No prior context exists. Read every section before writing any code.

---

## Goal

Three microservices (`OrderService`, `InventoryService`, `PaymentService`) using **Wolverine** for messaging, transactional outbox, inbox deduplication, and saga orchestration. No manual RabbitMQ client code, no custom outbox, no custom inbox — Wolverine handles all of it automatically.

---

## Architecture

```
Client → OrderService (HTTP + Wolverine saga orchestrator)
              ↓ commands via RabbitMQ
   InventoryService        PaymentService
              ↓ events via RabbitMQ
         OrderService (saga continues)
```

Each service has its **own PostgreSQL database**. Services communicate **only via messages** — no cross-service DB queries ever.

---

## Tech Stack (every service)

| Concern | Library |
|---|---|
| Framework | .NET 8, Minimal APIs |
| Messaging / outbox / inbox / saga | `Wolverine` 3.x |
| Transport | `Wolverine.RabbitMQ` 3.x |
| EF Core integration | `Wolverine.EntityFrameworkCore` 3.x |
| ORM | EF Core 9 + `Npgsql.EntityFrameworkCore.PostgreSQL` |
| Database | PostgreSQL 17 (one per service) |
| Broker | RabbitMQ 4.x |
| Logging | `Serilog.AspNetCore` |
| Metrics | `OpenTelemetry.Extensions.Hosting` + `OpenTelemetry.Exporter.Prometheus.AspNetCore` |
| Validation | `FluentValidation` + `FluentValidation.DependencyInjectionExtensions` |
| UUID generation | `UUIDNext` |

---

## Project Structure

```
/OrderProcessing.slnx
/src
  /Contracts              ← class library — all shared messages, referenced by all 3 services
  /OrderService           ← HTTP API + Wolverine saga orchestrator
  /InventoryService       ← inventory domain + command handlers
  /PaymentService         ← mock payment handler
/compose.yaml
/README.md
```

Each service is a `Microsoft.NET.Sdk.Web` project. `Contracts` is a `Microsoft.NET.Sdk` class library.

---

## Shared Contracts (Contracts project)

All messages are **immutable records**. Every service references this project directly (or as a NuGet package in a real enterprise setup).

### Commands (point-to-point — routed to a specific service queue)

```csharp
// Sent by OrderService → InventoryService
public record ReserveInventoryCommand(Guid OrderId, IReadOnlyList<OrderItemDto> Items);
public record ReleaseInventoryCommand(Guid OrderId);   // compensation when payment fails
public record FulfillInventoryCommand(Guid OrderId);   // reduce OnHand after payment succeeds

// Sent by OrderService → PaymentService
public record ProcessPaymentCommand(Guid OrderId, Guid CustomerId, decimal Amount);
```

### Events (published by downstream services → consumed by OrderService saga)

```csharp
// Published by InventoryService
public record InventoryReservedEvent(Guid OrderId);
public record InventoryReservationFailedEvent(Guid OrderId, string Reason);
public record InventoryReleasedEvent(Guid OrderId);
public record InventoryFulfilledEvent(Guid OrderId);

// Published by PaymentService
public record PaymentSuccessfulEvent(Guid OrderId);
public record PaymentFailedEvent(Guid OrderId, string Reason);
```

### DTOs

```csharp
public record OrderItemDto(string Sku, int Quantity, decimal UnitPrice);
```

---

## Service 1: OrderService

### Responsibilities

- Accept order submissions via HTTP
- Orchestrate the full order lifecycle via a Wolverine saga
- Expose order status via HTTP read endpoint
- Expose batch endpoint for load testing

### Wolverine Saga — how it works

A Wolverine `Saga` is a class that:
- Inherits from `Saga`
- Has a `Guid Id` property (the correlation key — must match `OrderId` on all messages)
- Has `static Start(...)` or `static Handle(...)` methods that return `(Saga, ...messages)`  
- Has instance `Handle(...)` methods for subsequent messages
- Calls `MarkCompleted()` when done — Wolverine deletes the saga row

Wolverine persists saga state automatically in PostgreSQL (via `PersistMessagesWithPostgresql`). It correlates incoming events to the right saga instance by matching `evt.OrderId == saga.Id` **by property name convention** — no manual lookup needed.

### Saga state

```csharp
public enum OrderSagaStatus
{
    ReservingInventory,
    ProcessingPayment,
    Fulfilling,
    Compensating,
    Completed,
    Failed,
    TimedOut
}
```

### OrderSaga.cs

```csharp
// Saga/OrderSaga.cs
public class OrderSaga : Saga
{
    public Guid Id { get; set; }                         // = OrderId — Wolverine uses this for correlation
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItemDto> Items { get; set; } = [];  // kept so FulfillInventoryCommand can reference them if needed
    public OrderSagaStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // ── Start: triggered by local SubmitOrderCommand ─────────────────────────────
    // Returns: the initial saga state + first outgoing command + scheduled timeout.
    public static (OrderSaga, ReserveInventoryCommand, DeliveryOptions) Start(SubmitOrderCommand cmd)
    {
        var saga = new OrderSaga
        {
            Id          = cmd.OrderId,
            CustomerId  = cmd.CustomerId,
            TotalAmount = cmd.TotalAmount,
            Items       = cmd.Items.ToList(),
            Status      = OrderSagaStatus.ReservingInventory,
            CreatedAt   = DateTime.UtcNow
        };

        // Schedule a timeout 5 minutes from now.
        // Wolverine will deliver OrderSagaTimeout back to this saga after 5 min.
        // Use DeliveryOptions to set scheduled time on the outgoing message.
        // Exact Wolverine v3 API for scheduling from saga Start:
        //   Return (saga, command) and schedule the timeout separately inside the method
        //   using IMessageContext injected as parameter, OR
        //   use the overload that accepts scheduled messages.
        // Consult Wolverine v3 docs for the exact scheduled-message API from saga Start.

        return (saga, new ReserveInventoryCommand(cmd.OrderId, cmd.Items), new DeliveryOptions
        {
            ScheduleDelay = TimeSpan.FromMinutes(5)
            // Attach to OrderSagaTimeout — see note above on exact API
        });
        // NOTE: if the tuple overload does not support DeliveryOptions directly,
        // inject IMessageContext into Start() as a parameter and call:
        //   await context.ScheduleAsync(new OrderSagaTimeout(cmd.OrderId), TimeSpan.FromMinutes(5));
        // then return (saga, new ReserveInventoryCommand(...));
    }

    // ── Happy path ────────────────────────────────────────────────────────────────

    public ProcessPaymentCommand Handle(InventoryReservedEvent evt)
    {
        Status = OrderSagaStatus.ProcessingPayment;
        return new ProcessPaymentCommand(Id, CustomerId, TotalAmount);
    }

    public FulfillInventoryCommand Handle(PaymentSuccessfulEvent evt)
    {
        Status = OrderSagaStatus.Fulfilling;
        return new FulfillInventoryCommand(Id);
    }

    public void Handle(InventoryFulfilledEvent evt)
    {
        Status      = OrderSagaStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        MarkCompleted(); // Wolverine deletes the saga row from DB
    }

    // ── Failure: reservation failed → reject immediately ─────────────────────────

    public void Handle(InventoryReservationFailedEvent evt)
    {
        Status          = OrderSagaStatus.Failed;
        RejectionReason = evt.Reason;
        CompletedAt     = DateTime.UtcNow;
        MarkCompleted();
    }

    // ── Failure: payment failed → release reservation first ──────────────────────

    public ReleaseInventoryCommand Handle(PaymentFailedEvent evt)
    {
        Status          = OrderSagaStatus.Compensating;
        RejectionReason = evt.Reason;
        return new ReleaseInventoryCommand(Id);
    }

    public void Handle(InventoryReleasedEvent evt)
    {
        Status      = OrderSagaStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        MarkCompleted();
    }

    // ── Timeout ───────────────────────────────────────────────────────────────────

    public void Handle(OrderSagaTimeout timeout)
    {
        if (Status is OrderSagaStatus.Completed or OrderSagaStatus.Failed) return; // already done, no-op

        // If stuck in ReservingInventory or Compensating, consider publishing ReleaseInventoryCommand.
        // For now: mark timed out and end.
        Status      = OrderSagaStatus.TimedOut;
        CompletedAt = DateTime.UtcNow;
        MarkCompleted();
    }
}

// Timeout message — Wolverine correlates it to the saga via OrderId matching saga.Id
public record OrderSagaTimeout(Guid OrderId);
```

### SubmitOrderCommand (local command — starts the saga)

```csharp
// This is an OrderService-internal command. Not in Contracts.
public record SubmitOrderCommand(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items);
```

### API Endpoints

```
POST /api/v1/orders              → validate → publish SubmitOrderCommand → 202 Accepted
GET  /api/v1/orders/{id:guid}    → load saga state → 200 / 404
POST /api/v1/orders/batch        → load test: create N orders in parallel
POST /api/v1/orders/batch/concurrent → load test: N orders sharing one SKU (tests lock contention)
```

**SubmitOrderEndpoint:**
```csharp
// Features/SubmitOrder/SubmitOrderEndpoint.cs
public sealed class SubmitOrderEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
        => app.MapPost("/api/v1/orders", Handle)
            .WithName("SubmitOrder")
            .WithTags("Orders")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);

    private static async Task<IResult> Handle(
        SubmitOrderRequest request,
        IValidator<SubmitOrderRequest> validator,
        IMessageBus bus,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        var orderId = Uuid.NewSequential();
        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        await bus.SendAsync(new SubmitOrderCommand(
            orderId,
            request.CustomerId,
            totalAmount,
            request.Items.Select(i => new OrderItemDto(i.Sku, i.Quantity, i.UnitPrice)).ToList()));

        return Results.Accepted($"/api/v1/orders/{orderId}", new { OrderId = orderId });
    }
}
```

**SubmitOrderRequest + validator:**
```csharp
public sealed record SubmitOrderRequest(Guid CustomerId, List<SubmitOrderItem> Items);
public sealed record SubmitOrderItem(string Sku, int Quantity, decimal UnitPrice);

public sealed class SubmitOrderRequestValidator : AbstractValidator<SubmitOrderRequest>
{
    public SubmitOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Sku).NotEmpty().MaximumLength(100);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThan(0);
        });
        // No duplicate SKUs (case-insensitive)
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.Sku.ToLowerInvariant()).Distinct().Count() == items.Count)
            .WithMessage("Duplicate SKUs are not allowed.");
    }
}
```

**GetOrderEndpoint:**
```csharp
// Reads saga state from the Wolverine saga table.
// Wolverine stores sagas in a table named after the saga class.
// Use the DbContext (configured with Wolverine) or IQuerySession to load it.
public sealed class GetOrderEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
        => app.MapGet("/api/v1/orders/{id:guid}", Handle)
            .WithName("GetOrder")
            .WithTags("Orders")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

    private static async Task<IResult> Handle(Guid id, OrderDbContext ctx, CancellationToken ct)
    {
        // Wolverine persists OrderSaga into the DB — query it via EF or raw SQL.
        // The table name depends on Wolverine configuration — check generated schema.
        var saga = await ctx.Set<OrderSaga>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return saga is null
            ? Results.NotFound()
            : Results.Ok(new GetOrderResponse(saga.Id, saga.CustomerId, saga.Status.ToString(),
                saga.TotalAmount, saga.RejectionReason, saga.CreatedAt, saga.CompletedAt));
    }
}

public sealed record GetOrderResponse(
    Guid OrderId, Guid CustomerId, string Status, decimal TotalAmount,
    string? RejectionReason, DateTime CreatedAt, DateTime? CompletedAt);
```

**Batch endpoints:**

Both batch endpoints follow the same pattern: `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = Environment.ProcessorCount`, batch size 500, each parallel worker gets its own `IServiceScope`.

```
POST /api/v1/orders/batch
Body: { "orderAmount": 100 }   (max 1000)
→ Each order gets a unique SKU ("B-{uuid}") with onHand=1 — no lock contention.
→ Creates inventory + publishes SubmitOrderCommand for each order.
→ Returns 202: { batchId, ordersQueued }

POST /api/v1/orders/batch/concurrent
Body: { "orderAmount": 100 }   (max 1000)
→ All orders share one SKU ("CONCURRENT-{batchId}") with onHand=orderAmount.
→ Creates one inventory row first (separate request), then orders.
→ Stress-tests optimistic concurrency in InventoryService.
→ Returns 202: { batchId, ordersQueued, sharedSku }
```

In batch handlers, publish `SubmitOrderCommand` via `IMessageBus` per order. Each scope creates its own bus.

### Wolverine Configuration — OrderService

```csharp
// Program.cs
builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMq(new Uri(config["ConnectionStrings:RabbitMq"]!))
        .AutoProvisionRoutingTopology(); // auto-creates queues and exchanges on startup

    // Durable messaging: outbox + inbox stored in PostgreSQL under "wolverine" schema
    opts.PersistMessagesWithPostgresql(config.GetConnectionString("Database")!, "wolverine");

    // Automatically wrap handlers in EF Core transactions (enables outbox)
    opts.Policies.AutoApplyTransactions();

    // Retry optimistic concurrency conflicts (relevant if saga state is updated concurrently)
    opts.OnException<DbUpdateConcurrencyException>()
        .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds());

    // Move to dead-letter queue after max retries
    opts.OnException<Exception>()
        .MoveToErrorQueueOnFailure();

    // Route commands to downstream service queues
    opts.PublishMessage<ReserveInventoryCommand>().ToRabbitQueue("inventory-service");
    opts.PublishMessage<ReleaseInventoryCommand>().ToRabbitQueue("inventory-service");
    opts.PublishMessage<FulfillInventoryCommand>().ToRabbitQueue("inventory-service");
    opts.PublishMessage<ProcessPaymentCommand>().ToRabbitQueue("payment-service");

    // Receive events from downstream services
    opts.ListenToRabbitQueue("order-service-events");
});

// DbContext must be registered with Wolverine awareness
builder.Services.AddDbContext<OrderDbContext>((sp, opts) =>
    opts.UseNpgsql(config.GetConnectionString("Database"))
        .UseWolverine(sp)); // enables EF Core outbox integration
```

### AddInventory endpoint (OrderService or InventoryService?)

Seeding inventory belongs to **InventoryService**. OrderService does not manage inventory. The batch endpoints in OrderService need to create inventory — they should call the `InventoryService` HTTP API (`POST /api/v1/inventories`) or publish an `AddInventoryCommand`. For simplicity in the batch test endpoints, publish an internal command that InventoryService handles.

---

## Service 2: InventoryService

### Responsibilities

- Manage inventory stock (`OnHand`, `Reserved`)
- Handle `ReserveInventoryCommand`, `ReleaseInventoryCommand`, `FulfillInventoryCommand`
- Publish result events back to OrderService
- Expose HTTP API for seeding and querying inventory

### Domain

```csharp
// Domain/Entities/Inventory.cs
public class Inventory
{
    public Guid Id { get; private set; }
    public string Sku { get; private set; } = null!;
    public int OnHand { get; private set; }
    public int Reserved { get; private set; }
    public int Available => OnHand - Reserved;

    private Inventory() { }

    public Inventory(string sku, int onHand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegative(onHand);
        Id = Uuid.NewSequential();
        Sku = sku;
        OnHand = onHand;
    }

    public void Reserve(int qty)
    {
        if (qty <= 0) throw new ArgumentException("Quantity must be positive");
        if (Available < qty) throw new InvalidOperationException($"Insufficient stock: {Sku}");
        Reserved += qty;
    }

    public void Release(int qty)
    {
        if (qty <= 0) throw new ArgumentException("Quantity must be positive");
        Reserved = Math.Max(0, Reserved - qty);
    }

    public void Fulfill(int qty)
    {
        if (qty <= 0) throw new ArgumentException("Quantity must be positive");
        if (OnHand < qty) throw new InvalidOperationException($"Cannot fulfill more than OnHand: {Sku}");
        OnHand -= qty;
        Reserved -= qty;
    }

    public void AddStock(int qty)
    {
        if (qty <= 0) throw new ArgumentException("Quantity must be positive");
        OnHand += qty;
    }
}
```

```csharp
// Domain/Entities/Reservation.cs
public class Reservation
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Sku { get; private set; } = null!;
    public int Quantity { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Reservation() { }

    public Reservation(Guid orderId, string sku, int qty)
    {
        Id = Uuid.NewSequential();
        OrderId = orderId;
        Sku = sku;
        Quantity = qty;
        CreatedAt = DateTime.UtcNow;
    }
}
```

### EF Core Configuration — Optimistic Concurrency

**Critical:** InventoryService uses **optimistic concurrency** via PostgreSQL's `xmin` system column instead of `SELECT ... FOR UPDATE`. This means:

1. EF Core loads `Inventory` rows normally (no lock)
2. On `SaveChanges`, EF includes `WHERE xmin = @original_xmin` in the UPDATE
3. If another transaction modified the row, PostgreSQL's `xmin` changed → `DbUpdateConcurrencyException`
4. Wolverine catches this and **retries the entire handler** from the start (re-fetches from DB)
5. On retry, handler re-checks availability with fresh data

This is strictly better throughput than `FOR UPDATE` when conflicts are rare. Under heavy contention it retries — bounded by Wolverine's retry policy.

```csharp
// Infrastructure/Persistence/Configurations/InventoryConfiguration.cs
public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OnHand).IsRequired();
        builder.Property(x => x.Reserved).IsRequired().HasDefaultValue(0);
        builder.HasIndex(x => x.Sku).IsUnique();

        // xmin is a PostgreSQL system column — always present, no migration needed.
        // EF Core uses it as the concurrency token.
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_inventory_onhand_nonneg", "\"OnHand\" >= 0");
            t.HasCheckConstraint("ck_inventory_reserved_nonneg", "\"Reserved\" >= 0");
            t.HasCheckConstraint("ck_inventory_reserved_le_onhand", "\"Reserved\" <= \"OnHand\"");
        });
    }
}
```

```csharp
public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.OrderId).HasColumnType("uuid").IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
        // Unique: one reservation entry per (OrderId, Sku) — idempotency guard
        builder.HasIndex(x => new { x.OrderId, x.Sku }).IsUnique();
    }
}
```

### Message Handlers

All handlers are discovered automatically by Wolverine via convention (method named `Handle` or `HandleAsync`). Wolverine wraps each handler in a transaction (via `AutoApplyTransactions`) and enrolls the outgoing message in the outbox automatically.

```csharp
// Features/ReserveInventory/ReserveInventoryHandler.cs
public static class ReserveInventoryHandler
{
    public static async Task<object> Handle(
        ReserveInventoryCommand cmd,
        InventoryDbContext ctx,
        CancellationToken ct)
    {
        // Idempotency: if reservations already exist for this order, return success
        var alreadyReserved = await ctx.Reservations
            .AnyAsync(r => r.OrderId == cmd.OrderId, ct);

        if (alreadyReserved)
            return new InventoryReservedEvent(cmd.OrderId);

        var skus = cmd.Items.Select(i => i.Sku).Distinct().OrderBy(s => s).ToArray();

        var inventories = await ctx.Inventories
            .Where(i => skus.Contains(i.Sku))
            .ToDictionaryAsync(i => i.Sku, ct);

        // Validate all items before reserving any
        foreach (var item in cmd.Items)
        {
            if (!inventories.TryGetValue(item.Sku, out var inv))
                return new InventoryReservationFailedEvent(cmd.OrderId, $"SKU not found: {item.Sku}");

            if (inv.Available < item.Quantity)
                return new InventoryReservationFailedEvent(
                    cmd.OrderId,
                    $"Insufficient stock for {item.Sku}: requested {item.Quantity}, available {inv.Available}");
        }

        // All valid — reserve and create reservation rows
        foreach (var item in cmd.Items)
        {
            inventories[item.Sku].Reserve(item.Quantity);
            ctx.Reservations.Add(new Reservation(cmd.OrderId, item.Sku, item.Quantity));
        }

        // SaveChanges includes WHERE xmin = @orig — throws DbUpdateConcurrencyException on conflict.
        // Wolverine retries the handler automatically (re-fetches all data from DB).
        await ctx.SaveChangesAsync(ct);

        // Wolverine outbox: this event is stored in the outbox table in the same transaction
        // and delivered to RabbitMQ after commit. No message loss possible.
        return new InventoryReservedEvent(cmd.OrderId);
    }
}
```

```csharp
// Features/ReleaseInventory/ReleaseInventoryHandler.cs
public static class ReleaseInventoryHandler
{
    public static async Task<InventoryReleasedEvent> Handle(
        ReleaseInventoryCommand cmd,
        InventoryDbContext ctx,
        CancellationToken ct)
    {
        var reservations = await ctx.Reservations
            .Where(r => r.OrderId == cmd.OrderId)
            .ToListAsync(ct);

        if (reservations.Count == 0)
            return new InventoryReleasedEvent(cmd.OrderId); // already released — idempotent

        var skus = reservations.Select(r => r.Sku).ToList();
        var inventories = await ctx.Inventories
            .Where(i => skus.Contains(i.Sku))
            .ToDictionaryAsync(i => i.Sku, ct);

        foreach (var r in reservations)
            if (inventories.TryGetValue(r.Sku, out var inv))
                inv.Release(r.Quantity);

        ctx.Reservations.RemoveRange(reservations);
        await ctx.SaveChangesAsync(ct);

        return new InventoryReleasedEvent(cmd.OrderId);
    }
}
```

```csharp
// Features/FulfillInventory/FulfillInventoryHandler.cs
public static class FulfillInventoryHandler
{
    public static async Task<InventoryFulfilledEvent> Handle(
        FulfillInventoryCommand cmd,
        InventoryDbContext ctx,
        CancellationToken ct)
    {
        var reservations = await ctx.Reservations
            .Where(r => r.OrderId == cmd.OrderId)
            .ToListAsync(ct);

        if (reservations.Count == 0)
            return new InventoryFulfilledEvent(cmd.OrderId); // already fulfilled — idempotent

        var skus = reservations.Select(r => r.Sku).ToList();
        var inventories = await ctx.Inventories
            .Where(i => skus.Contains(i.Sku))
            .ToDictionaryAsync(i => i.Sku, ct);

        foreach (var r in reservations)
            if (inventories.TryGetValue(r.Sku, out var inv))
                inv.Fulfill(r.Quantity);

        ctx.Reservations.RemoveRange(reservations);
        await ctx.SaveChangesAsync(ct);

        return new InventoryFulfilledEvent(cmd.OrderId);
    }
}
```

### API Endpoints — InventoryService

```
POST /api/v1/inventories           → seed/add inventory for a SKU
GET  /api/v1/inventories/{sku}     → check stock level for a SKU
```

```csharp
// POST /api/v1/inventories
// Body: { "sku": "ABC-123", "onHand": 100 }
// If SKU already exists → AddStock(onHand). If not → create new Inventory row.
// Returns 201 with { id, sku, onHand, available }

// GET /api/v1/inventories/{sku}
// Returns 200 with { id, sku, onHand, reserved, available } or 404
```

### Wolverine Configuration — InventoryService

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMq(new Uri(config["ConnectionStrings:RabbitMq"]!))
        .AutoProvisionRoutingTopology();

    opts.PersistMessagesWithPostgresql(config.GetConnectionString("Database")!, "wolverine");
    opts.Policies.AutoApplyTransactions();

    // Retry concurrency conflicts — Wolverine re-runs the entire handler
    opts.OnException<DbUpdateConcurrencyException>()
        .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds());
    opts.OnException<Exception>()
        .MoveToErrorQueueOnFailure();

    // Listen for commands from OrderService
    opts.ListenToRabbitQueue("inventory-service");

    // Publish events back to OrderService saga
    opts.PublishMessage<InventoryReservedEvent>().ToRabbitQueue("order-service-events");
    opts.PublishMessage<InventoryReservationFailedEvent>().ToRabbitQueue("order-service-events");
    opts.PublishMessage<InventoryReleasedEvent>().ToRabbitQueue("order-service-events");
    opts.PublishMessage<InventoryFulfilledEvent>().ToRabbitQueue("order-service-events");
});

builder.Services.AddDbContext<InventoryDbContext>((sp, opts) =>
    opts.UseNpgsql(config.GetConnectionString("Database"))
        .UseWolverine(sp));
```

---

## Service 3: PaymentService

### Responsibilities

- Receive `ProcessPaymentCommand` from OrderService
- Mock: always approve payment
- Publish `PaymentSuccessfulEvent` (or `PaymentFailedEvent` for real implementation)
- Has its own PostgreSQL DB (for Wolverine's outbox/inbox tables + future PaymentRecord audit table)

### Domain (minimal)

```csharp
// Optional audit table — store payment attempts for observability
public class PaymentRecord
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public string Status { get; private set; } = null!; // "Approved" | "Failed"
    public DateTime ProcessedAt { get; private set; }

    private PaymentRecord() { }

    public PaymentRecord(Guid orderId, Guid customerId, decimal amount, string status)
    {
        Id = Uuid.NewSequential();
        OrderId = orderId;
        CustomerId = customerId;
        Amount = amount;
        Status = status;
        ProcessedAt = DateTime.UtcNow;
    }
}
```

### Handler

```csharp
// Features/ProcessPayment/ProcessPaymentHandler.cs
public static class ProcessPaymentHandler
{
    public static async Task<PaymentSuccessfulEvent> Handle(
        ProcessPaymentCommand cmd,
        PaymentDbContext ctx,
        ILogger logger,
        CancellationToken ct)
    {
        // Mock: always succeeds.
        // Real implementation: call payment provider, handle failure by returning PaymentFailedEvent.
        ctx.PaymentRecords.Add(new PaymentRecord(cmd.OrderId, cmd.CustomerId, cmd.Amount, "Approved"));
        await ctx.SaveChangesAsync(ct);

        logger.LogInformation("Payment approved for order {OrderId}, amount {Amount}", cmd.OrderId, cmd.Amount);

        return new PaymentSuccessfulEvent(cmd.OrderId);
    }
}
```

### Wolverine Configuration — PaymentService

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMq(new Uri(config["ConnectionStrings:RabbitMq"]!))
        .AutoProvisionRoutingTopology();

    opts.PersistMessagesWithPostgresql(config.GetConnectionString("Database")!, "wolverine");
    opts.Policies.AutoApplyTransactions();

    opts.OnException<Exception>().MoveToErrorQueueOnFailure();

    opts.ListenToRabbitQueue("payment-service");

    opts.PublishMessage<PaymentSuccessfulEvent>().ToRabbitQueue("order-service-events");
    opts.PublishMessage<PaymentFailedEvent>().ToRabbitQueue("order-service-events");
});

builder.Services.AddDbContext<PaymentDbContext>((sp, opts) =>
    opts.UseNpgsql(config.GetConnectionString("Database"))
        .UseWolverine(sp));
```

---

## Infrastructure

### compose.yaml

```yaml
services:

  order-service:
    build:
      context: .
      dockerfile: src/OrderService/Dockerfile
    depends_on:
      order-db: { condition: service_healthy }
      rabbitmq: { condition: service_healthy }
    environment:
      ConnectionStrings__Database: "Host=order-db;Port=5432;Database=orders;Username=app;Password=app"
      ConnectionStrings__RabbitMq: "amqp://guest:guest@rabbitmq:5672"
      ASPNETCORE_ENVIRONMENT: Development
    networks: [backend]
    labels:
      dev.orbstack.http-port: "8080"

  inventory-service:
    build:
      context: .
      dockerfile: src/InventoryService/Dockerfile
    depends_on:
      inventory-db: { condition: service_healthy }
      rabbitmq: { condition: service_healthy }
    environment:
      ConnectionStrings__Database: "Host=inventory-db;Port=5432;Database=inventory;Username=app;Password=app"
      ConnectionStrings__RabbitMq: "amqp://guest:guest@rabbitmq:5672"
      ASPNETCORE_ENVIRONMENT: Development
    networks: [backend]

  payment-service:
    build:
      context: .
      dockerfile: src/PaymentService/Dockerfile
    depends_on:
      payment-db: { condition: service_healthy }
      rabbitmq: { condition: service_healthy }
    environment:
      ConnectionStrings__Database: "Host=payment-db;Port=5432;Database=payments;Username=app;Password=app"
      ConnectionStrings__RabbitMq: "amqp://guest:guest@rabbitmq:5672"
      ASPNETCORE_ENVIRONMENT: Development
    networks: [backend]

  order-db:
    image: postgres:17-alpine
    environment: { POSTGRES_USER: app, POSTGRES_PASSWORD: app, POSTGRES_DB: orders }
    volumes: [order_pgdata:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U app -d orders"]
      interval: 5s
      timeout: 5s
      retries: 5
    networks: [backend]

  inventory-db:
    image: postgres:17-alpine
    environment: { POSTGRES_USER: app, POSTGRES_PASSWORD: app, POSTGRES_DB: inventory }
    volumes: [inventory_pgdata:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U app -d inventory"]
      interval: 5s
      timeout: 5s
      retries: 5
    networks: [backend]

  payment-db:
    image: postgres:17-alpine
    environment: { POSTGRES_USER: app, POSTGRES_PASSWORD: app, POSTGRES_DB: payments }
    volumes: [payment_pgdata:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U app -d payments"]
      interval: 5s
      timeout: 5s
      retries: 5
    networks: [backend]

  rabbitmq:
    image: rabbitmq:4-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 5s
      timeout: 5s
      retries: 5
    labels:
      dev.orbstack.http-port: "15672"
    networks: [backend]

volumes:
  order_pgdata:
  inventory_pgdata:
  payment_pgdata:

networks:
  backend:
```

### Dockerfiles

Each service needs a standard multi-stage Dockerfile:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Contracts/Contracts.csproj", "src/Contracts/"]
COPY ["src/OrderService/OrderService.csproj", "src/OrderService/"]
RUN dotnet restore "src/OrderService/OrderService.csproj"
COPY . .
RUN dotnet publish "src/OrderService/OrderService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderService.dll"]
```

Repeat for InventoryService and PaymentService, adjusting project names.

---

## Cross-Cutting Concerns (all services)

### Logging — Serilog

```csharp
// Program.cs (all services)
builder.Host.UseSerilog((ctx, config) =>
    config.ReadFrom.Configuration(ctx.Configuration));
```

```json
// appsettings.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Wolverine": "Warning"
      }
    },
    "WriteTo": [{ "Name": "Console" }],
    "Enrich": ["FromLogContext"]
  }
}
```

```json
// appsettings.Development.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```

### Metrics — OpenTelemetry + Prometheus

```csharp
// Program.cs (all services)
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .AddMeter("OrderService")      // or "InventoryService" / "PaymentService"
        .AddPrometheusExporter());

app.MapPrometheusScrapingEndpoint(); // exposes /metrics
```

**Key metrics per service:**

OrderService:
- `orders.submitted` — counter, incremented in SubmitOrderEndpoint
- `orders.completed` — counter, incremented in saga `Handle(InventoryFulfilledEvent)`
- `orders.rejected` — counter, incremented in saga failure handlers
- `orders.timed_out` — counter, incremented in saga `Handle(OrderSagaTimeout)`

InventoryService:
- `inventory.reservations.succeeded`
- `inventory.reservations.failed`

PaymentService:
- `payments.processed`

### Exception Middleware

```csharp
// Infrastructure/Http/GlobalExceptionHandler.cs (all services)
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        logger.LogError(ex, "Unhandled exception on {Method} {Path}",
            ctx.Request.Method, ctx.Request.Path);

        var status = ex switch
        {
            ArgumentException         => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status422UnprocessableEntity,
            _                         => StatusCodes.Status500InternalServerError
        };

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = ReasonPhrase(status) }, ct);
        return true;
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad request", 422 => "Unprocessable request", _ => "Internal server error"
    };
}

// Registration in Program.cs:
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// ...
app.UseExceptionHandler();
```

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddNpgsql(config.GetConnectionString("Database")!);

app.MapHealthChecks("/health");
```

### Endpoint Discovery Pattern

Use reflection-based endpoint registration so each feature registers itself:

```csharp
// Infrastructure/Http/IEndpoint.cs
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}

// Infrastructure/Http/EndpointExtensions.cs
public static class EndpointExtensions
{
    public static void MapEndpoints(this WebApplication app)
    {
        var endpoints = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(Activator.CreateInstance)
            .Cast<IEndpoint>();

        foreach (var endpoint in endpoints)
            endpoint.Map(app);
    }
}

// In Program.cs:
app.MapEndpoints();
```

### Migrations on Startup

```csharp
// Infrastructure/Persistence/PersistenceExtensions.cs
public static void ApplyMigrations(this WebApplication app)
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<TDbContext>().Database.Migrate();
}

// In Program.cs:
if (app.Environment.IsDevelopment())
    app.ApplyMigrations();
```

**Important:** Wolverine also creates its own schema tables (`wolverine` schema) on startup automatically — no manual migration needed for Wolverine's tables.

### Rate Limiting (OrderService only — submit endpoint)

```csharp
// In-memory rate limiter (replace with Redis-backed for multi-instance)
builder.Services.AddRateLimiter(opts =>
{
    opts.AddPolicy("per-customer", ctx =>
    {
        var customerId = ctx.Request.RouteValues["customerId"]?.ToString()
            ?? ctx.Request.Headers["X-Customer-Id"].FirstOrDefault()
            ?? "anonymous";

        return RateLimitPartition.GetSlidingWindowLimiter(customerId,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit      = 10,
                Window           = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4
            });
    });
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Apply to the submit endpoint:
app.MapPost("/api/v1/orders", ...).RequireRateLimiting("per-customer");
```

**Note:** For multi-instance production, replace with `AspNetCoreRateLimiting` + `StackExchange.Redis` or a custom Redis-backed partition.

---

## How Wolverine Replaces Custom Code

| Custom implementation | Wolverine equivalent |
|---|---|
| `RabbitMqPublisher` (manual channel management) | `IMessageBus.PublishAsync` / `SendAsync` |
| `RabbitMqConsumerService` (BackgroundService + channel) | `ListenToRabbitQueue` + handler discovery |
| `OutboxMessage` table + `OutboxProcessor` + `OutboxChannel` signal | `PersistMessagesWithPostgresql` + `AutoApplyTransactions` |
| `InboxMessage` table + `INSERT ... ON CONFLICT DO NOTHING` | Built into Wolverine envelope store (automatic deduplication) |
| `MessageDispatcher` (manual switch/case routing) | Handler discovery by convention + message type routing |
| `RabbitMqInitializer` (queue/exchange declaration) | `AutoProvisionRoutingTopology()` |
| Manual retry loop + dead-letter queue | `RetryWithCooldown` + `MoveToErrorQueueOnFailure` |
| Saga state machine (manual) | `Wolverine.Saga` base class |

---

## Key Design Decisions

### Optimistic vs. Pessimistic Locking

The previous version used `SELECT ... FOR UPDATE` (pessimistic locking). This serializes all transactions touching the same SKU — under high load, every order queues behind the previous one.

The new version uses **optimistic concurrency** (`xmin`):
- No locks held during the handler execution
- On conflict: Wolverine retries the handler (re-fetches fresh data, re-checks availability)
- Conflict only occurs when two transactions modify the **same row simultaneously**
- Under normal load this never happens; under extreme load retries are bounded

This gives significantly higher throughput when the conflict rate is low (which it is for most real-world order patterns).

### Saga Idempotency

All saga handlers must be safe to run multiple times. Wolverine's inbox deduplication (via envelope store) prevents duplicate delivery, but if a handler commits its DB changes and then crashes before acking the message, Wolverine will redeliver. Handlers must be idempotent:

- `ReserveInventoryHandler`: checks `AnyAsync(r => r.OrderId == cmd.OrderId)` before acting
- `ReleaseInventoryHandler`: if no reservations found → returns success (already released)
- `FulfillInventoryHandler`: if no reservations found → returns success (already fulfilled)
- `ProcessPaymentHandler`: consider a unique index on `PaymentRecord.OrderId` + check before inserting

### No Duplicate SKUs

`SubmitOrderRequestValidator` rejects duplicate SKUs in the request. Handlers do not need to group/merge — items arrive already de-duplicated.

### Database per Service

Each service has complete ownership of its data. No shared tables. Cross-service queries are prohibited. The only communication channel is RabbitMQ messages.

### Saga as Read Model

`OrderSaga` state is stored in the DB by Wolverine and is queryable. The `GET /api/v1/orders/{id}` endpoint reads directly from the saga state table. A separate `Orders` read table is not required (but can be added if richer query capabilities are needed, e.g., list all orders by customer with filtering).

---

## Assumptions

- Payment is mocked — always succeeds. A real implementation returns `PaymentFailedEvent` on failure, which the saga handles by releasing the reservation and marking the order rejected.
- No authentication — assumed to be handled at the API gateway level.
- Rate limiting is in-memory — for multi-instance production, back it with Redis.
- Wolverine manages its own `wolverine` schema tables (outbox, inbox envelopes, saga state, scheduled messages) — do not manually create or migrate these.
- `xmin` concurrency token requires no migration — it is a PostgreSQL system column present on every row automatically.
- Migrations (`db.Database.Migrate()`) run on startup in Development. In production, run migrations as a separate step before deploying.
- Inventory is seeded via `POST /api/v1/inventories` on InventoryService, or via the batch endpoints on OrderService (which call InventoryService internally or publish an `AddInventoryCommand`).
- `InboxMessages` / outbox cleanup is handled by Wolverine automatically (it expires processed envelopes).
- Saga timeout (5 minutes) covers the case where downstream services are unreachable and orders get stuck in processing. In production, the timeout handler should also publish `ReleaseInventoryCommand` if the saga was past the `ReservingInventory` step.

---

## Links

- Wolverine docs: https://wolverine.netlify.app
- Wolverine RabbitMQ: https://wolverine.netlify.app/guide/messaging/transports/rabbitmq
- Wolverine Sagas: https://wolverine.netlify.app/guide/durability/sagas
- Wolverine EF Core: https://wolverine.netlify.app/guide/persistence/efcore
- Wolverine error handling: https://wolverine.netlify.app/guide/messaging/error-handling
