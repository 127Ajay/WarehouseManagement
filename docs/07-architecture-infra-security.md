# WMS SaaS: Infrastructure, Security, CI/CD, DR, NFR Verification

| Field | Value |
|---|---|
| Version | 1.0 |
| Date | 2026-11-21 |
| Phase | 3. Solution Architect, step 3 of 3 |
| Sources | `05-architecture-hld.md`, `06-architecture-lld.md` |
| Next phase | 4. Tech Lead + Developers |

> **Current status:** development is local-first (Docker Compose). AWS setup starts in solo Sprint 7 (see `08-tech-lead-sprint-01-plan.md`).

---

## 1. Environments and AWS Accounts

AWS Organizations with one account per environment. Blast radius stays small and billing is clear.

| Account | Purpose | Region | Data |
|---|---|---|---|
| `wms-shared` | ECR, Terraform state, CI roles | ap-south-1 | None |
| `wms-security` | CloudTrail org trail, Config, GuardDuty and Security Hub admin, log archive | ap-south-1 | Logs only |
| `wms-dev` | Developer integration | ap-south-1 | Synthetic |
| `wms-test` | QA, automated E2E | ap-south-1 | Synthetic |
| `wms-staging` | Prod-like: performance tests, pen test, pilot UAT rehearsal | ap-south-1 | Synthetic, prod-scale volume |
| `wms-prod` | Production | ap-south-1 primary, ap-south-2 DR | Real tenant data |

Dev and test scale to zero tasks outside 08:00 to 22:00 IST to save cost.

---

## 2. Network (per environment, prod shown)

```
VPC 10.20.0.0/16, 3 AZs (ap-south-1a, 1b, 1c)
├─ Public subnets    /24 x3   ALB, NAT gateways
├─ App subnets       /20 x3   ECS Fargate tasks (private, no public IP)
└─ Data subnets      /24 x3   Aurora, ElastiCache (no route to internet)
```

- **VPC endpoints** for S3, SQS, SNS, Secrets Manager, ECR (api + dkr), CloudWatch Logs, KMS, STS. Most AWS traffic skips NAT, which cuts cost and exposure.
- NAT gateways only for outbound calls to Shiprocket, client webhooks, SFTP-out, and email provider. Egress goes through fixed Elastic IPs so clients can allow-list us.
- Security groups:
  - ALB: 443 from CloudFront managed prefix list and the internet (Integration API).
  - ECS tasks: from ALB security group only.
  - Aurora 5432 and Redis 6379: from ECS task security groups only.
- **SFTP:** AWS Transfer Family (SFTP) endpoint backed by S3, one user per client, keys only.

---

## 3. Edge and Routing

| Host name | Path | Origin |
|---|---|---|
| `*.app-domain` (tenant subdomains) | `/` (default) | CloudFront > S3 (Blazor static files) |
| `*.app-domain` | `/api/app/*` | CloudFront > ALB > App API target group |
| `*.app-domain` | `/hubs/*` | CloudFront > ALB > Realtime target group (WebSockets) |
| `api.app-domain` | `/v1/*` | ALB (no CloudFront) > Integration API target group |
| `edge.app-domain` | `/hubs/edge` | ALB > Realtime target group |

- Wildcard certificate `*.app-domain` from ACM (us-east-1 copy for CloudFront, ap-south-1 copy for ALB).
- CloudFront terminates TLS at edge locations in India, close to the warehouse. This takes TLS handshake time out of the handheld's 120 ms network budget (ADR-015). API responses are never cached (`Cache-Control: no-store`, caching disabled policy on `/api/*`).
- **AWS WAF** on CloudFront and ALB:
  - AWS managed rules: Core rule set, Known bad inputs, SQL database, IP reputation.
  - Rate-based rule: 300 requests per 5 minutes per IP on `/connect/token` (login brute force).
  - Geo rule: Integration API open globally; App API restricted to India for Release 1.0 (tenant-level allow-list later).

---

## 4. Compute (ECS on Fargate, ARM64 Graviton)

ARM64 is about 20% cheaper than x86 on Fargate and .NET 10 runs natively on it.

| Service | Task size | Prod min / max | Scale rule | Health check |
|---|---|---|---|---|
| `app-api` | 1 vCPU, 2 GB | 3 / 20 | Target tracking: CPU 50%, ALB requests per target 400/min | `/health/ready` (DB + Redis) |
| `integration-api` | 0.5 vCPU, 1 GB | 2 / 10 | CPU 60%, requests per target | `/health/ready` |
| `worker` | 1 vCPU, 2 GB | 2 / 10 | Step scaling on SQS `ApproximateNumberOfMessagesVisible` per task > 500, and custom metric outbox lag > 30 s | `/health/live` (internal port) |
| `realtime` | 0.5 vCPU, 1 GB | 2 / 6 | Active connections per task > 3,000 | `/health/ready` |
| `migrator` | 0.5 vCPU, 1 GB | run-task | Runs once per deploy, before services update | exit code |

- Minimum 3 tasks for `app-api` keeps one per AZ (NFR-020).
- Container images: .NET 10 Ubuntu Chiseled (no shell, non-root), read-only root file system, `/tmp` as ephemeral volume.
- SignalR uses WebSockets only (`SkipNegotiation`), so no ALB sticky sessions. Redis backplane shares groups across tasks.
- Graceful shutdown: 30 s stop timeout; ALB deregistration delay 20 s so in-flight scans complete.

---

## 5. Data Services

### 5.1 Aurora PostgreSQL (v17)

| Item | Pilot (Release 1.0) | Year 1 (10 tenants, 40 WH) | Year 3 (500 tenants, 3,000 WH) |
|---|---|---|---|
| Writer | db.r7g.large (2 vCPU, 16 GB) | db.r7g.xlarge | db.r7g.4xlarge, plus dedicated clusters for heavy tenants |
| Readers | 1 x db.r7g.large | 1 to 2 x r7g.xlarge | 2 to 4, auto scaling |
| Storage config | Standard | Switch to I/O-Optimized if I/O exceeds ~25% of DB bill | I/O-Optimized |

- **No RDS Proxy.** Our RLS sets a session variable (`app.tenant_id`) on each connection, which would pin proxy connections and remove its benefit. Npgsql connection pooling in each task is used instead. Budget: 50 connections per task x max ~40 tasks = 2,000, under the r7g.xlarge limit. Revisit at Year 3 with PgBouncer in transaction mode and `set_config(..., true)` inside each transaction.
- Parameters: `rds.force_ssl = 1`, `statement_timeout = 30s` for `wms_app` role (reports role 120 s), `idle_in_transaction_session_timeout = 60s`, `log_min_duration_statement = 500ms`.
- Backups: continuous (point-in-time restore, 35 days retention).
- **DR:** Aurora Global Database with a **headless secondary cluster in ap-south-2** (replicated storage, no instances, so low cost). See section 9.
- Performance Insights on, 7 days free tier, 2 years on prod writer.

### 5.2 ElastiCache (Valkey)
- Pilot: cache.t4g.medium, primary + 1 replica, Multi-AZ, encryption in transit and at rest, AUTH token.
- Year 1: cache.r7g.large.
- Used for: tenant catalog, flags, config, permissions, rate limits, idempotency keys, SignalR backplane. **No data lives only in Redis**; losing it costs latency, not correctness.

### 5.3 S3 buckets

| Bucket | Content | Lifecycle | Replication to ap-south-2 |
|---|---|---|---|
| `web` | Blazor static files | Versioned | Yes |
| `documents` | GRN, packing slip, manifest PDFs, labels | 90 days then delete (re-generable) | No |
| `media` | QC and returns photos | 7 years, Glacier Instant Retrieval after 90 days | Yes |
| `exports` | Report exports | Delete after 7 days | No |
| `archive` | Ledger and audit Parquet older than 24 months | **Object Lock (compliance mode) 7 years** | Yes |
| `sftp` | Transfer Family home folders | Processed files 30 days | No |
| `logs` | ALB, CloudFront access logs | 1 year | No (sent to security account) |

All buckets: Block Public Access on, SSE-KMS with customer managed keys, TLS-only bucket policy. The `web` bucket is reached by CloudFront through Origin Access Control only.

### 5.4 Messaging
- SNS topic `wms-events` (one per environment). SQS standard queue per consumer module, each with a DLQ (`maxReceiveCount = 5`), 14-day DLQ retention.
- SNS subscription filter policies on the `type` attribute.
- Encryption with KMS on topic and queues.
- **SES** for email (notifications, invites), with a dedicated sending domain, DKIM and SPF.

---

## 6. Security Design

### 6.1 Identity and access (cloud)
- No IAM users with long-lived keys. Humans use IAM Identity Center with MFA. CI uses **GitHub OIDC** to assume deploy roles.
- One IAM task role per service, least privilege. Example: `app-api` may write to `documents` and `media` and send to SQS for print jobs, but cannot read `archive`.
- Production data access by engineers: break-glass role only, with 2-person approval, session recording through Session Manager, alert to security channel.

### 6.2 Encryption
| Data | At rest | In transit |
|---|---|---|
| Aurora | KMS CMK `wms-db` | TLS 1.2+ enforced |
| Redis | KMS | TLS |
| S3 | KMS CMK per bucket class | TLS-only policy |
| SQS/SNS | KMS | TLS |
| Handheld local queue | SQLCipher, key in Android Keystore | TLS 1.2+ |
| Edge Agent local store | DPAPI-protected SQLite key | TLS 1.2+ |
| Backups and snapshots | Same CMK, copied with ap-south-2 CMK | n/a |

Dedicated-tier tenants (1.2) can bring their own KMS key (NFR-041).

### 6.3 Secrets
- AWS Secrets Manager for DB credentials (automatic rotation every 30 days), OpenIddict signing and encryption certificates, Shiprocket credentials per client, SFTP passwords where unavoidable, SES SMTP.
- Services read secrets at startup and on rotation events; nothing in environment files or images.

### 6.4 Application security (OWASP ASVS Level 2)

| Area | Control |
|---|---|
| Auth | OpenIddict; PKCE; access token 15 min; refresh token rotation with reuse detection; MFA (FR-IAM-03) |
| Session | Secure, HttpOnly, SameSite=Strict cookies for web BFF; handheld tokens in Android Keystore |
| Access control | Flag, permission, scope filters (LLD 2.8); tenant isolation in 3 layers; deny by default |
| Input | FluentValidation on every command; parameterized SQL only (EF Core and Dapper); max request body 1 MB (10 MB for imports, streamed) |
| File upload | Type check by content (magic bytes), size limit, stored in S3 with random keys, served through pre-signed URLs (5 min) |
| Output | Blazor encodes by default; strict Content-Security-Policy; no inline scripts except Blazor boot hash |
| Webhooks | HMAC-SHA256 signing; customers' endpoints must be HTTPS; SSRF protection: block private IP ranges and metadata endpoints on outbound calls |
| Integration API | Per-client rate limits; request size limits; scope checks; client bound to 3PL client IDs |
| Logging | No passwords, PINs, tokens, or full ship-to addresses or phone numbers in logs; masking enricher |
| Dependencies | Central package versions; Dependabot; `dotnet list package --vulnerable` gate; licence check (HLD section 5 note) |

### 6.5 Detection and response
- GuardDuty (including ECS runtime and RDS protection), Security Hub with AWS Foundational and CIS benchmarks, AWS Config rules, CloudTrail org trail to the security account (log file validation on).
- Application security events (failed logins, cross-tenant attempts per FR-PLT-06, support access) go to a dedicated CloudWatch log group with alarms.
- **Cross-tenant attempt alarm:** any `TENANT_MISMATCH` event pages on-call immediately.
- Incident runbook with severity levels; DPDP breach notification steps included.

### 6.6 Handheld and Edge
- Handhelds managed by MDM (customer's or ours); app distributed as a managed private app.
- **No TLS certificate pinning** in 1.0: certificate rotation by ACM would break devices in the field. Relying on MDM-controlled trust store and TLS 1.2+. Revisit if a customer's security review demands pinning (pin to the Amazon root CA, not the leaf).
- Device revoke invalidates refresh tokens within 60 s (FR-IAM-06).
- Edge Agent MSI and handheld APK are code-signed. Edge Agent only accepts commands over its authenticated hub connection; it never listens on a port.

### 6.7 Data protection (DPDP)
- Personal data held: user names, emails, employee codes; ship-to name, address, phone on orders and shipments.
- Ship-to data on orders older than `privacy.shipToRetentionDays` (default 365) is masked by a nightly job; ledger keeps only order references.
- Data subject requests handled through a Platform support procedure: export or erase user/consignee data, logged.

---

## 7. CI/CD

### 7.1 Pull request pipeline (GitHub Actions)

| Stage | Gate |
|---|---|
| Restore, build (warnings as errors) | Must pass |
| Unit + application tests | Must pass; coverage on Domain and Application of changed modules >= 80% |
| Architecture tests | Must pass |
| Integration tests (Testcontainers PostgreSQL 17, Valkey) | Must pass |
| Tenant isolation suite | Must pass (release blocker, NFR-042) |
| OpenAPI diff on `/api/v1` | No breaking change without a new version |
| CodeQL (C#) | No new High/Critical |
| Dependency and licence scan | No known High/Critical vulnerability; no disallowed licence |
| Secret scanning | Must pass |
| Terraform `fmt`, `validate`, `plan` (if infra changed) | Plan posted to PR |

Target PR pipeline time: under 15 minutes (integration tests run in parallel per module).

### 7.2 Main branch and release flow

```
merge to main
  > build images (linux/arm64), tag = git SHA, push to ECR (wms-shared), sign images
  > deploy to dev (auto)
  > deploy to test (auto) > E2E suite (Playwright web, Appium handheld)
  > deploy to staging (manual approval, end of sprint) > smoke + perf regression (k6, 15 min)
  > deploy to prod (manual approval by Tech Lead + PO)
```

**Production deploy steps:**
1. Run `migrator` task (additive migrations only; expand/contract rule from LLD 13).
2. ECS rolling update with **deployment circuit breaker and automatic rollback** on failed health checks.
3. Post-deploy smoke tests against a synthetic tenant.
4. New features stay behind flags; **ring rollout** by flag: internal tenant, then pilot tenant, then all (NFR-103).

**Rollback:** previous task definition re-deployed (one click); flags turned off for feature issues; migrations are backward compatible for one release, so code rollback never needs a DB rollback.

### 7.3 Clients
- **Handheld:** CI builds signed APK; uploaded to managed Play / MDM. Minimum supported version enforced by the API (`X-App-Version`).
- **Edge Agent:** signed MSI published to S3; agents self-update outside shift hours.
- **Web:** static files deployed to S3 with versioned folders; CloudFront invalidation of `index.html` only.

### 7.4 Infrastructure as code
- Terraform, one root module per account and environment, shared modules for VPC, ECS service, Aurora, etc.
- State in S3 (wms-shared) with native S3 locking, encrypted, versioned.
- `plan` on PR, `apply` from pipeline only (no console changes in staging or prod; AWS Config flags drift).

---

## 8. Observability and SLOs

### 8.1 Telemetry pipeline
- OpenTelemetry SDK in all hosts > AWS Distro for OpenTelemetry (ADOT) collector sidecar > CloudWatch Metrics, X-Ray traces, CloudWatch Logs.
- Handheld sends client-side timings (scan to screen) in batches to `/api/app/telemetry`, so NFR-010 is measured end to end, not just server side.

### 8.2 SLOs (prod)

| SLO | Target | Window |
|---|---|---|
| Handheld scan p95 (client measured) | < 300 ms | Rolling 7 days, per warehouse |
| App API availability (non-5xx) | 99.9% | 30 days |
| Integration API availability | 99.9% | 30 days |
| Order intake latency (API received to order visible) | < 60 s p99 | 7 days |
| Outbox lag (event created to published) | < 10 s p99 | 7 days |
| Webhook first attempt | < 30 s p95 | 7 days |

### 8.3 Alarms (paging)

| Alarm | Condition |
|---|---|
| Scan latency | p95 > 300 ms for 10 min in any warehouse during its shift |
| 5xx rate | > 1% for 5 min on App API or Integration API |
| Cross-tenant attempt | any `TENANT_MISMATCH` |
| Ledger mismatch | nightly check finds any difference (FR-INV-13) |
| Outbox lag | > 60 s for 5 min |
| DLQ | any message in any DLQ |
| Aurora | writer CPU > 70% for 15 min; replica lag > 30 s; free storage alerts |
| Edge Agent | offline > 2 min during shift (customer notification, not paging) |

Initial alert routing: SNS > email and a team chat channel. Add an on-call tool before the second paying tenant.

---

## 9. Disaster Recovery

| Failure | Mechanism | RPO | RTO |
|---|---|---|---|
| Task or AZ failure | Multi-AZ ECS, Aurora reader promoted automatically, Redis replica | 0 | < 2 min (automatic) |
| Bad deploy | Circuit breaker rollback, flags off | 0 | < 10 min |
| Data corruption (bug or bad import) | Aurora point-in-time restore to a new cluster, then targeted repair | to the second | 1 h |
| **Region loss (ap-south-1)** | Aurora Global Database headless secondary in ap-south-2: add instances, promote; Terraform applies DR compute; S3 replicas; ECR replication; Route 53 and CloudFront origin switch | **~1 s** (storage replication) | **< 1 h** |

**Why Global Database instead of snapshot copies:** cross-region snapshot copies would give an RPO of hours for region loss, which breaks NFR-022. A headless secondary adds only replicated storage and I/O cost, not instance cost, until failover.

**DR drills:**
- Before pilot go-live: one full point-in-time restore test and one region failover rehearsal in staging.
- Then twice a year in production with a synthetic tenant, results recorded.

---

## 10. Cost Estimate (prod account, monthly)

**Rough order of magnitude for planning.** Validate with the AWS Pricing Calculator for ap-south-1 before budgeting; prices change and depend on usage.

| Item | Pilot (1 tenant, 1 to 2 WH) | Year 1 (40 WH) |
|---|---|---|
| ECS Fargate ARM (all services) | $200 to 300 | $600 to 900 |
| Aurora PostgreSQL (writer + reader, storage, I/O, backups) | $600 to 800 | $1,500 to 2,500 |
| Aurora Global DB secondary (storage + replication) | $50 to 150 | $200 to 400 |
| ElastiCache | $80 to 120 | $300 to 400 |
| ALB, NAT gateways, data transfer | $200 to 300 | $400 to 700 |
| CloudFront, WAF | $50 to 100 | $150 to 300 |
| S3, Transfer Family, SES | $250 to 300 (Transfer Family endpoint is the bulk) | $350 to 500 |
| CloudWatch, X-Ray, GuardDuty, Security Hub, Config | $150 to 250 | $400 to 700 |
| **Total prod** | **~$1,600 to 2,300** | **~$3,900 to 6,400** |
| Non-prod (dev, test, staging, scaled down off-hours) | ~$800 to 1,200 | ~$1,200 to 1,800 |

**Unit economics check:** Year 1 prod at ~$100 to 160 per warehouse per month for infrastructure. This must stay well under the per-warehouse price (BR-012) to keep gross margin above 75%. Biggest levers: Aurora instance size, log volume, NAT traffic.

**Cost guardrails:** AWS Budgets alerts at 80% and 100% of monthly plan; cost allocation tags `tenant_tier`, `service`, `env`; monthly review of top 5 cost lines.

---

## 11. NFR Verification Matrix

| NFR | Target | How verified | Tool | When | Pass criteria |
|---|---|---|---|---|---|
| NFR-010 | Scan p95 < 300 ms | Client-measured timings; load test with 200 virtual handhelds | Handheld telemetry, k6 | SP-01, EN-08, prod SLO | p95 < 300 ms at 3x pilot peak |
| NFR-011 | Web page < 1.5 s | Synthetic page loads | Playwright timings, CloudWatch RUM | Reports/hardening sprints | p95 < 1.5 s |
| NFR-012 | Integration API < 500 ms | Load test on `/api/v1/orders` bulk | k6 | Hardening | p95 < 500 ms at 50 req/s per client |
| NFR-013 | Allocation 5,000 lines < 60 s | Seeded staging data, timed run | k6 + custom harness | US-048 AC6, hardening | < 60 s |
| NFR-014 | Trace < 5 s | 2 years synthetic ledger (~40M rows per tenant) | Harness | US-075 AC3 | < 5 s |
| NFR-015 | Dashboard < 3 s | Synthetic | Playwright | US-077 | p95 < 3 s |
| NFR-016 | Report < 10 s | 12 months synthetic data | Harness | US-080 | < 10 s |
| NFR-020 | 99.9% | Uptime and error-budget tracking | CloudWatch SLO | Prod, monthly | >= 99.9% |
| NFR-021 | Zero-downtime deploy | Deploy during load test | k6 + deploy | Hardening | No failed requests attributable to deploy |
| NFR-022 | RPO 5 min, RTO 1 h | PITR restore + region failover rehearsal | Runbook drill | Before pilot, then twice a year | Restore and failover within targets |
| NFR-030 | India data residency | Config review: all data stores in ap-south-1/2 | AWS Config rule | Continuous | No resource outside allowed regions |
| NFR-040 | ASVS L2 | Checklist review + pen test | Manual + vendor | Hardening | No open Critical/High |
| NFR-041 | Encryption | AWS Config rules (encrypted storage, TLS-only) | AWS Config, Security Hub | Continuous | 100% compliant |
| NFR-042 | Tenant isolation | Isolation suite over all endpoints; RLS tests; pen test cross-tenant cases | xUnit suite, pen test | Every PR, hardening | 0 failures |
| NFR-043 | Secrets in vault | Secret scanning; image scan | GitHub, Inspector | Every PR | 0 findings |
| NFR-044 | Rate limits | Burst test on one tenant while measuring another's latency | k6 | Hardening | Other tenant's p95 unchanged (within 10%) |
| NFR-045 | Pen test | External vendor | Vendor report | Hardening | Critical/High fixed |
| NFR-050 | DPDP | Data map review; masking job test; erase procedure test | Manual + integration test | Hardening | Procedure works end to end |
| NFR-060 | 7-year retention | Archive job writes Parquet with Object Lock; Athena query test | Integration test | Reports/hardening | Archived trace query works |
| NFR-061 | Tenant export | Export procedure on synthetic tenant | Runbook | Hardening | Complete export within 7 days (target: hours) |
| NFR-070 | Telemetry tags | Trace sampling review | X-Ray | Hardening | Every span has tenant and warehouse |
| NFR-080 | Usage metering | Compare metered orders vs DB counts | Integration test | Hardening | 100% match |
| NFR-090/091 | Usability | Observed sessions with pilot pickers | Stopwatch, checklist | UAT | New picker productive in <= 30 min |
| NFR-092 | WCAG 2.1 AA | Automated + manual check of key screens | axe-core via Playwright | Reports/hardening | No serious violations |
| NFR-100 | Module boundaries | Architecture tests | `tests/Wms.ArchitectureTests` | Every PR | 0 violations |
| NFR-101 | 80% coverage | Coverage report | Coverlet | Every PR | >= 80% Domain + Application |
| NFR-102 | API versioning | OpenAPI diff | oasdiff | Every PR | No unversioned breaking change |
| NFR-103 | Release cadence | Release log | GitHub releases | Every sprint | Release every 2 weeks |

---

## 12. Runbooks Required Before Pilot

| Runbook | Owner |
|---|---|
| New tenant and warehouse onboarding | PO + Support |
| Edge Agent install and printer setup | Support |
| Handheld enrolment (MDM + device registration) | Support |
| Point-in-time restore and data repair | Tech Lead |
| Region failover | Tech Lead |
| DLQ triage and replay | Developer on call |
| Ledger mismatch investigation | Tech Lead |
| Security incident and DPDP breach notification | Tech Lead + PO |
| Tenant export and deletion | Tech Lead |

---

## 13. AWS Setup Checklist (solo Sprint 7)

- [ ] AWS Organization and 6 accounts; IAM Identity Center with MFA
- [ ] GitHub OIDC roles per account
- [ ] Terraform state bucket, shared modules skeleton
- [ ] Dev and test: VPC, ECS cluster, Aurora (db.t4g.medium in dev), Valkey, S3, SNS/SQS
- [ ] Domain, Route 53 zone, ACM wildcard certificates
- [ ] CloudFront + WAF baseline
- [ ] ECR with image scanning, cross-region replication rule
- [ ] CloudTrail org trail, GuardDuty, Security Hub, Config enabled in all accounts
- [ ] AWS Budgets per account
