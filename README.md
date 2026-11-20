# Warehouse Management SaaS

Multi-tenant, modular WMS for 3PLs and in-house warehouses. .NET 10, Clean Architecture per module, PostgreSQL with row-level security.

Design documents are in `docs/` (BRD, PRD, functional spec, backlog, HLD, LLD, infrastructure, sprint plan, decision log). `CLAUDE.md` tells AI assistants how to work in this repo.

## Repository layout

```
src/
  BuildingBlocks/        SharedKernel, Application.Abstractions, Infrastructure.Common, Web.Common
  Modules/<Module>/      Domain, Application, Infrastructure, Contracts, Endpoints
  Hosts/                 AppApi (/api/app), Migrator (SQL migrations)
tests/                   Unit and architecture tests
db/init/                 Local database roles (dev only)
```

Module rules (enforced by `tests/Wms.ArchitectureTests`):

| Project | May reference |
|---|---|
| `*.Domain` | SharedKernel |
| `*.Contracts` | SharedKernel |
| `*.Application` | own Domain, Application.Abstractions, any module's Contracts |
| `*.Infrastructure` | own Domain + Application, Infrastructure.Common, any module's Contracts |
| `*.Endpoints` | own Application + Contracts + Infrastructure (composition), Web.Common |
| `Wms.Host.*` | anything |

## Run locally

Prerequisites: .NET 10 SDK, Docker.

One command (starts containers, migrates, runs the API with hot reload):

```bash
./scripts/dev.sh            # macOS / Linux / WSL
.\scripts\dev.ps1           # Windows PowerShell
```

Or step by step:

```bash
docker compose up -d                                   # PostgreSQL 17 + Valkey 8
dotnet run --project src/Hosts/Wms.Host.Migrator       # apply SQL migrations as wms_owner
dotnet watch --project src/Hosts/Wms.Host.AppApi       # http://localhost:5080, reloads on save
```

Check it:

```bash
curl http://localhost:5080/health/ready
curl http://localhost:5080/api/system/status
```

`/api/system/status` runs through the full query pipeline and shows the database user (`wms_app`) and the tenant session (null until tenant resolution lands in US-002).

## Database roles

| Role | Use | Row-level security |
|---|---|---|
| `wms_owner` | Migrations only | Owner |
| `wms_app` | All runtime hosts | Enforced (`NOBYPASSRLS`) |
| `wms_platform_jobs` | Outbox dispatcher and audited cross-tenant jobs | Bypassed |

Every connection sets `app.tenant_id`; every tenant table has a policy on it. A missing filter in code returns zero rows, not another tenant's data.

## Migrations

SQL-first, per module: `Migrations/V0001__description.sql` embedded in the module's Infrastructure project. Applied scripts are checksummed; never edit an applied script, add a new one.

## Conventions

- Business failures return `Result<T>` with an `Error` code (`MOD_SOME_RULE`); exceptions are for bugs.
- Time comes from `IClock`; IDs from `Ids.New()` (UUID v7). Both enforced by tests.
- Commands run in one transaction across all modules they touch (`IUnitOfWork`); queries never open a write transaction.
- Trunk-based: short branches `feat/US-xxx-name`, squash merge, conventional commit titles.
