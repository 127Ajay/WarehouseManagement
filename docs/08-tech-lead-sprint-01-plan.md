# WMS SaaS: Tech Lead Plan, Solo Re-plan and Sprint 1 Breakdown

| Field | Value |
|---|---|
| Version | 1.1 |
| Date | 2026-11-21 |
| Phase | 4. Tech Lead + Developers, step 1 |
| Inputs | Docs 01 to 07; answers from product owner on 2026-11-21 |
| Status | Solo re-plan **approved**. EN-01 done (branch `feat/EN-01-solution-skeleton`). Next: US-001. |

## 0. Inputs That Change the Plan

| Question | Answer | Impact |
|---|---|---|
| Where does code live? | GitHub repository `127Ajay/WarehouseManagement` | Trunk-based flow with PRs, GitHub Actions CI as designed |
| Who builds it? | **Solo, with AI help** | Velocity re-planned from 30 to 35 points to **~20 points per sprint** |
| Runtime for Sprint 1 | **Local-first** | Docker Compose for dev; AWS and Terraform deferred; adapters for bus and file store |

---

## 1. Solo Re-plan (approved)

### 1.1 Numbers
- Release 1.0 backlog: 394 points.
- Solo, AI-assisted velocity assumption: **20 points per 2-week sprint**. AI writes most boilerplate, tests, and migrations; one person still reviews, integrates, and tests on devices. Sprint 1 actuals will reset this number.
- 394 / 20 = about **20 sprints (~40 weeks)** for Release 1.0.

### 1.2 Trims to Release 1.0 (moved to 1.1)

| Item | Points | Why it can wait |
|---|---|---|
| US-066 SFTP channel | 5 | Pilot can use API or Excel import |
| US-082 Onboarding wizard | 5 | Individual imports already exist; wizard is polish |
| US-081 Notifications: email part | 1 | Keep in-app only for pilot |
| **Total trimmed** | **11** | |

Release 1.0 becomes **383 points, about 19 to 20 solo sprints**. Starting the week of 30 Nov 2026, the pilot-ready build lands around **August to September 2027**.

If that is too late, the next cut options (in order) are: batch picking (US-053, 5), e-way bill export (US-064, 5; manual on government portal), Shiprocket (US-060, 8; manual AWB entry).

### 1.3 Solo sprint map (Release 1.0)

| Solo sprint | Contents (original backlog) | Points |
|---|---|---|
| S1 | EN-01, US-001, 002, 006 | 21 |
| S2 | US-003, 004, 005, 007, 016, 011 | 16 + login infra |
| S3 | US-008, 009, 010, 012, 013 | 19 |
| S4 | US-014, 015, 017, 018, 020 | 17 |
| S5 | US-019, 021, 022, 023, 024 | 19 |
| S6 | US-025, EN-02, US-026, 031, 044 | 20 |
| S7 | US-027, 028, 029, 032 + **AWS dev environment, SP-01 spike** | 18 + infra |
| S8 | EN-03, US-033, 034, 035 | 19 |
| S9 | US-036, 037, 038, 039 | 17 |
| S10 | EN-04, US-040, 041, 042 | 18 |
| S11 | US-043, 045, 046, 047, 059 | 18 |
| S12 | US-048, 049, 050 | 14 + buffer |
| S13 | US-051, 052, 053, 054 | 17 |
| S14 | US-055, 056, 057 | 18 |
| S15 | US-058, 060, 061, 062 | 15 |
| S16 | US-063, 064, 065, 067 | 18 |
| S17 | US-068, 069, 070, 071 | 20 |
| S18 | US-072, 073, 074, EN-05 | 18 |
| S19 | US-075, 076, 077, 078, 079 | 21 |
| S20 | US-080, 081, EN-06, 07, 08, 09, 10 (staging, pen test, UAT) | 26 (partly vendor and pilot time) |

Note: SP-01 (latency spike) moves to S7 because it needs a cloud endpoint. It must still happen before the handheld scan flows (S9) are built.

---

## 2. Technical Clarifications (resolved by Tech Lead)

| # | Question | Resolution |
|---|---|---|
| TQ-01 | US-001 sends the first Tenant Admin invite, but invites are US-003 (S2). | S1 seeds the Tenant Admin user record as Invited. Email sending lands with US-003 in S2. AC1 is verified in two parts. |
| TQ-02 | US-002 AC2 (token tenant mismatch) needs tokens, which arrive in S2. | S1 builds the resolution middleware and the mismatch check against a claims principal. S1 tests use a test auth handler. Real tokens plug in during S2. |
| TQ-03 | How is the platform admin API protected in S1? | Dev-only API key on `/api/platform/*`, disabled outside Development. Replaced by platform admin role in S2. |
| TQ-04 | Where are plans defined? | Seeded by migration: `starter`, `growth`, `enterprise` with module ceilings and limits. No plan editing UI in 1.0. |
| TQ-05 | US-006 AC4 "open work blocks disable" with no modules yet. | Interface `IModuleWorkInspector` in Platform.Contracts; each module implements it later. S1 ships the mechanism and tests it with a fake inspector. |
| TQ-06 | Tenant subdomains on a laptop. | Use `*.localtest.me` (public DNS wildcard resolving to 127.0.0.1): `acme.localtest.me:5080`. |
| TQ-07 | LLD 2.5 opens the DB connection synchronously inside the DbContext factory. | Fixed (see `decisions.md`). |
| TQ-08 | Local replacements for SNS/SQS and S3 without paid emulators. | `IMessageTransport` with two adapters: `PostgresTransport` (local; queue table + `LISTEN/NOTIFY`) and `SnsSqsTransport` (AWS). `IFileStore` with `LocalFileStore` and `S3FileStore`. Same consumer code in both. |
| TQ-09 | FluentAssertions licence. | Became commercial from v8. Use **Shouldly** (BSD) for assertions. |
| TQ-10 | .NET version. | .NET 10 SDK, pinned in `global.json`. |
| TQ-11 to TQ-14 | Decided during EN-01. | See `decisions.md`. |

---

## 3. Engineering Conventions

### 3.1 Git and PRs
- Trunk-based. `main` is always releasable.
- Short branches: `feat/US-001-create-tenant`, `fix/...`, `chore/...`. Live for less than 3 days.
- Squash merge. Conventional commit titles (`feat(platform): create tenant (US-001)`).
- PR template includes the PRD Definition of Done checklist.
- Branch protection on `main`: PR required, CI green, linear history.

### 3.2 Code standards
- `Directory.Build.props`: nullable enabled, warnings as errors, analyzers (latest).
- `.editorconfig` with file-scoped namespaces, `var` where type is obvious, expression bodies for one-liners.
- No `DateTime.Now` or `Guid.NewGuid()`: use `IClock` and `Ids.New()` (architecture test bans both).
- No business logic in endpoints. Endpoint = map request, call handler, map result.
- Every handler has unit tests; every endpoint has at least one integration test.

### 3.3 Package list

| Package | Licence | Use |
|---|---|---|
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL | EF Core provider |
| Dapper | Apache 2.0 | Reads (when needed) |
| FluentValidation | Apache 2.0 | Validation |
| Scrutor | MIT | Decorator registration |
| Microsoft.FeatureManagement.AspNetCore | MIT | Flags (US-006) |
| StackExchange.Redis | MIT | Valkey client (US-002) |
| Serilog.AspNetCore | Apache 2.0 | Logging |
| OpenTelemetry.Extensions.Hosting + instrumentation | Apache 2.0 | Tracing |
| xunit.v3 | Apache 2.0 | Tests |
| Shouldly | BSD-3 | Assertions |
| NSubstitute | BSD-3 | Fakes |
| Testcontainers.PostgreSql, Testcontainers.Redis | MIT | Integration tests |
| Microsoft.AspNetCore.Mvc.Testing | MIT | API tests |

---

## 4. Local Development Stack

See `README.md` and `docker-compose.yml`.

| Service | Image | Port |
|---|---|---|
| PostgreSQL 17 | `postgres:17` | 5432 |
| Valkey 8 | `valkey/valkey:8` | 6379 |
| Mailpit (from S2) | `axllent/mailpit` | 8025 UI, 1025 SMTP |

Database roles created by `db/init/01-roles-and-database.sql`: `wms_owner` (migrations), `wms_app` (runtime, no BYPASSRLS), `wms_platform_jobs` (BYPASSRLS, Worker only).

---

## 5. Sprint 1 (Solo) Breakdown

**Sprint goal:** The solution skeleton runs locally with CI. A platform admin can create a tenant, tenant subdomains resolve, and module flags work per plan, tenant, and client. All isolation layers are in place from day one.

**Points:** 21. **Capacity:** ~60 focused hours (solo, part-time assumption 30 h/week). Track actuals per task.

### EN-01 Solution skeleton, CI, local environment (8) - DONE

Delivered on branch `feat/EN-01-solution-skeleton`. Verified in the build workspace: package-free projects compile with 0 warnings; architecture rules pass on the real tree; migrations apply on PostgreSQL and RLS behaves as designed. **Not yet verified:** projects that need NuGet packages (the build workspace had no NuGet access). First full compile happens on a machine with internet or in CI.

### US-001 Create a tenant (5) - NEXT

| # | Task | Est (h) |
|---|---|---|
| 2.1 | Platform Domain: Tenant aggregate (code, subdomain rules, status transitions Trial/Active/Suspended/Terminated), Plan | 3 |
| 2.2 | Migration: `platform.tenant`, `platform.plan` (seed starter/growth/enterprise), `platform.warehouse`, `platform.client` (empty use for now) | 2 |
| 2.3 | `CreateTenant` command + validator (code, DNS-safe subdomain, reserved words like `api`, `www`, `edge`) | 3 |
| 2.4 | Provisioning step: seed default config rows and a Tenant Admin user record (Invited) via Identity contract stub | 2 |
| 2.5 | `SuspendTenant` / `ActivateTenant` commands | 1 |
| 2.6 | Platform endpoints `/api/platform/tenants` protected by dev API key (TQ-03) | 1 |
| 2.7 | Tests: domain (subdomain rules, transitions), handler, integration (create, duplicate subdomain, suspend) | 3 |
| | **Subtotal** | **15** |

### US-002 Resolve tenant from subdomain (3)

| # | Task | Est (h) |
|---|---|---|
| 3.1 | `TenantResolutionMiddleware`: host header > subdomain > tenant catalog | 2 |
| 3.2 | `TenantCatalog` with memory + Valkey cache (5 min) and invalidation via Valkey pub/sub on tenant change | 3 |
| 3.3 | Mismatch check: claims `tenant_id` vs resolved tenant (tested with test auth handler, TQ-02); security event log entry | 1 |
| 3.4 | Suspended tenant returns 403 `TENANT_SUSPENDED`; unknown subdomain returns 404 generic body | 1 |
| 3.5 | Tests: middleware unit tests, integration over `acme.localtest.me` host header, **RLS test: query with tenant A session returns zero rows of tenant B even with no EF filter** | 3 |
| | **Subtotal** | **10** |

### US-006 Feature flag framework (5)

| # | Task | Est (h) |
|---|---|---|
| 4.1 | Migration: `platform.feature_setting`; flag catalogue seed (Appendix B of Functional Spec) | 1 |
| 4.2 | `IFeatureResolver`: Plan ceiling AND tenant AND client override; cached per tenant, invalidated by pub/sub | 3 |
| 4.3 | Microsoft.FeatureManagement custom feature definition provider + `FeatureFlagFilter` endpoint filter returning 403 `MODULE_DISABLED` | 2 |
| 4.4 | `SetFeature` command with checks: not in plan, open work via `IModuleWorkInspector` (TQ-05), audit entry | 2 |
| 4.5 | Endpoints: list effective flags for tenant/client, set flag | 1 |
| 4.6 | Tests: resolution matrix (plan off / tenant on, tenant off / client on, etc.), 60-second visibility via cache invalidation, blocking inspector | 3 |
| | **Subtotal** | **12** |

### Sprint 1 demo script
1. `docker compose up`, run migrator, start AppApi.
2. `POST /api/platform/tenants` creates `acme` on plan `growth`. Show seeded config and Invited admin.
3. `GET http://acme.localtest.me:5080/api/app/ping` returns Acme context. Unknown subdomain returns 404.
4. Suspend Acme; same call returns 403 `TENANT_SUSPENDED`.
5. Turn `module.returns` off for Acme; a sample Returns ping endpoint returns 403 `MODULE_DISABLED`. Turn it on for one client only; show effective flags.
6. Run the RLS test live: raw SQL with tenant A session cannot see tenant B rows.
7. Open the CI run on the PR: all gates green.

---

## 6. Sprint 1 Risks

| # | Risk | Mitigation |
|---|---|---|
| TR-01 | Building blocks take longer than planned (they are the base for everything) | Keep them minimal; add features only when a story needs them |
| TR-02 | RLS plus pooled connections misconfigured, leaking tenant session state | Set `app.tenant_id` on every open; Npgsql resets session on return to pool; integration test opens 2 tenants on the same pool in sequence |
| TR-03 | Testcontainers slow in CI | Share one PostgreSQL container per test assembly; reset with schema truncation |
| TR-04 | Solo velocity unknown | Track hours per task; re-plan after S1 and S2 |
