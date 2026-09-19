# dotnet-api-sample

A small **ASP.NET Core** API that demonstrates clean architecture, tests, and the reliability patterns.

**Status:** in progress — clean architecture wired, and the Products slice is verified end-to-end against PostgreSQL (Dapper + Npgsql). See checklist below.  

---

## Goals (what "done" looks like)

- [x] ASP.NET Core Web API (.NET 8+)
- [x] Clean-ish layout: `Api` / `Application` / `Domain` / `Infrastructure` (or equivalent)
- [x] PostgreSQL persistence (Dapper; schema in `db/init.sql`, applied by hand)
- [ ] RabbitMQ messaging demo: publish + consume with **manual ack**, backoff retries, and **DLQ/DLX** (document the topology)
- [ ] xUnit: unit tests + at least one integration test path _(test projects scaffolded, no tests yet)_
- [ ] GitHub Actions: restore → build → test on PR / `main`
- [ ] Optional: Prometheus metrics endpoint (wire to `observability-demo` later)
- [ ] README: architecture sketch, how to run locally, what each pattern shows _(sketch + local run added; per-pattern write-ups pending)_

---

## Layout

```text
src/
  DotnetApiSample.Api/            # controllers + composition root
  DotnetApiSample.Application/    # ports (interfaces) + request DTOs
  DotnetApiSample.Domain/         # entities
  DotnetApiSample.Infrastructure/ # Dapper repositories, Npgsql wiring
tests/
  DotnetApiSample.UnitTests/
  DotnetApiSample.IntegrationTests/
db/
  init.sql                        # hand-applied schema
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

Requires the .NET 10 SDK and a reachable PostgreSQL. Bring your own Postgres — no compose file ships with this repo.

```bash
# 1. Create the database and apply the schema by hand
psql -h localhost -U postgres -c "CREATE DATABASE dotnet_api_sample;"
psql -h localhost -U postgres -d dotnet_api_sample -f db/init.sql

# 2. Keep the connection string out of the repo (user-secrets)
dotnet user-secrets set "ConnectionStrings:Postgres" \
  "Host=localhost;Port=5432;Database=dotnet_api_sample;Username=postgres;Password=<your-password>" \
  --project src/DotnetApiSample.Api
# ...or set the environment variable ConnectionStrings__Postgres (see .env.example)

# 3. Run the API
dotnet run --project src/DotnetApiSample.Api --launch-profile http

# 4. Exercise the Products CRUD (also in src/DotnetApiSample.Api/DotnetApiSample.Api.http)
curl http://localhost:5291/api/products
curl -X POST http://localhost:5291/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Pour-over Set","sku":"POV-004","price":32.00}'

# build / test
dotnet build
dotnet test
```

Schema changes are **not** migrated automatically — edit `db/init.sql` and re-apply it by hand.

The connection string is read from configuration key `ConnectionStrings:Postgres`. Locally it comes from **user-secrets**; in other environments set the `ConnectionStrings__Postgres` environment variable. Never commit real secrets.

---
