# OrderProcessing

A .NET 8 order processing service built around the transactional outbox pattern and an event-driven saga.

## How to run

```bash
docker compose up
```

Docker Compose starts PostgreSQL, RabbitMQ, and the service. Migrations run automatically on startup.

**localhost**
- Swagger UI: http://localhost:8080/swagger
- RabbitMQ management: http://localhost:15672 (guest / guest)
- Prometheus metrics: http://localhost:8080/metrics
- Health check: http://localhost:8080/health

**OrbStack**
- Swagger UI: http://order-service.orderprocessing.orb.local/swagger
- RabbitMQ management: http://rabbitmq.orderprocessing.orb.local (guest / guest)
- Prometheus metrics: http://order-service.orderprocessing.orb.local/metrics
- Health check: http://order-service.orderprocessing.orb.local/health

## Testing using endpoints

Option 1 - batch endpoint:
- `POST /api/v1/orders/batch` - creates N orders

Option 2 - classic flow:
- `POST /api/v1/inventories` - seed inventory for a given SKU
- `POST /api/v1/orders` - submit an order with a pre-seeded SKU
- `GET /api/v1/orders/{orderId}` - poll order status

## What it does

An order goes through this saga:

```
OrderSubmitted → ReserveInventory → InitiatePayment → ProcessPayment → FulfillOrder
                       ↓ (failure)
                 RejectOrder
```

Each step is triggered by a RabbitMQ message. Each handler publishes the next message via the outbox, so every transition is atomic with its DB write.

## Design decisions

**Transactional outbox.** Instead of publishing to RabbitMQ directly inside a handler, the message is written to an `OutboxMessages` table in the same transaction as the business data. A `BackgroundService` picks it up and publishes after commit. This guarantees no message is lost if the process crashes between a DB write and a publish.

**Wake channel instead of polling.** The outbox processor sleeps on a `Channel<bool>` and wakes immediately when new rows are committed. A 30-second fallback poll catches anything that survives a process restart before the signal fires.

**Pessimistic locking with sorted keys.** `ReserveInventory` locks inventory rows with `SELECT ... FOR UPDATE` ordered alphabetically by SKU. The consistent ordering is what prevents deadlocks - every concurrent transaction acquires locks in the same sequence, so they queue rather than deadlock each other.

**All-or-nothing reservation.** All items in an order are validated and reserved in a single transaction. Partial reservations are never committed. If any SKU has fewer items in stock than requested, the whole order is rejected.

**Consumer idempotency.** Before a handler runs, its `MessageId` is inserted into an `InboxMessages` table using `INSERT ... ON CONFLICT DO NOTHING`. If the row already exists the message is silently skipped. If the handler fails, the record is removed so the message can be retried. This covers RabbitMQ's at-least-once delivery guarantee.

**Dead-letter queues.** Messages that fail processing 3 times are routed to a `{queue}.dead` queue and not requeued. Requires manual intervention.

**Structured logging and metrics.** Every saga event is logged with structured properties via Serilog. Fulfilled order count is exposed as a Prometheus counter (`orders_processed_total`) at `/metrics`.

**Vertical slice architecture.** Each feature owns its handler, endpoint, and contracts. No shared application layer.

## Assumptions

- Payment is mocked - it always succeeds. A real implementation would integrate a payment provider and handle `PaymentFailed` by releasing the reservation and rejecting the order.
- A single service handles the full saga. In production this would be split into an `InventoryService`, `InventoryService`, `OrderService` etc.
- No authentication on endpoints - assumed to be handled at the gateway level.
- Inventory is seeded manually via `POST /api/v1/inventories` or through the batch endpoints.
- No rate limiting on order submission. In production, a per-customer sliding window persisted in Redis is a way to go.
