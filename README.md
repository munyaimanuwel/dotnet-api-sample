# dotnet-api-sample

A small **ASP.NET Core** API that demonstrates clean architecture, tests, and the reliability patterns.

**Status:** in progress — clean architecture wired, Products slice verified end-to-end against PostgreSQL (Dapper), RabbitMQ publish/consume with manual ack and a retry→DLQ ladder verified live and by tests, 17 tests green, and CI running on PRs and `main`. See checklist below.  

---

## Goals (what "done" looks like)

- [x] ASP.NET Core Web API (.NET 8+)
- [x] Clean-ish layout: `Api` / `Application` / `Domain` / `Infrastructure` (or equivalent)
- [x] PostgreSQL persistence (Dapper; schema in `db/init.sql`, applied by hand)
- [x] RabbitMQ messaging demo: publish + consume with **manual ack**, backoff retries, and **DLQ/DLX** (document the topology)
- [x] xUnit: unit tests + at least one integration test path
- [x] GitHub Actions: restore → build → test on PR / `main`
- [x] README: architecture sketch, how to run locally, what each pattern shows _(sketch + local run added; per-pattern write-ups pending)_

---

## Layout

```text
src/
  DotnetApiSample.Api/            # controllers + composition root
  DotnetApiSample.Application/    # ports (interfaces) + request DTOs
  DotnetApiSample.Domain/         # entities
  DotnetApiSample.Infrastructure/ # Dapper repositories, Npgsql + RabbitMQ wiring
  DotnetApiSample.Worker/         # hosts the product.created consumer
tests/
  DotnetApiSample.UnitTests/
  DotnetApiSample.IntegrationTests/
db/
  init.sql                        # hand-applied schema
.github/
  workflows/
    ci.yml                        # restore -> build -> test
.env.example
.gitignore
LICENSE
README.md
DotnetApiSample.slnx
```

---

## Architecture

One vertical slice, with dependencies pointing inward:

```text
ProductsController (Api)
  -> IProductRepository            (Application port)
       -> DapperProductRepository  (Infrastructure)
            -> NpgsqlConnectionFactory -> PostgreSQL
```

- **Domain** — entities only, no dependencies.
- **Application** — ports and request DTOs; references only `Domain`, and its ports use BCL types (`IDbConnection`), so no data-access library leaks inward.
- **Infrastructure** — Dapper + Npgsql implementations; exposes `AddInfrastructure`.
- **Api** — composition root: controllers and DI wiring.

---

## Messaging

`POST /api/products` publishes `product.created`; the Worker consumes it. The topology is declared in code (`RabbitMqTopology`), not by hand:

```text
                          routing key: product.created
  Api ──publish──▶ [ products exchange (topic) ] ──▶ products.created (queue)
                                                       │ x-dead-letter-exchange = products.dlx
                                                       │ x-dead-letter-routing-key = product.created
                                       Worker consumes │ (prefetch 1, MANUAL ack)
                                                       │
              ┌────────────────────────────────────────┴──────────────────────────────────┐
              │ success                          failure, attempt < 3          failure, attempt = 3
              ▼                                  ▼                                ▼
          basicAck              publish → [ products.retry ]            nack(requeue: false)
                                    rk: retry.{n}                                │
                                        │                                        ▼
                                        ▼                            products.created.dlq
                          products.created.retry.{1..3}
                            x-message-ttl = 5s / 15s / 45s
                            x-dead-letter-exchange = products     ← returns to the main queue
```

- **Manual ack** — a message is acknowledged only once the handler succeeds. On failure the consumer re-publishes to the retry exchange (carrying an `attempt` header) and acks; the retry queue's TTL provides the delay before dead-lettering it back onto the main queue.
- **Backoff ladder** — 3 attempts at 5s / 15s / 45s (`RabbitMq:RetryDelaysSeconds`). Beyond that the message is nacked without requeue and the main queue's DLX moves it to `products.created.dlq`.
- **What the consumer does** — writes a row to `product_audit`, so the effect is observable rather than log-only.
- **Delivery semantics: at-least-once.** Events carry an `EventId` for idempotency. Publishing happens after the database commit and there is **no outbox**, so a crash in between loses the event — a real system would publish transactionally.
- **Split responsibilities** — the Api publishes and declares only the exchange; the Worker declares the full graph and consumes, so the producer can never fight the consumer over queue arguments.

---

## API

| Method | Route | Result |
|--------|-------|--------|
| GET | `/api/products` | 200 — list |
| GET | `/api/products/{id}` | 200 / 404 |
| POST | `/api/products` | 201 + `Location` |
| PUT | `/api/products/{id}` | 204 / 404 |
| DELETE | `/api/products/{id}` | 204 / 404 |

---

## Local run

Requires the .NET 10 SDK plus a reachable PostgreSQL and RabbitMQ. Bring your own infrastructure — no compose file ships with this repo.

```bash
# 1. Postgres: create the database and apply the schema by hand
psql -h localhost -U postgres -c "CREATE DATABASE dotnet_api_sample;"
psql -h localhost -U postgres -d dotnet_api_sample -f db/init.sql

# 2. RabbitMQ (management UI on http://localhost:15672)
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# 3. Keep credentials out of the repo (user-secrets)
dotnet user-secrets set "ConnectionStrings:Postgres" \
  "Host=localhost;Port=5432;Database=dotnet_api_sample;Username=postgres;Password=<your-password>" \
  --project src/DotnetApiSample.Api
dotnet user-secrets set "RabbitMq:Password" "guest" --project src/DotnetApiSample.Api

dotnet user-secrets set "ConnectionStrings:Postgres" \
  "Host=localhost;Port=5432;Database=dotnet_api_sample;Username=postgres;Password=<your-password>" \
  --project src/DotnetApiSample.Worker
dotnet user-secrets set "RabbitMq:Password" "guest" --project src/DotnetApiSample.Worker
# ...or set the environment variables ConnectionStrings__Postgres / RabbitMq__Password (see .env.example)

# 4. Run the API and the Worker (separate terminals)
dotnet run --project src/DotnetApiSample.Api --launch-profile http
dotnet run --project src/DotnetApiSample.Worker

# 5. Exercise the Products CRUD (also in src/DotnetApiSample.Api/DotnetApiSample.Api.http)
curl http://localhost:5291/api/products
curl -X POST http://localhost:5291/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Pour-over Set","sku":"POV-004","price":32.00}'
# the Worker consumes product.created and writes a row to product_audit

# build / test
dotnet build
dotnet test
```

Schema changes are **not** migrated automatically — edit `db/init.sql` and re-apply it by hand.

The connection string is read from configuration key `ConnectionStrings:Postgres`. Locally it comes from **user-secrets**; in other environments set the `ConnectionStrings__Postgres` environment variable. Never commit real secrets.

---

## Tests

```bash
dotnet test
```

- **UnitTests** (9) — `ProductsController` behaviour (200/404/201/204 mapping, `CreatedAt` routing, and the event it publishes) against hand-rolled fake ports. No Docker needed.
- **IntegrationTests** (8) — full HTTP round-trip through `WebApplicationFactory<Program>` against ephemeral **Testcontainers** PostgreSQL **and RabbitMQ**, with the schema applied from `db/init.sql`. Covers CRUD plus messaging: the event is published, the consumer writes its audit row, and a poison message retries then reaches the DLQ. **Requires Docker**; pulls `postgres:17-alpine` and `rabbitmq:3.13-alpine` on first run.

The fixture pins the environment to `Testing` and injects connection settings as host settings, so local `appsettings`/user-secrets never leak into a test run. Tests re-seed `products`, drain the queues, and run the retry ladder at 1-second delays so the DLQ path finishes in seconds — and because everything is ephemeral on random ports, it never touches your local Postgres or broker.

---

## CI

`.github/workflows/ci.yml` runs on pull requests and pushes to `main`: restore → build (Release) → test, on `ubuntu-latest` with the .NET 10 SDK.

- **No secrets required** — the integration tests start their own PostgreSQL and RabbitMQ through Testcontainers, using the Docker daemon that GitHub-hosted runners provide.
- **Least privilege** — `permissions: contents: read`; no write token, so forked PRs are safe.
- **Cost controls** — a single OS (no matrix), `concurrency` cancels superseded runs on the same ref, a 15-minute job timeout, NuGet caching keyed on the csproj files, and `.trx` results uploaded only on failure.
- **No duplicate runs** — `push` is limited to `main`, so a feature branch is tested once via its PR rather than on every push.

---
