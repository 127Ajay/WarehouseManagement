# WMS SaaS: Architecture, High-Level Design and ADRs

| Field | Value |
|---|---|
| Version | 1.0 |
| Date | 2026-11-21 |
| Phase | 3. Solution Architect, step 1 of 3 |
| Sources | BRD, PRD, Functional Spec, Backlog (docs 01 to 04) |
| Next | Step 2: Low-level design |

> **Accepted 2026-11-21:** ADR-003 PostgreSQL (Aurora), ADR-005 SNS/SQS + outbox, ADR-014 Blazor WebAssembly. Implementation-time changes are in `decisions.md` (e.g. architecture tests read `.csproj` files instead of ArchUnitNET).

---

## 1. Architecture Drivers

The design is shaped by these, in priority order:

| # | Driver | Source | Design consequence |
|---|---|---|---|
| 1 | Stock must never be wrong; ledger and balance always consistent | FR-INV-10 | Strong consistency for stock writes; single transactional database for inventory |
| 2 | Handheld scan p95 < 300 ms | NFR-010 | Lean hot-path endpoints, no cross-service network hops on scan paths, region close to users |
| 3 | Zero cross-tenant leaks | NFR-042 | Tenant isolation in three layers (app, ORM, database) |
| 4 | Feature-based modules, toggled per tenant and client | BR-013 | Module per bounded context, feature flags checked at the edge of every module |
| 5 | Team of 7, 12 sprints | PRD | Fewest moving parts that meet the targets; managed AWS services; no microservices |
| 6 | Lower price than incumbents | BR-004 | Open-source DB and libraries, pay-per-use AWS services, shared tenancy by default |
| 7 | Grow to 3,000 warehouses, 1M lines/day | NFR-001, 002 | Stateless hosts, horizontal scale, ledger partitioning, extractable modules |

---

## 2. Architecture Style

**Modular monolith, Clean Architecture inside each module, event-driven between modules.**

- One codebase, a small number of deployable hosts (section 4).
- Each business capability is a **module** (bounded context) with its own Domain, Application, Infrastructure, and Contracts projects, and its own database schema.
- Modules talk to each other only through **Contracts** (public interfaces and integration events). Never through each other's tables or internal classes. Enforced by architecture tests in CI (NFR-100).
- Any module can be pulled out into its own service later, because the boundary is already a contract.

**Why not microservices now:** stock moves touch Inventory, Inbound, Outbound, and Locations in one user action. Splitting them across services would force distributed transactions or sagas on every scan, add network hops against the 300 ms target, and needs a bigger team to run. A modular monolith gives the same boundaries at a fraction of the cost. (ADR-001)

---

## 3. Module Map

| Module | Owns (data) | Epics |
|---|---|---|
| **Platform** | Tenants, plans, warehouses, clients, feature flags, config, document sequences | E01, E03 |
| **Identity** | Users, roles, assignments, devices, auth tokens, security audit | E02 |
| **Catalog** | Items, UOMs, barcodes | E04 |
| **Layout** | Locations, zones, putaway rules | E05 |
| **Inventory** | Stock records, LPNs, ledger, adjustments, status changes, reason codes | E07 |
| **Inbound** | ASNs, receipts, GRNs, QC inspections, putaway tasks | E08 |
| **Outbound** | Orders, allocations, pick lists, pick tasks, cartons, shipments, manifests | E09 |
| **Returns** | Returns, grading | E11 |
| **Counting** | Count plans, count tasks, variances, replenishment tasks | E12 |
| **Traceability** | Trace queries, recalls | E13 |
| **Integration** | API clients, webhooks, message log, SFTP, connectors (Shiprocket) | E10 |
| **Reporting** | Report definitions, saved views, exports, dashboard read models | E14 |
| **Notifications** | Notification preferences, in-app messages, email dispatch | NTF |
| **Devices** | Edge Agents, printers, print jobs, label templates | E06 |
| **Billing** (1.1) | Rate cards, charges | E15 |
| **ClientPortal** (1.1) | Portal-specific read models | E16 |

### Key dependency rule
**Inventory is the only module that writes stock.** Inbound, Outbound, Returns, and Counting call Inventory's command contracts (`IInventoryCommands.Receive(...)`, `.Pick(...)`, etc.) inside the same database transaction. Every stock change therefore goes through one place that writes the stock record and the ledger event together.

```
             +-----------+
             | Platform  |  (tenancy, flags, config: used by all)
             +-----------+
                   ^
   +---------+  +--------+  +---------+  +--------+  +----------+
   | Inbound |  |Outbound|  | Returns |  |Counting|  |Traceabil.|
   +----+----+  +---+----+  +----+----+  +---+----+  +----+-----+
        |           |            |           |            | (read)
        +-----------+------+-----+-----------+------------+
                           v
                    +-------------+     +---------+   +---------+
                    |  Inventory  |---->| Layout  |   | Catalog |
                    | (stock +    |     +---------+   +---------+
                    |  ledger)    |
                    +-------------+
                           |
              integration events (outbox)
                           v
      +-------------+  +-----------+  +---------------+
      | Integration |  | Reporting |  | Notifications |
      +-------------+  +-----------+  +---------------+
```

---

## 4. Deployable Hosts

Same codebase, different composition roots.

| Host | Contains | Scales on | Notes |
|---|---|---|---|
| **App API** | Endpoints for web app and handheld (`/api/app/...`), all modules' commands and queries | CPU, request count | Latency-critical. Handheld traffic. |
| **Integration API** | Public `/api/v1/...` endpoints | Request count | Separate host so client API bursts never slow handhelds. Own rate limits. |
| **Worker** | Outbox dispatcher, event handlers, scheduled jobs (expiry, reconciliation, SFTP poll), webhook delivery, connectors, report exports | Queue depth | No inbound HTTP. |
| **Realtime Hub** | SignalR hub for Edge Agents and live dashboard push | Connections | Redis backplane. |
| **Web App** | Static SPA files | CDN | Served from S3 + CloudFront. |
| **Handheld App** | .NET MAUI Android app | n/a | Distributed via MDM or Play private channel. |
| **Edge Agent** | .NET Worker Service on a Windows PC per warehouse | n/a | Printers, later scales and automation. |

---

## 5. Technology Stack

| Layer | Choice | Why |
|---|---|---|
| Runtime | **.NET 10 (LTS)**, C# | Constraint; LTS until late 2028 |
| API | ASP.NET Core Minimal APIs, grouped per module | Low overhead on hot paths |
| ORM | EF Core for writes; Dapper for heavy reads and reports | EF for domain consistency, Dapper for speed |
| Database | **Amazon Aurora PostgreSQL** | Lower cost than SQL Server licences (pricing goal), strong JSONB, native partitioning, row-level security, read replicas |
| Cache | Amazon ElastiCache (Redis/Valkey) | Flags, config, tenant catalog, SignalR backplane, rate limits |
| Messaging | **Transactional outbox + Amazon SNS/SQS** | Pay-per-use, no cluster to run; at-least-once with idempotent consumers |
| Background jobs | Hangfire (PostgreSQL storage) | Scheduled and recurring jobs with dashboard |
| Identity | ASP.NET Core Identity + **OpenIddict** | Free, embeddable, supports custom grant (badge + PIN), client credentials, MFA, and OIDC federation for SSO later |
| Feature flags | Microsoft.FeatureManagement with custom tenant/client provider | Standard API, our own store |
| Validation | FluentValidation | |
| PDF | QuestPDF (check licence tier against revenue) | GRN, packing slip, manifest |
| Labels | ZPL templates with Scriban placeholders | Rendered server-side, sent to Edge Agent |
| Files | Amazon S3 | Photos, labels, exports, archives |
| Observability | OpenTelemetry + Serilog to CloudWatch / X-Ray | Tenant and warehouse tags on every span |
| Web frontend | **Blazor WebAssembly + MudBlazor** (ADR-014, accepted) | |
| Mobile | .NET MAUI (Android) with SQLite (SQLCipher) | Decided in BRD |
| Infra as code | Terraform | |
| Hosting | Amazon ECS on Fargate | Containers without managing Kubernetes |
| CI/CD | GitHub Actions | |

**Library licence note:** several popular .NET libraries moved to commercial licences in 2025 (MediatR, AutoMapper, MassTransit v9). We avoid them. Commands are dispatched with a small in-house dispatcher; mapping is explicit code. The Tech Lead will run a licence check on every new dependency.

---

## 6. Multi-Tenancy Design

### 6.1 Tenant resolution
1. Request arrives at `{subdomain}.app-domain` (web/handheld) or with an API client token (integration).
2. Middleware looks up the tenant in the **Tenant Catalog** (Platform module table, cached in Redis, 5-minute TTL plus invalidation on change).
3. Token's `tenant_id` claim must equal the resolved tenant. Mismatch = 401 + security event (FR-PLT-06).
4. `ITenantContext` (tenant ID, plan, region, connection string key) is set for the request.

### 6.2 Isolation in three layers (NFR-042)
| Layer | Mechanism |
|---|---|
| Application | Every command/query handler receives `ITenantContext`; repositories never accept a tenant ID from the request body |
| ORM | EF Core global query filter `TenantId == current` on every tenant-owned entity; inserts stamp `TenantId` automatically |
| Database | **PostgreSQL row-level security** on every tenant table. Each connection runs `SET app.tenant_id = ...` when opened from the pool. A missed filter in code still returns zero rows. |

### 6.3 Shared vs dedicated (BR-011)
- **Shared:** all tenants in one Aurora cluster per region, one database, schema per module.
- **Dedicated (1.2):** separate Aurora cluster for the tenant, same schemas. Tenant Catalog holds the connection key; a connection resolver picks it. Code is identical.

### 6.4 Data scope inside a tenant
Warehouse, client, and zone scope (FR-IAM-09) are applied in application query handlers from the user's role assignments, not by RLS. RLS is the tenant safety net; scope is business logic.

---

## 7. Decisions on Open Questions (Functional Spec Appendix F)

### ADR-004 (Q-01) Stock and ledger in one transaction, not event sourcing
**Decision:** Stock records are a normal state table. Each stock change updates the stock row and inserts the ledger row in **the same database transaction**. Ledger is append-only (no UPDATE/DELETE grants for the app role).

**Why:**
- Allocation needs current, exact balances under concurrency. Event-sourced projections with lag would allow double allocation.
- Simple to reason about, test, and debug with a small team.
- The ledger still gives full history and traceability. Balances can be rebuilt from it (nightly check, FR-INV-13).

**Concurrency:** optimistic concurrency on stock rows using PostgreSQL `xmin` as row version. Allocation runs are serialized per (tenant, warehouse, client) using a PostgreSQL advisory lock, so two allocation runs never fight over the same stock.

**Scale:** about 8 ledger events per order line. At 1M lines/day that is ~8M rows/day, ~3B rows/year. The ledger table is **partitioned by month**, indexed on `(tenant_id, sku_id, batch)` and `(tenant_id, lpn_id)`. Partitions older than 24 months are exported to S3 as Parquet and queried with Athena (NFR-062).

**Rejected:** full event sourcing (complexity, projection lag on allocation); separate ledger database (breaks single-transaction guarantee).

### ADR-006 (Q-02) Reporting from Aurora replica plus summary tables
**Decision:**
- Reports and traces read from an **Aurora read replica** (replica lag normally under 1 second, well within the 5-minute rule).
- Dashboards read **summary tables** in a `reporting` schema, updated by Worker event handlers from outbox events (e.g. orders by status per warehouse per day). Live dashboard tiles get pushed through the Realtime Hub.
- Traces on archived data (older than 24 months) go to Athena over S3 Parquet; up to 30 seconds is acceptable (FR-TRC-05).

**Why:** meets all Release 1.0 targets with no new database technology.
**Revisit when:** report load affects replica lag, or Release 1.2+ needs a BI feed. Then add a columnar store (e.g. ClickHouse or Redshift Serverless) fed from the same events.

### ADR-007 (Q-03) Handheld offline queue
**Decision:**
- Local queue in SQLite encrypted with SQLCipher. Key held in Android Keystore.
- Each queued command has a client-generated UUID (used as the server idempotency key), the task ID, the stock row version seen at download, and a device timestamp.
- On reconnect, commands are sent in order, one at a time. Server checks row version; if stock changed, the command is rejected as a conflict and the task goes to "Needs review" (FR-HHD-13).
- Only task-confirmation commands are queueable (FR-HHD-10). The app checks the command type before queuing.

### ADR-008 (Q-04) Edge Agent connection
**Decision:**
- Edge Agent opens an **outbound WebSocket using SignalR** to the Realtime Hub. No inbound ports at the site.
- Auth: at registration, the agent exchanges the one-time token for a **client certificate-bound refresh token** (rotated every 24 hours).
- Print jobs are pushed over the hub; the agent acknowledges after the printer accepts the job. Jobs are also stored in a local SQLite queue so received jobs survive internet drops and agent restarts.
- Heartbeat every 30 seconds; Offline after 2 minutes missed (FR-HHD-24).

**Rejected:** MQTT (extra broker to operate); polling (slower, wasteful).

### ADR-009 (Q-05) Feature flags and config
**Decision:**
- Flags and config stored in the Platform module's tables.
- Read through Microsoft.FeatureManagement with a custom provider that resolves Plan AND Tenant AND Client.
- Cached in process memory (30 seconds) and Redis. On change, a Redis pub/sub message clears caches on all hosts. Worst case visible within 60 seconds (US-006 AC2).
- Every module's endpoint group has a filter: flag check, then permission check, then scope check (FR-GEN-07).

### ADR-010 (Q-06) Document number sequences
**Decision:**
- Table `platform.document_sequence (tenant_id, warehouse_id, doc_type, period, next_value)`.
- Each host process reserves a **block of 20 numbers** with one `UPDATE ... RETURNING` and hands them out from memory.
- Gaps are allowed (FR-GEN spec says gap-tolerant), so no hot-row contention even at peak.
- LPN and SSCC use the same mechanism with larger blocks (100).

---

## 8. Other Key Decisions

### ADR-001 Modular monolith
See section 2.

### ADR-002 Clean Architecture per module
Each module has:
- `Domain`: entities, value objects, domain rules. No framework dependencies.
- `Application`: commands, queries, handlers, validators, ports (interfaces).
- `Infrastructure`: EF Core mappings, repositories, external adapters.
- `Contracts`: public command interfaces, DTOs, integration events. **The only project other modules may reference.**
- `Endpoints`: Minimal API route groups for this module.

Details and folder layout come in step 2 (LLD).

### ADR-003 PostgreSQL (Aurora) over SQL Server
- Licence cost: SQL Server on RDS adds a large per-core licence charge per instance, which works against the pricing goal (BR-004).
- Needed features exist in PostgreSQL: row-level security, declarative partitioning, JSONB for custom fields (1.1), advisory locks.
- Team impact: SQL is largely transferable; EF Core and Dapper work the same way.

### ADR-005 Inter-module communication
| Need | Mechanism |
|---|---|
| Change stock as part of a business action | Synchronous call to Inventory command contract, same transaction |
| Read another module's data | Synchronous query contract (e.g. `ICatalogQueries.GetItem`) with in-process cache |
| Notify others something happened | Integration event written to the **outbox** in the same transaction; Worker publishes to SNS; subscribers get it on their SQS queue |
| External webhooks | Integration module subscribes to events and delivers (FR-INT-10 to 13) |

All event consumers are **idempotent** (event ID stored in an `inbox` table per consumer).

### ADR-011 Identity with OpenIddict
- Web: authorization code + PKCE, cookie session, TOTP MFA.
- Handheld: custom grant `urn:wms:badge_pin` (badge, PIN, device ID). Device must be Active and registered to the warehouse (FR-IAM-05).
- Integration API: client credentials, scopes per FR-INT-02, client-to-3PL-client binding as a token claim.
- SSO (1.2): OpenIddict acts as our server; external OIDC providers (Azure AD, Okta, Google) are federated in.
- Access tokens: 15 minutes. Permissions are not stored in tokens (too large); they are loaded per user and cached in Redis, invalidated on role change.

### ADR-012 Hosting on ECS Fargate
- Small team, no Kubernetes to operate.
- Each host is an ECS service with auto scaling.
- Aurora PostgreSQL Multi-AZ (writer + 1 reader at launch), ElastiCache Multi-AZ, S3, SNS/SQS, Secrets Manager, WAF on ALB and CloudFront.
- Primary ap-south-1; backups and S3 replicated to ap-south-2 (NFR-023, 030).

### ADR-013 API surface
| Route prefix | Consumer | Auth | Versioning |
|---|---|---|---|
| `/api/app/...` | Our web app and handheld | User token | Not versioned publicly; web and handheld ship together, handheld enforces minimum app version |
| `/api/v1/...` | Client systems | Client credentials | Versioned, 12-month support (NFR-102) |
| `/hubs/...` | Edge Agent, live dashboards | Agent token / user token | |

Errors use RFC 9457 Problem Details with our `code` and `traceId` fields (FR-INT-07).

### ADR-015 Handheld performance budget (NFR-010)
Target p95 300 ms from trigger to screen update:

| Segment | Budget |
|---|---|
| Device: scan decode + UI | 30 ms |
| Network round trip (Wi-Fi + internet to ap-south-1) | 120 ms |
| ALB + API host processing | 100 ms |
| Database (1 to 3 indexed queries, 1 write) | 40 ms |
| Spare | 10 ms |

**Rules for hot paths** (scan, confirm, next task):
- No calls to external systems; no synchronous cross-host calls.
- Response payload under 2 KB.
- HTTP/2 with connection reuse from the handheld.
- Task lists are downloaded once per list, not per scan.
- Spike SP-01 (Sprint 1) validates the network segment; if Wi-Fi alone eats the budget, the fallback is optimistic UI on the handheld for scan validation that can be done locally (e.g. location barcode matches the task).

---

## 9. Key Runtime Flows (summary; full sequences in LLD)

**Receive on handheld (US-036)**
Handheld > App API `POST /api/app/inbound/receipts/{asnId}/lines` > Inbound handler validates ASN and rules (config resolved from cache) > calls `IInventoryCommands.Receive` > Inventory writes stock row + ledger event > Inbound writes receipt line > outbox event `ReceiptLineRecorded` > commit > 200. Worker later publishes the event for Reporting and webhooks.

**Allocate orders (US-048)**
Supervisor > App API > Outbound takes advisory lock (tenant, warehouse, client) > loads eligible stock via Inventory query contract (one set-based query) > allocation algorithm in Outbound domain > `IInventoryCommands.Reserve` in batch > commit > outbox `OrderAllocated` events.

**Recall (US-076)**
Approve > Traceability calls `IInventoryCommands.BlockBatch` per warehouse in one transaction per warehouse > outbox `BatchRecalled` > Outbound handler releases allocations and holds orders > Integration sends `batch.recalled` webhook.

---

## 10. How the Design Meets Key NFRs

| NFR | How |
|---|---|
| NFR-010 scan < 300 ms | ADR-015 budget and hot-path rules; Mumbai region; spike in Sprint 1 |
| NFR-013 allocation 5,000 lines < 60 s | Set-based stock query, in-memory algorithm, batched writes, advisory lock per client |
| NFR-014 trace < 5 s | Ledger indexes by (tenant, sku, batch); read replica; partition pruning by date |
| NFR-020 99.9% | Multi-AZ everything; stateless hosts with 2+ tasks; rolling deploys |
| NFR-022 RPO 5 min / RTO 1 h | Aurora continuous backup (PITR); Aurora Global Database for region loss (see doc 07); tested restore runbook |
| NFR-042 isolation | Three-layer isolation (section 6.2); CI isolation suite (EN-06) |
| NFR-044 noisy neighbour | Separate Integration API host; per-tenant rate limits in Redis; per-tenant queue fairness in Worker |
| NFR-080 metering | Usage counters from outbox events into `platform.usage_daily` |
| NFR-100 boundaries | Contracts-only references; architecture tests in CI |

---

## 11. Decisions Confirmed by Product Owner

| # | Decision | Chosen | Alternative considered |
|---|---|---|---|
| ADR-003 | Database | **PostgreSQL (Aurora)** | SQL Server: team familiarity, higher licence cost |
| ADR-005 | Event transport | **SNS/SQS with outbox** | Amazon MSK (Kafka): replay and streaming, but a cluster to pay for and run |
| ADR-014 | Web frontend | **Blazor WebAssembly + MudBlazor** | React + TypeScript: bigger talent pool and ecosystem, but a second language for a 1-person frontend team |

## 12. Risks

| # | Risk | Mitigation |
|---|---|---|
| AR-01 | Ledger growth (~3B rows/year at target scale) | Monthly partitions, archive to S3 after 24 months, index review at each 10x growth |
| AR-02 | Allocation lock contention for one huge client | Lock per client; split by warehouse; measure in EN-08 |
| AR-03 | Shared DB noisy neighbour | Statement timeouts per role, per-tenant rate limits, dedicated tier for heavy tenants |
| AR-04 | Modular boundaries erode under deadline pressure | Architecture tests fail the build; Tech Lead review on any Contracts change |
| AR-05 | Handheld network latency outside our control | SP-01 spike; optimistic local validation fallback |
