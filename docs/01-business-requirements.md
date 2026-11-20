# WMS SaaS: Business Requirements Document (BRD)

| Field | Value |
|---|---|
| Version | 1.0 |
| Date | 2026-11-21 |
| Phase | 1. Business Agent (Discovery), complete |
| Next phase | 2. Product Owner / BA (PRD, Functional Spec, User Stories) |
| Status | Approved by product owner (Ajay) |

Requirement IDs (BR-xxx, NFR-xxx) are stable. The PRD and user stories must trace back to them.

---

## 1. Business Context

### 1.1 Vision
A configurable, mobile-first, multi-tenant Warehouse Management System. It onboards a warehouse in days, gives end-to-end batch and LPN traceability, and costs less than enterprise WMS suites.

### 1.2 Target customers (in rollout order)
1. **3PL providers** (MVP). Store and fulfil goods for many clients.
2. In-house warehouses of brands, distributors, retailers.
3. Manufacturers (raw material, WIP, finished goods stores).

### 1.3 Customer size
Enterprise-grade architecture (10+ sites, high volume, automation-ready). Initial sales focus: mid-to-large 3PLs with 3 to 15 warehouses. They close faster and validate the product.

### 1.4 Industries
- Horizontal 3PL core.
- First vertical pack: **Pharma and FMCG distribution**.
- MVP core: batch/lot, expiry with FEFO, serial (per SKU), multi-UOM.
- Later: cold chain, catch weight, kitting, hazmat.

### 1.5 Region
Global product. **MVP launch in India.**

### 1.6 Differentiators
| ID | Differentiator | How we measure it |
|---|---|---|
| BR-001 | Fast onboarding | New warehouse live in under 5 working days |
| BR-002 | Mobile-first | All floor work on Android handheld; new picker productive in 30 minutes |
| BR-003 | Configurable workflows | Per tenant / warehouse / client / SKU, no code changes |
| BR-004 | Lower price | Per-warehouse base plus add-ons, below enterprise WMS pricing |
| BR-005 | Vertical focus | Pharma and FMCG pack out of the box |
| BR-006 | Traceability | Batch recall trace across a tenant in under 5 seconds |

### 1.7 Delivery constraints
- Commercial SaaS product, greenfield. No legacy system, no pilot customer yet.
- Team of about 7: PO, Architect/Tech Lead, 3 .NET developers, 1 frontend/mobile developer, 1 QA.
- 2-week sprints. **MVP in 12 sprints (about 6 months)**, then pilot with one Indian 3PL.
- Stack: .NET Core backend, Clean Architecture, modular / feature-based. Mobile: **.NET MAUI**.

---

## 2. SaaS Model, Tenancy, Packaging

| ID | Requirement |
|---|---|
| BR-010 | Hierarchy: Tenant > Warehouses > Clients (goods owners) > SKUs. In-house tenants get one default client. |
| BR-011 | Hybrid isolation. Shared DB with tenant ID by default. Dedicated DB as a paid enterprise tier. |
| BR-012 | Pricing: base fee per warehouse + module add-ons + order-volume band. Requires usage metering (NFR-080). |
| BR-013 | Feature toggles: plan sets the ceiling; tenant admin toggles within it; per-client overrides supported. |
| BR-014 | Customization MVP: configurable rules, custom fields, label and document templates. Post-MVP: extension API. Later: rules engine. |
| BR-015 | 3PL client billing (MVP): rate cards for storage, inbound, outbound, VAS. Monthly charge report with export. No invoicing inside WMS. |
| BR-016 | Client portal (MVP): read-only stock, orders, reports. Post-MVP: client-created orders and ASNs. |
| BR-017 | Onboarding: sales-led, setup wizard, Excel import for SKUs, locations, opening stock. |
| BR-018 | White label MVP: logo and subdomain. Later: full theming, custom domain. |

---

## 3. Warehouse Operations Profile

### 3.1 Scale per warehouse
- Up to 50,000 SKUs, 100,000 locations
- 20,000 outbound order lines a day, peak 3x (sale events)
- 200 concurrent handheld users
- Largest tenant: 30 warehouses

### 3.2 Inventory
| ID | Requirement |
|---|---|
| BR-020 | LPN-based tracking for pallets and cartons. |
| BR-021 | Attributes: SKU, client, batch/lot, expiry, mfg date, serial (optional per SKU), UOM hierarchy (each, inner, case, pallet). |
| BR-022 | Statuses: Available, Allocated, Quarantine, QC Hold, Damaged, Expired, Returned. |
| BR-023 | Rotation: FEFO for expiry SKUs, FIFO otherwise, overridable per client. |
| BR-024 | Barcodes: GS1-128, EAN-13, QR, SSCC. |

### 3.3 Inbound
| ID | Requirement |
|---|---|
| BR-030 | ASN-based and blind receiving. |
| BR-031 | Configurable QC inspection per client or SKU. |
| BR-032 | System-directed putaway by rules (zone, velocity, FEFO, client segregation). |
| BR-033 | Cross-docking: post-MVP. |

### 3.4 Outbound
| ID | Requirement |
|---|---|
| BR-040 | B2B (case/pallet) and B2C (single unit) orders. |
| BR-041 | Allocation by FEFO/FIFO, client, order priority. |
| BR-042 | Picking MVP: discrete, batch, wave. Post-MVP: zone, cluster. |
| BR-043 | Pack station: scan-to-verify, carton selection, packing slip, shipping label. |
| BR-044 | Shipping: manifest, dispatch confirmation, e-way bill data export. |

### 3.5 Returns, replenishment, counting
| ID | Requirement |
|---|---|
| BR-050 | Returns in MVP (RTO is 20 to 30% in Indian e-commerce). Grading: restock, refurbish, damaged, dispose. |
| BR-051 | Min/max replenishment from reserve to pick faces. Demand-based later. |
| BR-052 | Cycle counting: ABC classes, scheduled and ad hoc, blind counts, auto recount above variance threshold, supervisor approval before posting. |

### 3.6 Devices
| ID | Requirement |
|---|---|
| BR-060 | Android handhelds (Zebra, Honeywell) for all floor work. |
| BR-061 | Offline tolerance: queue scans during drops under 5 minutes, then sync. Full offline mode out of scope. |
| BR-062 | Web app for supervisors, admins, client portal. |
| BR-063 | Automation (conveyors, AMRs, sorters): post-MVP via integration layer. |

### 3.7 Out of scope for MVP
Kitting, cold chain monitoring, catch weight, hazmat, labor management, yard and dock scheduling, slotting optimization, cross-docking.

---

## 4. Locations, Configurable Workflows, Traceability

### 4.1 Location management
| ID | Requirement |
|---|---|
| BR-070 | Hierarchy: Warehouse > Zone > Aisle > Bay > Level > Bin. Levels below Zone optional. |
| BR-071 | Types: Receiving dock, Staging, QC, Storage (rack, shelf, floor), Pick face, Pack station, Dispatch dock, Returns, Quarantine, Damaged. |
| BR-072 | Attributes: capacity (weight, volume, pallets), mixing rules (SKU, batch, client), allowed statuses, velocity class, pick sequence, status (Active, Blocked, Counting). |
| BR-073 | Bulk creation by pattern (e.g. `A-{01..20}-{01..10}-{A..E}`), Excel import, bulk label print. |

### 4.2 Configurable workflows
| ID | Requirement |
|---|---|
| BR-080 | Each process is steps plus switches. No code per customer. |
| BR-081 | Config inheritance: Platform > Tenant > Warehouse > Client > SKU. Most specific wins. |
| BR-082 | Inbound switches: ASN required, QC on/off, over-receipt tolerance %, minimum shelf life on receipt, scan-to-confirm putaway. |
| BR-083 | Allocation switches: FEFO/FIFO/LIFO, minimum shelf life per customer, partial allocation. |
| BR-084 | Picking switches: pick method, short-pick handling (backorder, cancel, reallocate), batch override. |
| BR-085 | Packing switches: scan verify, weight check with tolerance, carton rules. |
| BR-086 | Counting switches: blind or guided, recount threshold, approval levels. |
| BR-087 | Config is versioned, effective-dated, audited, with preview before publish. |

### 4.3 Traceability
| ID | Requirement |
|---|---|
| BR-090 | Immutable inventory ledger. Every movement is an event (who, what, when, from, to, device). Balances derive from it. |
| BR-091 | LPN genealogy: splits and merges tracked. |
| BR-092 | Backward trace from shipped carton to receipt and supplier. |
| BR-093 | Forward trace (recall) from batch to every order, customer, shipment. Under 5 seconds per tenant. |
| BR-094 | Recall workflow: block a batch across all warehouses in one action; allocations released and flagged. |
| BR-095 | Audit retention 7 years default, configurable. |

---

## 5. Users, Roles, Permissions

### 5.1 User groups
Platform (our team), Tenant (3PL staff), Client portal (3PL customers).

### 5.2 Role templates
- **Platform:** Super Admin; Support (tenant consent, max 24 h, audited).
- **Tenant office:** Tenant Admin, Warehouse Manager, Inventory Controller, Inbound Supervisor, Outbound Supervisor, QC Inspector, Billing User, Report Viewer.
- **Tenant floor:** Receiver, Putaway Operator, Picker, Packer, Dispatcher, Counter, Returns Operator. Multiple roles per user allowed.
- **Client portal:** Client Admin, Client Viewer.

### 5.3 Rules
| ID | Requirement |
|---|---|
| BR-100 | RBAC plus scope. Permission = `Module.Action`. Assignment scoped to warehouses, clients, optionally zones. |
| BR-101 | Custom roles cloned from read-only templates. |
| BR-102 | Feature-aware: disabled module hides permissions; API returns 403. |
| BR-103 | Separation of duties: no self-approval of counts; adjustments above value threshold (e.g. INR 50,000) need second approver; no self role edits. |

### 5.4 Authentication
| ID | Requirement |
|---|---|
| BR-110 | Web: email + password + TOTP MFA. OIDC SSO (Azure AD, Google, Okta) on enterprise plan. |
| BR-111 | Handheld: badge scan + 4 to 6 digit PIN, login under 5 seconds, shared devices. |
| BR-112 | Client portal: email + password, MFA optional. |
| BR-113 | API: OAuth2 client credentials per integration. |
| BR-114 | Sessions: web idle 30 min; handheld one shift (max 12 h), lock after 10 min idle. |
| BR-115 | Handhelds registered per warehouse; remote revoke. |
| BR-116 | Languages: web English; handheld English + Hindi. i18n-ready. |
| BR-117 | Security audit log: logins, failures, role changes, support access, config changes. |

---

## 6. Integrations

| ID | Requirement |
|---|---|
| BR-120 | Integration API in MVP: item master, ASNs, sales orders, returns, stock snapshot, shipment confirmation. |
| BR-121 | Webhooks in MVP: e.g. `asn.received`, `order.shipped`, `stock.adjusted`, `batch.recalled`. |
| BR-122 | File channel in MVP: CSV/Excel upload, SFTP drop folders. |
| BR-123 | Connectors are modules, enabled per tenant or client. |
| BR-124 | MVP connectors: Tally Prime, Shopify, Shiprocket, Delhivery. |
| BR-125 | Post-MVP: SAP S/4HANA, SAP B1, Dynamics 365, NetSuite, Zoho, Amazon, Flipkart, WooCommerce, Magento, Unicommerce, EasyEcom, Blue Dart, DHL, FedEx. |
| BR-126 | E-way bill: MVP export in government bulk-upload format. Post-MVP: direct API via GSP. |
| BR-127 | Orders reach WMS within 60 s. Stock pushed on change plus full reconciliation every 15 min. |
| BR-128 | Retries with backoff, dead-letter queue, idempotent APIs. |
| BR-129 | Integration Monitor screen: view, fix, replay messages without developers. |
| BR-130 | Hardware MVP: Zebra/Honeywell handhelds, Zebra (ZPL) and TSC printers, pack-station scales, USB scanners. RFID and automation post-MVP. |
| BR-131 | Warehouse Edge Agent (.NET service per site) for printers and scales, buffers print jobs during outages. |
| BR-132 | Notifications MVP: email, in-app. Post-MVP: WhatsApp, SMS. |

---

## 7. Reporting and Dashboards

### 7.1 Dashboards
Operations (live), Inventory health, Productivity, Multi-warehouse, Client, Billing.

### 7.2 KPIs (fixed definitions, configurable targets)
| KPI | Definition | Default target |
|---|---|---|
| On-time ship % | Shipped before cutoff / due | 98% |
| Order fill rate | Lines shipped in full / ordered | 97% |
| Pick accuracy | Error-free lines / picked | 99.5% |
| Inventory record accuracy | Zero-variance locations / counted | 98% |
| Dock-to-stock | GRN start to putaway confirm | Under 4 h |
| Order cycle time | Release to dispatch | Tracked |
| Lines per labor hour | Lines / task hours | Tracked |
| Space utilization | Occupied / active locations | Tracked |
| Returns turnaround | Received to graded | Under 48 h |

### 7.3 MVP reports (20)
Stock on hand; Stock movement ledger; Expiry; Aging; Location utilization; Batch forward trace; Batch backward trace; LPN history; GRN; ASN vs received variance; Putaway pending; Order status; Short pick; Shipment manifest; Picker productivity; Returns register; Count variance; Adjustments with approvers; Client charges; User activity and audit.

### 7.4 Capabilities
| ID | Requirement |
|---|---|
| BR-140 | Filters, sort, saved views. Export Excel, CSV, PDF. |
| BR-141 | Scheduled email delivery (daily, weekly, monthly). |
| BR-142 | Exports over 100,000 rows run in background with notification. |
| BR-143 | Permission-scoped data; tenant time zone and currency. |
| BR-144 | Dashboards max 1 min stale; reports max 5 min lag from a reporting store. |
| BR-145 | Post-MVP: custom report builder, BI feed (Power BI), predictive insights. |

---

## 8. Non-Functional Requirements

### 8.1 Scale (Year 3)
| ID | Requirement |
|---|---|
| NFR-001 | 500 tenants, 3,000 warehouses. |
| NFR-002 | 1 million outbound order lines a day platform-wide. |
| NFR-003 | 20,000 concurrent handheld users. |

### 8.2 Performance (p95)
| ID | Operation | Target |
|---|---|---|
| NFR-010 | Handheld scan to response | < 300 ms |
| NFR-011 | Web page load | < 1.5 s |
| NFR-012 | Integration API call | < 500 ms |
| NFR-013 | Wave allocation, 5,000 orders | < 60 s |
| NFR-014 | Batch recall trace | < 5 s |
| NFR-015 | Dashboard load | < 3 s |
| NFR-016 | Standard report, 12 months data | < 10 s |

### 8.3 Availability and recovery
| ID | Requirement |
|---|---|
| NFR-020 | 99.9% monthly uptime; 99.95% for dedicated tier. |
| NFR-021 | Zero-downtime deployments. |
| NFR-022 | RPO 5 min, RTO 1 h. |
| NFR-023 | Multi-AZ primary; cross-region backup replication. |

### 8.4 Hosting
| ID | Requirement |
|---|---|
| NFR-030 | AWS Mumbai (ap-south-1) primary, Hyderabad (ap-south-2) DR. Indian data stays in India. |
| NFR-031 | Regional stacks for global markets; tenant selects region at signup. |

### 8.5 Security
| ID | Requirement |
|---|---|
| NFR-040 | OWASP ASVS Level 2 baseline. |
| NFR-041 | TLS 1.2+ in transit; AES-256 at rest. BYOK for dedicated tier post-MVP. |
| NFR-042 | Automated tenant isolation tests in CI. Cross-tenant leak blocks release. |
| NFR-043 | Secrets in managed vault. |
| NFR-044 | Per-tenant rate limits and quotas. |
| NFR-045 | Pen test before pilot, then yearly. |
| NFR-046 | ISO 27001 and SOC 2 Type II targeted Year 2. |

### 8.6 Compliance
| ID | Requirement |
|---|---|
| NFR-050 | India DPDP Act 2023: minimize, log access, delete on request. |
| NFR-051 | GDPR on EU entry. |
| NFR-052 | GST fields on items and shipments; e-way bill export. |
| NFR-053 | Pharma: audit and approvals support GDP expectations. E-signature (Part 11 style) post-MVP. |

### 8.7 Data lifecycle
| ID | Requirement |
|---|---|
| NFR-060 | Ledger and audit retained 7 years default. |
| NFR-061 | Full tenant export within 7 days on request; deletion 90 days after contract end. |
| NFR-062 | Closed transactions older than 24 months archived, still queryable. |

### 8.8 Observability
| ID | Requirement |
|---|---|
| NFR-070 | Central logs, metrics, tracing tagged by tenant and warehouse. |
| NFR-080 | Usage metering per tenant (orders, lines, warehouses, users). |
| NFR-081 | Alerts on SLA breach, integration failure, queue backlog. |

### 8.9 Usability
| ID | Requirement |
|---|---|
| NFR-090 | Handheld: scan-first, one task per screen, large targets, glove-friendly. |
| NFR-091 | New picker productive within 30 minutes of training. |
| NFR-092 | Web: WCAG 2.1 AA. |
| NFR-093 | Android 10+, 3 GB RAM minimum. Latest Chrome and Edge. |

### 8.10 Engineering quality
| ID | Requirement |
|---|---|
| NFR-100 | Clean Architecture, strict module boundaries. Modules interact via contracts or events only, never shared tables. |
| NFR-101 | 80%+ unit coverage on domain and application layers. |
| NFR-102 | API versioning; old versions supported 12 months. |
| NFR-103 | Releases every 2 weeks behind feature flags; ring rollout (internal, pilot, all). |

---

## 9. Feature Modules (candidate list for toggling)

Each is independently enableable per plan, tenant, and where relevant per client.

| Module | MVP | Notes |
|---|---|---|
| Core (tenancy, users, roles, config) | Yes | Always on |
| Item Master | Yes | Always on |
| Location Management | Yes | Always on |
| Inventory and Ledger | Yes | Always on |
| Inbound (Receiving, QC, Putaway) | Yes | |
| Outbound (Allocation, Picking, Packing, Shipping) | Yes | |
| Returns | Yes | |
| Replenishment | Yes | |
| Cycle Counting | Yes | |
| Traceability and Recall | Yes | Pharma/FMCG pack |
| 3PL Billing | Yes | |
| Client Portal | Yes | Read-only |
| Reporting and Dashboards | Yes | |
| Integration Hub and Connectors | Yes | Connectors toggled individually |
| Label and Document Designer | Yes | Templates |
| Cross-docking, Kitting, Cold chain, Labor mgmt, Yard mgmt, Slotting | No | Post-MVP |

---

## 10. Decision Log

| # | Decision | Reason |
|---|---|---|
| D-01 | MVP market is 3PL | Hardest model (multi-client); others become a subset |
| D-02 | Sell to mid-to-large 3PLs first, architect for enterprise | Faster sales cycle, proves product |
| D-03 | Pharma/FMCG first vertical | Matches traceability differentiator and Indian 3PL demand |
| D-04 | Hybrid tenancy | Low cost by default, dedicated DB for enterprise deals |
| D-05 | LPN-based inventory with immutable ledger | Traceability and audit backbone |
| D-06 | .NET MAUI for mobile | Single C# skill set across team |
| D-07 | Integration API in MVP (moved from post-MVP) | Needed to hit 5-day onboarding goal |
| D-08 | Returns in MVP | High RTO in Indian e-commerce |
| D-09 | AWS India regions | Data residency, team familiarity |
| D-10 | Edge Agent per warehouse | Local printers and scales, resilience to internet drops |

## 11. Risks and Open Items for the Product Owner

| # | Item | Impact |
|---|---|---|
| R-01 | MVP scope is large for 12 sprints with 7 people. PO must cut to a "pilot-ready" slice and push the rest to releases 1.1 and 1.2. | High |
| R-02 | No pilot customer yet. Workflow assumptions need validation with at least 2 Indian 3PLs before Sprint 4. | High |
| R-03 | Handheld 300 ms target on Indian warehouse Wi-Fi needs early proof (spike in Sprint 1 or 2). | Medium |
| R-04 | Tally Prime integration options vary by customer setup. Confirm approach with pilot. | Medium |
| R-05 | Enterprise buyers may demand SSO, ISO/SOC reports earlier than Year 2. | Medium |
| R-06 | Billing rate card variety across 3PLs may exceed MVP model. | Medium |

## 12. Glossary
- **3PL:** Third-party logistics provider.
- **ASN:** Advance Shipping Notice.
- **GRN:** Goods Receipt Note.
- **LPN:** License Plate Number, a unique ID for a pallet or carton.
- **SSCC:** Serial Shipping Container Code (GS1).
- **FEFO / FIFO:** First Expired First Out / First In First Out.
- **RTO:** Return to Origin (undelivered e-commerce shipment).
- **VAS:** Value-added services (labeling, bundling, etc.).
- **GSP:** GST Suvidha Provider.
- **DPDP:** Digital Personal Data Protection Act, 2023 (India).
