# Engineering Decision Log

Short records of decisions made during implementation. Architecture-level ADRs are in the
project docs (HLD section 7 and 8); this file holds the ones made while building.

| ID | Date | Decision | Why |
|---|---|---|---|
| TQ-07 | 2026-11-21 | `UnitOfWork` creates the `NpgsqlConnection` object up front; decorators open it asynchronously before a handler runs. Module DbContexts are built on that connection with `AutoTransactionBehavior.Never`. A `TenantSessionInterceptor` sets `app.tenant_id` if EF opens the connection itself. | Removes sync-over-async from LLD 2.5. Handlers get DbContexts injected before the connection opens, so the connection must exist but stay closed until the pipeline opens it. |
| TQ-11 | 2026-11-21 | SQL-first migrations per module (`Migrations/V0001__name.sql`, embedded), applied by `Wms.Host.Migrator` with checksums and an advisory lock. EF Core is the ORM only; no EF migrations. | RLS policies, partitions, grants and `NULLS NOT DISTINCT` are SQL anyway. Also removes the need for the `dotnet ef` tool. Supersedes LLD section 13 "EF Core migrations per module". |
| TQ-12 | 2026-11-21 | `*.Endpoints` may reference its own module's `Infrastructure` project. | The module's `IModule` is its composition root and must register its own infrastructure. Endpoint code still calls only Application handlers. |
| TQ-13 | 2026-11-21 | Architecture tests read `.csproj` files directly instead of using ArchUnitNET. | Catches declared-but-unused references (compiled assemblies drop them), needs no extra package, and the rules fit in one file. |
| TQ-14 | 2026-11-21 | RLS policies compare against `NULLIF(current_setting('app.tenant_id', true), '')::uuid`. | A session with no tenant has an empty string, not NULL; casting `''` to uuid would error instead of matching nothing. |
