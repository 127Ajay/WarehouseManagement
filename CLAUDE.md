# CLAUDE.md

Guidance for Claude Code (and any AI assistant) working in this repository.

## What this is

A multi-tenant Warehouse Management System (WMS) SaaS. First market: Indian 3PLs (pharma and FMCG focus). Built solo by the product owner with AI help, following an Agile multi-role process.

Full context lives in `docs/`. Read the relevant doc before changing behaviour:

| Doc | Use it for |
|---|---|
| `docs/01-business-requirements.md` | BR-xxx and NFR-xxx requirement IDs, scope, decisions D-01 to D-10 |
| `docs/02-prd.md` | Release plan (1.0 / 1.1 / 1.2), MoSCoW, Definition of Ready and Done |
| `docs/03-functional-spec.md` | FR-xxx rules, state machines, permission/flag/config catalogues (appendices A to E) |
| `docs/04-user-stories-backlog.md` | User stories with Given/When/Then acceptance criteria |
| `docs/05-architecture-hld.md` | Module map, ADRs, tenancy design |
| `docs/06-architecture-lld.md` | Solution layout, schemas, contracts, allocation algorithm, sequences |
| `docs/07-architecture-infra-security.md` | AWS, security, CI/CD, DR, NFR verification |
| `docs/08-tech-lead-sprint-01-plan.md` | **Active plan**: solo sprint map, Sprint 1 task breakdown |
| `docs/decisions.md` | Implementation decisions (TQ-xx). **Overrides** the HLD/LLD where they differ. |

## Process (always follow)

Roles, in order. Stay in the current role until the product owner says to move on.

1. Business Agent: requirements (done)
2. Product Owner / BA: PRD, functional spec, stories (done)
3. Solution Architect: HLD, LLD, infra (done)
4. **Tech Lead + Developers: current phase.** Technical analysis, clarifications, implementation, story by story.
5. QA: test plans, test cases, testing (later)

Work one story at a time from the solo sprint map in `docs/08-tech-lead-sprint-01-plan.md`. Ask clarifying questions when a story is ambiguous. Record any design change in `docs/decisions.md` with the next TQ number.

## Current status

- Phase 4, solo Sprint 1.
- **EN-01 done** on branch `feat/EN-01-solution-skeleton` (skeleton, building blocks, Platform sample module, CI, local stack).
- **Next: US-001 Create a tenant**, then US-002, then US-006. Task breakdown in doc 08 section 5.
- Not yet compiled end to end: projects with NuGet packages were written without package access. First job on a machine with internet: `dotnet build` and fix anything that breaks.

## Commands

```bash
docker compose up -d                                        # PostgreSQL 17 + Valkey 8
dotnet run --project src/Hosts/Wms.Host.Migrator            # apply SQL migrations (as wms_owner)
dotnet watch --project src/Hosts/Wms.Host.AppApi            # API on http://localhost:5080 with hot reload
dotnet build WarehouseManagement.slnx                       # warnings are errors
dotnet test WarehouseManagement.slnx                        # unit + architecture tests
./scripts/dev.sh   (or scripts/dev.ps1 on Windows)          # all of the above in one go
```

Smoke checks: `curl http://localhost:5080/health/ready` and `curl http://localhost:5080/api/system/status`.

## Architecture rules (enforced by tests, do not break)

- Modular monolith. Each module: `Domain`, `Application`, `Infrastructure`, `Contracts`, `Endpoints` under `src/Modules/<Module>/`.
- A module references another module **only** through its `Contracts` project. Rules live in `tests/Wms.ArchitectureTests/ReferenceRules.cs`.
- **Inventory is the only module that writes stock.** Stock row and ledger event in the same transaction. Ledger is append-only.
- Tenant isolation in three layers: `ITenantContext` in handlers, EF global filter (`TenantDbContext`), PostgreSQL RLS on every tenant table. Every new tenant table needs an RLS policy using `NULLIF(current_setting('app.tenant_id', true), '')::uuid` and a tenant isolation test.
- Commands go through `ICommandHandler<TCommand, TResult>`; queries through `IQueryHandler<TQuery, TResult>`. Decorators handle validation, transaction, logging. Handlers never open or commit transactions.
- Business failures return `Result<T>` with an `Error` code (`MODULE_RULE_NAME`). Exceptions are for bugs only.
- Time from `IClock`, IDs from `Ids.New()` (UUID v7). `DateTime.Now` and `Guid.NewGuid()` fail the build's architecture tests.
- Migrations are SQL files `Migrations/V0001__name.sql` embedded in the module's Infrastructure project. Never edit an applied migration; add a new one. Register new modules in `src/Hosts/Wms.Host.Migrator/Program.cs`.
- Every endpoint checks feature flag, then permission, then data scope (FR-GEN-07).
- New packages: add to `Directory.Packages.props` and check the licence. Do not use MediatR, AutoMapper, MassTransit v9+, FluentAssertions v8+.

## Definition of Done (per story)

From PRD section 10: code reviewed; CI green; unit tests on Domain and Application (80%+ new code); integration test per new endpoint; tenant isolation test for new tenant data; flag and permission wired; audit/ledger events where required; acceptance criteria verified; API docs updated.

## Git

- Trunk-based. Short branches: `feat/US-001-create-tenant`. Squash merge. Conventional commit titles: `feat(platform): create tenant (US-001)`.
- One story per branch and PR. Fill in the PR template checklist.

## Writing style (docs, commit messages, PR text)

Plain, human-sounding English. Short sentences. No em dashes. No hype words. Concrete and quantified over generic.
