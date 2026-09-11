# dotnet-api-sample

Portfolio sample: a small **ASP.NET Core** API that demonstrates clean architecture, tests, and the reliability patterns you claim on a Backend/DevOps CV.

**Status:** scaffold only — build from this README.  
**Visibility:** private until scrubbed and demo-ready, then publicize.

Not affiliated with any employer. No real customer data.

---

## Goals (what "done" looks like)

- [ ] ASP.NET Core Web API (.NET 8+)
- [ ] Clean-ish layout: `Api` / `Application` / `Domain` / `Infrastructure` (or equivalent)
- [ ] PostgreSQL persistence (EF Core or Dapper — pick one and stick to it)
- [ ] RabbitMQ messaging demo: publish + consume with **manual ack**, backoff retries, and **DLQ/DLX** (document the topology)
- [ ] xUnit: unit tests + at least one integration test path
- [ ] GitHub Actions: restore → build → test on PR / `main`
- [ ] Optional: Prometheus metrics endpoint (wire to `observability-demo` later)
- [ ] README: architecture sketch, how to run locally, what each pattern shows

**Soft-cut:** do not claim production Docker/K8s ownership here. Compose for local Postgres/RabbitMQ is fine if you mark it as local-dev only.

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
# requires .NET 8 SDK
dotnet restore
dotnet build
dotnet test
# API: dotnet run --project src/DotnetApiSample.Api
```

Use `.env.example` for connection strings — never commit real secrets.

---

## CV mapping

| Skill on CV | Show it here |
|-------------|--------------|
| C# / ASP.NET Core / REST | Controllers or minimal APIs + clear contracts |
| PostgreSQL | Migrations + repository/query layer |
| RabbitMQ | Producer/consumer + DLQ story in README |
| xUnit | Unit + integration |
| CI/CD (GitHub Actions) | Green `ci.yml` |

---

## Before public

- [ ] No secrets in history
- [ ] MIT (or chosen) LICENSE
- [ ] Topics: `dotnet`, `aspnetcore`, `postgresql`, `rabbitmq`, `xunit`
- [ ] Pin only after StackContract; this is the #2 Backend pin

---

## Out of scope

Full-stack UI, Talend, Angular, Kubernetes-mandatory demos, phone-home telemetry.
