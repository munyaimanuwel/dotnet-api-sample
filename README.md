# dotnet-api-sample

A small **ASP.NET Core** API that demonstrates clean architecture, tests, and the reliability patterns.

**Status:** in progress — skeleton builds; clean-architecture layout and project references are wired. See checklist below.  
**Visibility:** private until scrubbed and demo-ready, then publicize.


---

## Goals (what "done" looks like)

- [x] ASP.NET Core Web API (.NET 8+)
- [x] Clean-ish layout: `Api` / `Application` / `Domain` / `Infrastructure` (or equivalent)
- [ ] PostgreSQL persistence (EF Core or Dapper — pick one and stick to it)
- [ ] RabbitMQ messaging demo: publish + consume with **manual ack**, backoff retries, and **DLQ/DLX** (document the topology)
- [ ] xUnit: unit tests + at least one integration test path _(test projects scaffolded, no tests yet)_
- [ ] GitHub Actions: restore → build → test on PR / `main`
- [ ] Optional: Prometheus metrics endpoint (wire to `observability-demo` later)
- [ ] README: architecture sketch, how to run locally, what each pattern shows

---

## Suggested layout

```text
src/
  DotnetApiSample.Api/
  DotnetApiSample.Application/
  DotnetApiSample.Domain/
  DotnetApiSample.Infrastructure/
tests/
  DotnetApiSample.UnitTests/
  DotnetApiSample.IntegrationTests/
.github/workflows/ci.yml
docker-compose.yml          # local Postgres + RabbitMQ only
.gitignore
LICENSE
README.md
```

---

## Local run (fill in as you build)

```bash
# requires .NET 10 SDK
dotnet restore
dotnet build
dotnet test
# API: dotnet run --project src/DotnetApiSample.Api
```

Use `.env.example` for connection strings — never commit real secrets.

---