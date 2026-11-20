# WMS SaaS: Product Requirements Document (PRD)

| Field | Value |
|---|---|
| Version | 1.0 |
| Date | 2026-11-21 |
| Phase | 2. Product Owner / BA, step 1 of 3 |
| Source | `01-business-requirements.md` (BRD v1.0) |
| Next | Functional Specification, then Epics and User Stories |

---

## 1. Problem

Indian 3PLs run multi-client warehouses on spreadsheets, ERP add-ons, or expensive enterprise WMS suites. The common pain points:

- **Slow onboarding.** A new client or warehouse takes weeks of setup and custom work.
- **Weak traceability.** A batch recall means days of manual digging through records.
- **Rigid workflows.** Each client wants different rules (FEFO, QC, shelf life). Tools force one way.
- **Desktop-bound operations.** Floor staff walk back to a PC, or use paper.
- **Cost.** Enterprise suites price out mid-size 3PLs.

## 2. Product Goal

A multi-tenant WMS that gets a 3PL warehouse live in under 5 working days, runs all floor work on a handheld, and traces any batch in seconds.

## 3. Success Metrics

### 3.1 Pilot metrics (first 90 days after Release 1.0 go-live)

| Metric | Target | Source |
|---|---|---|
| Time to onboard pilot warehouse | 5 working days or less | Onboarding log |
| Inventory record accuracy | 98%+ by day 60 | Cycle count results |
| Pick accuracy | 99.5%+ | Pack verification errors |
| Handheld scan response p95 | Under 300 ms | APM |
| Operator training time | 30 minutes or less per picker | Pilot observation |
| Batch recall trace | Under 5 s | APM |
| Cross-tenant data incidents | Zero | Security monitoring |
| Unplanned downtime during shifts | Under 43 min per month | Uptime monitor |
| Pilot renewal | Signed paid contract by day 90 | Commercial |

### 3.2 Business metrics (Year 1)
- 10 paying tenants, 40 warehouses
- Average onboarding under 5 days across all tenants
- Monthly logo churn under 2%

## 4. Personas

| Persona | Role | Main need | Primary device |
|---|---|---|---|
| **Rakesh**, Warehouse Manager | Runs one site, 80 staff, 6 clients | Know what's late, fix it before the carrier cutoff | Web, desktop |
| **Priya**, Inventory Controller | Owns stock accuracy and recalls | Find any batch fast, prove every adjustment | Web |
| **Suresh**, Picker | Floor operator, basic smartphone literacy | Clear next task, fast scans, no typing | Handheld |
| **Anita**, Inbound Supervisor | Plans dock and receiving | See expected ASNs, assign receivers, clear QC | Web + handheld |
| **Vikram**, Tenant Admin (IT) | Sets up warehouses, users, integrations | Fast setup, imports, clear integration errors | Web |
| **Meera**, Client (brand ops) | Customer of the 3PL | See own stock and order status without calling | Client portal |

## 5. Release Plan (scope cut for R-01)

The BRD MVP needs about 16 to 18 sprints. We have 12. So the BRD MVP is split into three releases.

**Principle for the cut:** Release 1.0 must let a pilot 3PL run **receive, store, pick, pack, ship, return, count, trace** for real clients. Anything that can be done manually or added a few weeks into the pilot moves to 1.1.

**Key point:** 3PL billing can move to 1.1 safely. The inventory ledger is immutable and timestamped, so charges for the first weeks of the pilot can be calculated afterwards from ledger data. Nothing is lost.

| Release | Sprints | Purpose |
|---|---|---|
| **1.0 Pilot** | 1 to 12 | Run one pilot 3PL warehouse end to end |
| **1.1 Commercial** | 13 to 16 | What a 3PL needs to pay: billing, client portal, Hindi, wave picking |
| **1.2 Enterprise** | 17 to 20 | What larger buyers need: SSO, dedicated DB, more connectors |

### 5.1 Scope by release (MoSCoW for 1.0)

| Capability | BRD ref | 1.0 | 1.1 | 1.2 |
|---|---|---|---|---|
| Tenant provisioning, subdomain, logo | BR-010, BR-018 | Must | | |
| Feature flag framework (plan, tenant, client) | BR-013 | Must | | |
| Shared-DB tenancy with tenant isolation tests | BR-011, NFR-042 | Must | | |
| Dedicated DB tier | BR-011 | | | Yes |
| Users, role templates, scoped RBAC, SoD rules | BR-100 to 103 | Must | | |
| Custom roles (clone template) | BR-101 | Should | | |
| Web login with MFA | BR-110 | Must | | |
| SSO (OIDC) | BR-110 | | | Yes |
| Handheld badge + PIN login, device registry | BR-111, BR-115 | Must | | |
| Security audit log | BR-117 | Must | | |
| Platform support access with consent | 5.2 | | Yes | |
| Config inheritance (Tenant > WH > Client > SKU) | BR-081 | Must | | |
| Config versioning, effective dates, preview | BR-087 | | Yes | |
| Custom fields | BR-014 | | Yes | |
| Label templates (LPN, location, SSCC, shipping) | BR-014 | Must | | |
| Item master, UOM hierarchy, Excel import | BR-021, BR-017 | Must | | |
| Location hierarchy, types, attributes, bulk create | BR-070 to 073 | Must | | |
| LPN inventory, immutable ledger, statuses | BR-020, 022, 090 | Must | | |
| Batch, expiry, serial capture | BR-021 | Must | | |
| Opening stock import | BR-017 | Must | | |
| ASN (API + Excel) and blind receiving | BR-030 | Must | | |
| QC inspection step | BR-031 | Must | | |
| Directed putaway (zone, client, FEFO rules) | BR-032 | Must | | |
| Velocity-based putaway rules | BR-032 | | Yes | |
| Moves, status changes, adjustments with approval | BR-103 | Must | | |
| Order intake (API + Excel), B2B and B2C | BR-040 | Must | | |
| Allocation FEFO/FIFO, min shelf life, priority | BR-041, 083 | Must | | |
| Discrete and batch picking | BR-042 | Must | | |
| Wave picking | BR-042 | | Yes | |
| Short-pick handling | BR-084 | Must | | |
| Pack station scan verify, packing slip, label | BR-043 | Must | | |
| Weight check at pack (scales) | BR-085, 130 | | Yes | |
| Manifest, dispatch, e-way bill export | BR-044, 126 | Must | | |
| Returns with grading | BR-050 | Must | | |
| Replenishment: manual task creation | BR-051 | Must | | |
| Replenishment: automatic min/max | BR-051 | | Yes | |
| Cycle count: scheduled and ad hoc, blind, recount, approval | BR-052 | Must | | |
| ABC classification for counts | BR-052 | | Yes | |
| Traceability: forward, backward, LPN history | BR-091 to 093 | Must | | |
| Recall block across warehouses | BR-094 | Must | | |
| Integration API + webhooks + CSV/SFTP | BR-120 to 122 | Must | | |
| Integration Monitor (view, replay) | BR-129 | Must | | |
| Shiprocket connector | BR-124 | Must | | |
| Delhivery, Tally Prime connectors | BR-124 | | Yes | |
| Shopify connector | BR-124 | | | Yes |
| Edge Agent: printing | BR-131 | Must | | |
| Edge Agent: scales | BR-131 | | Yes | |
| Handheld offline scan queue (under 5 min) | BR-061 | Must | | |
| Handheld Hindi | BR-116 | | Yes | |
| Operations dashboard, inventory health dashboard | 7.1 | Must | | |
| Productivity, multi-warehouse dashboards | 7.1 | | | Yes |
| 13 core reports (list in 5.2) | 7.3 | Must | | |
| Remaining 7 reports, scheduled email reports | 7.3, BR-141 | | Yes | |
| 3PL billing (rate cards, charge report) | BR-015 | | Yes | |
| Client portal (read-only) | BR-016 | | Yes | |
| Onboarding setup wizard | BR-017 | Should | | |
| Email and in-app notifications | BR-132 | Should | | |

**Won't (any of 1.0 to 1.2):** cross-docking, kitting, cold chain, catch weight, hazmat, labor management, yard management, slotting, automation equipment, custom report builder, WhatsApp/SMS.

### 5.2 Release 1.0 reports (13)
Stock on hand; Stock movement ledger; Expiry; GRN; ASN vs received variance; Putaway pending; Order status; Short pick; Shipment manifest; Returns register; Count variance; Adjustments with approvers; Batch trace (forward and backward, includes LPN history); User audit log.

## 6. Epics

| ID | Epic | Release | Sprints |
|---|---|---|---|
| E01 | Platform Foundation (tenancy, feature flags, module framework, CI/CD) | 1.0 | 1 |
| E02 | Identity and Access (users, roles, scope, MFA, handheld login, audit) | 1.0 | 1 to 2 |
| E03 | Configuration Engine (inheritance, workflow switches) | 1.0 / 1.1 | 2, 13 |
| E04 | Item Master | 1.0 | 2 |
| E05 | Location Management | 1.0 | 3 |
| E06 | Handheld App Shell and Edge Agent | 1.0 / 1.1 | 3, 14 |
| E07 | Inventory Core (LPN, ledger, statuses, adjustments) | 1.0 | 4, 6 |
| E08 | Inbound (ASN, receiving, QC, putaway) | 1.0 | 5 to 6 |
| E09 | Outbound (orders, allocation, picking, packing, shipping) | 1.0 / 1.1 | 7 to 9, 14 |
| E10 | Integration Hub (API, webhooks, files, monitor, connectors) | 1.0 / 1.1 / 1.2 | 5, 7, 9, 15, 17 |
| E11 | Returns | 1.0 | 10 |
| E12 | Cycle Counting and Replenishment | 1.0 / 1.1 | 10, 14 |
| E13 | Traceability and Recall | 1.0 | 11 |
| E14 | Reporting and Dashboards | 1.0 / 1.1 / 1.2 | 11, 16, 18 |
| E15 | 3PL Billing | 1.1 | 13 to 14 |
| E16 | Client Portal | 1.1 | 15 |
| E17 | Localization (Hindi) | 1.1 | 16 |
| E18 | Enterprise (SSO, dedicated DB, Shopify) | 1.2 | 17 to 20 |
| E19 | Release Hardening (perf, security, UAT) | 1.0 | 12 |

## 7. Release 1.0 Sprint Roadmap

| Sprint | Goal | Demo at sprint review |
|---|---|---|
| 1 | Platform skeleton runs in AWS. A tenant can be created and a user can log in. | Create tenant, log in with MFA, toggle a module flag. **Handheld latency spike** result (R-03). |
| 2 | Users, roles, scoped permissions, config inheritance, item master. | Admin creates a Picker scoped to one zone. Import 5,000 SKUs from Excel. |
| 3 | Locations and handheld shell. | Bulk-create 1,000 bins, print labels via Edge Agent, log in on Zebra with badge + PIN. |
| 4 | Inventory core. | Import opening stock with batches, see it on handheld and web, ledger shows every event. |
| 5 | Inbound receiving. | ASN via API, receive on handheld with batch/expiry capture, GRN generated. |
| 6 | QC, putaway, inventory control. | QC hold and release, system-directed putaway, adjustment needs second approver. |
| 7 | Orders and allocation. | Orders via API, FEFO allocation respects min shelf life, discrete pick on handheld. |
| 8 | Batch pick and packing. | Batch pick 20 orders, short-pick reallocates, pack with scan verify, print label. |
| 9 | Shipping and webhooks. | Shiprocket AWB and label, manifest, dispatch, e-way bill file, `order.shipped` webhook fires. |
| 10 | Returns, counting, replenishment. | Grade a return, blind count with auto recount, manual replenishment task. |
| 11 | Traceability and reports. | Recall a batch across 2 warehouses in one action. Trace under 5 s. Ops dashboard live. |
| 12 | Hardening and pilot readiness. | Load test at 3x peak, pen test fixes, tenant isolation suite green, pilot UAT sign-off. |

**Capacity assumption:** about 30 to 35 story points per sprint for the team. Sprint 1 velocity will reset this.

## 8. Release 1.0 Exit Criteria
- All Must items done and meeting acceptance criteria
- p95 scan response under 300 ms at 3x pilot peak load
- Tenant isolation test suite 100% passing
- No open Critical or High defects; pen test Critical and High findings fixed
- Pilot UAT signed by the pilot 3PL's warehouse manager
- Runbooks for onboarding, support, backup restore (restore tested once)

## 9. Definition of Ready (story)
- Linked to an epic and a BR/NFR ID
- Acceptance criteria in Given/When/Then
- Feature flag and permission named
- UI sketch for new screens (web or handheld)
- Estimated by the team; fits in one sprint

## 10. Definition of Done (story)
- Code reviewed and merged; CI green
- Unit tests on domain and application layers (80%+ for new code)
- Integration test for each new API endpoint
- Tenant isolation test added if new data is stored
- Feature flag and permission wired; module off means 403 and hidden UI
- Audit or ledger events written where the BRD requires them
- Acceptance criteria verified by QA in the test environment
- API docs (OpenAPI) updated

## 11. Assumptions and Dependencies
| # | Item |
|---|---|
| A-01 | A pilot 3PL is signed by Sprint 4 to validate workflows (BRD R-02). Until then, PO validates with 2 to 3 3PL interviews. |
| A-02 | Pilot uses Zebra Android handhelds and Zebra ZPL printers. |
| A-03 | Shiprocket sandbox access available by Sprint 7. |
| A-04 | AWS account, domain, and certificates ready before Sprint 1. |
| A-05 | Architecture phase completes before Sprint 1 starts. |

## 12. Changes from BRD
| BRD item | Change | Reason |
|---|---|---|
| BR-015 3PL billing | MVP to Release 1.1 | Scope fit; charges can be computed later from the ledger |
| BR-016 Client portal | MVP to Release 1.1 | Not needed for pilot floor operations |
| BR-116 Hindi on handheld | MVP to Release 1.1 | English first; i18n built in from Sprint 3 |
| BR-110 SSO | MVP to Release 1.2 | Pilot uses MFA; SSO matters for enterprise sales |
| BR-042 Wave picking | MVP to Release 1.1 | Batch picking covers pilot volume |
| BR-087 Config versioning | MVP to Release 1.1 | 1.0 config changes are audited but not versioned |
| BR-124 Tally, Delhivery, Shopify | MVP to 1.1 / 1.2 | Shiprocket plus generic API covers pilot |
